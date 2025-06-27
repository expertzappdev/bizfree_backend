using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using BizfreeApp.Models;
using BizfreeApp.Models.DTOs;
using BizfreeApp.Data;

[ApiExplorerSettings(IgnoreApi = false)]
[AllowAnonymous]
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly BizfreeApp.Data.ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(BizfreeApp.Data.ApplicationDbContext context, IConfiguration configuration, ILogger<AuthController> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    private string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    private JwtSecurityToken GenerateAccessToken(User user)
    {
        var claims = new List<Claim>
        {
            new Claim("UserId", user.UserId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, user.Role?.RoleName ?? "User"),
            new Claim("RoleId", user.RoleId?.ToString() ?? "0"),
            new Claim("CompanyId", user.CompanyId?.ToString() ?? "0")
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        return new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds
        );
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        _logger.LogInformation($"Login attempt for email: {request.Email}");

        if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
        {
            _logger.LogWarning("Login failed: Email or password not provided.");
            return BadRequest(new { message = "Email and password are required.", status = "error", status_code = 400 });
        }

        // OPTIMIZATION 1: Single optimized query with all required data
        var userWithRolePermissionsAndCompanyUser = await _context.Users
            .Where(u => u.Email == request.Email && u.IsActive && !u.IsDeleted)
            .Select(u => new
            {
                User = u,
                RoleName = u.Role != null ? u.Role.RoleName : null,
                Permissions = u.RoleId.HasValue && u.CompanyId.HasValue
                    ? _context.Rolespermissions
                        .Where(rp => rp.RoleId == u.RoleId.Value && rp.CompanyId == u.CompanyId.Value)
                        .Select(rp => rp.Permission!.PermissionName)
                        .ToList()
                    : new List<string>(),
                CompanyUser = _context.CompanyUsers
                                    .FirstOrDefault(cu => cu.UserId == u.UserId)
            })
            .FirstOrDefaultAsync();

        if (userWithRolePermissionsAndCompanyUser?.User == null || userWithRolePermissionsAndCompanyUser.User.PasswordHash != request.Password)
        {
            _logger.LogWarning($"Login failed for email: {request.Email} - Invalid credentials.");
            return Unauthorized(new { message = "Invalid credentials.", status = "error", status_code = 401 });
        }

        var user = userWithRolePermissionsAndCompanyUser.User;
        var permissions = userWithRolePermissionsAndCompanyUser.Permissions;
        var companyUser = userWithRolePermissionsAndCompanyUser.CompanyUser;

        // Construct full name
        string? fullName = null;
        if (companyUser != null)
        {
            // Prefer "Full Name" as it's more professional.
            // Concatenate First Name and Last Name, handling potential nulls.
            fullName = $"{companyUser.FirstName} {companyUser.LastName}".Trim();
            if (string.IsNullOrEmpty(fullName))
            {
                fullName = null; // If both are null/empty, set fullName to null
            }
        }

        var accessToken = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation($"User {user.UserId} logged in successfully.");

        return Ok(new
        {
            message = "Login successful.",
            status = "success",
            status_code = 200,
            token = new JwtSecurityTokenHandler().WriteToken(accessToken),
            refreshToken = refreshToken,
            user = new
            {
                id = user.UserId,
                email = user.Email,
                role = userWithRolePermissionsAndCompanyUser.RoleName,
                roleId = user.RoleId,
                companyId = user.CompanyId,
                fullName = fullName // Added full name here
            },
            permissions = permissions
        });
    }

    private readonly Dictionary<string, (List<string> permissions, DateTime cachedAt)> _permissionsCache = new();
    private readonly TimeSpan _cacheTimeout = TimeSpan.FromMinutes(5);

    private async Task<List<string>> GetUserPermissionsAsync(int? roleId, int? companyId)
    {
        if (!roleId.HasValue || !companyId.HasValue)
            return new List<string>();

        var cacheKey = $"{roleId}_{companyId}";

        if (_permissionsCache.TryGetValue(cacheKey, out var cached) &&
            DateTime.UtcNow - cached.cachedAt < _cacheTimeout)
        {
            return cached.permissions;
        }

        var permissions = await _context.Rolespermissions
            .Where(rp => rp.RoleId == roleId.Value && rp.CompanyId == companyId.Value)
            .Include(rp => rp.Permission)
            .Select(rp => rp.Permission!.PermissionName)
            .ToListAsync();

        _permissionsCache[cacheKey] = (permissions, DateTime.UtcNow);
        return permissions;
    }


[HttpPost("login-cached")]
    public async Task<IActionResult> LoginWithCachedPermissions([FromBody] LoginRequest request)
    {
        _logger.LogInformation($"Login attempt for email: {request.Email}");

        if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
        {
            _logger.LogWarning("Login failed: Email or password not provided.");
            return BadRequest(new { message = "Email and password are required.", status = "error", status_code = 400 });
        }

        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive && !u.IsDeleted);

        if (user == null || user.PasswordHash != request.Password)
        {
            _logger.LogWarning($"Login failed for email: {request.Email} - Invalid credentials.");
            return Unauthorized(new { message = "Invalid credentials.", status = "error", status_code = 401 });
        }

        // Use cached permissions lookup
        var permissions = await GetUserPermissionsAsync(user.RoleId, user.CompanyId);

        var accessToken = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation($"User {user.UserId} logged in successfully.");

        return Ok(new
        {
            message = "Login successful.",
            status = "success",
            status_code = 200,
            token = new JwtSecurityTokenHandler().WriteToken(accessToken),
            refreshToken = refreshToken,
            user = new
            {
                id = user.UserId,
                email = user.Email,
                role = user.Role?.RoleName,
                roleId = user.RoleId,
                companyId = user.CompanyId
            },
            permissions = permissions
        });
    }

    // Rest of your existing methods remain the same...
    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        _logger.LogInformation("Refresh token request received.");

        if (string.IsNullOrEmpty(request.Token) || string.IsNullOrEmpty(request.RefreshToken))
        {
            _logger.LogWarning("Refresh token failed: Invalid client request (missing token or refresh token).");
            return BadRequest(new { message = "Invalid client request (token or refresh token missing).", status = "error", status_code = 400 });
        }

        var principal = GetPrincipalFromExpiredToken(request.Token);
        if (principal == null)
        {
            _logger.LogWarning("Refresh token failed: Invalid access token.");
            return BadRequest(new { message = "Invalid access token.", status = "error", status_code = 400 });
        }

        var userIdClaim = principal.Claims.FirstOrDefault(c => c.Type == "UserId");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
        {
            _logger.LogWarning("Refresh token failed: Token claims missing UserId.");
            return BadRequest(new { message = "Invalid token claims (UserId missing).", status = "error", status_code = 400 });
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null || user.RefreshToken != request.RefreshToken || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            _logger.LogWarning($"Refresh token failed for user {userId}: Invalid or expired refresh token.");
            return Unauthorized(new { message = "Invalid or expired refresh token.", status = "error", status_code = 401 });
        }

        var newAccessToken = GenerateAccessToken(user);
        var newRefreshToken = GenerateRefreshToken();

        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Access token refreshed successfully for user {userId}.");
        return Ok(new
        {
            message = "Token refreshed successfully.",
            status = "success",
            status_code = 200,
            token = new JwtSecurityTokenHandler().WriteToken(newAccessToken),
            refreshToken = newRefreshToken
        });
    }

    private ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidAudience = _configuration["Jwt:Audience"],
            ValidateIssuer = true,
            ValidIssuer = _configuration["Jwt:Issuer"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!)),
            ValidateLifetime = false
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);
            if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                _logger.LogWarning("GetPrincipalFromExpiredToken failed: Invalid security token or algorithm mismatch.");
                return null;
            }

            return principal;
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogError(ex, "SecurityTokenException occurred while validating expired token.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred in GetPrincipalFromExpiredToken.");
            return null;
        }
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
        {
            _logger.LogWarning("Logout failed: Invalid user session (UserId claim missing or invalid).");
            return Unauthorized(new { message = "Invalid user session.", status = "error", status_code = 401 });
        }

        _logger.LogInformation($"Logout attempt for user ID: {userId}");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId && u.IsActive && !u.IsDeleted);
        if (user == null)
        {
            _logger.LogWarning($"Logout failed: User {userId} not found or inactive/deleted.");
            return NotFound(new { message = "User not found or already logged out.", status = "error", status_code = 404 });
        }

        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation($"User {userId} logged out successfully at {DateTime.UtcNow}");
        return Ok(new { message = "Logged out successfully.", status = "success", status_code = 200 });
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
        {
            _logger.LogWarning("Change password failed: Invalid user session (UserId claim missing or invalid).");
            return Unauthorized(new { message = "Invalid user session.", status = "error", status_code = 401 });
        }

        _logger.LogInformation($"Change password attempt for user ID: {userId}");

        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword) || string.IsNullOrWhiteSpace(request.ConfirmPassword))
        {
            _logger.LogWarning($"Change password failed for user {userId}: All fields are required.");
            return BadRequest(new { message = "All fields are required.", status = "error", status_code = 400 });
        }

        if (request.NewPassword != request.ConfirmPassword)
        {
            _logger.LogWarning($"Change password failed for user {userId}: New password and confirm password do not match.");
            return BadRequest(new { message = "New password and confirm password do not match.", status = "error", status_code = 400 });
        }

        if (request.NewPassword.Length < 8 ||
            !request.NewPassword.Any(char.IsUpper) ||
            !request.NewPassword.Any(char.IsLower) ||
            !request.NewPassword.Any(char.IsDigit) ||
            !request.NewPassword.Any(c => "!@#$%^&*()_+-=[]{}|;':\",./<>?".Contains(c)))
        {
            _logger.LogWarning($"Change password failed for user {userId}: New password does not meet complexity requirements.");
            return BadRequest(new { message = "Password must be at least 8 characters long, include upper and lower case letters, a digit, and a special character.", status = "error", status_code = 400 });
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId && u.IsActive && !u.IsDeleted);
        if (user == null)
        {
            _logger.LogWarning($"Change password failed: User {userId} not found or inactive/deleted.");
            return NotFound(new { message = "User not found.", status = "error", status_code = 404 });
        }

        // Direct plain text password verification as per your working code
        if (user.PasswordHash != request.CurrentPassword)
        {
            _logger.LogWarning($"Change password failed for user {userId}: Current password incorrect.");
            return Unauthorized(new { message = "Current password is incorrect.", status = "error", status_code = 401 });
        }

        // Direct plain text password storage as per your working code
        user.PasswordHash = request.NewPassword;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Password changed successfully for user {userId}.");
        return Ok(new { message = "Password changed successfully.", status = "success", status_code = 200 });
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        _logger.LogInformation($"Forgot password request for email: {request.Email}");

        if (string.IsNullOrEmpty(request.Email))
        {
            _logger.LogWarning("Forgot password failed: Email is required.");
            return BadRequest(new { message = "Email is required.", status = "error", status_code = 400 });
        }

        // Validate the incoming email format before querying the database
        if (!IsValidEmail(request.Email))
        {
            _logger.LogWarning($"Forgot password failed: Provided email '{request.Email}' is not in a valid format.");
            return BadRequest(new { message = "Provided email is not in a valid format.", status = "error", status_code = 400 });
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email && !u.IsDeleted);

        // --- MODIFIED CHECK HERE ---
        if (user == null)
        {
            _logger.LogWarning($"Forgot password failed: User with email '{request.Email}' not found in the database.");
            // Return an error status code (e.g., 404 Not Found) if the email is not present.
            // Be aware that revealing if an email exists can be a security vulnerability (email enumeration).
            return NotFound(new { message = "The provided email address is not associated with an account.", status = "error", status_code = 404 });
        }
        // --- END MODIFIED CHECK ---

        // Ensure the user's email from the database is also valid before using it
        if (!IsValidEmail(user.Email!))
        {
            _logger.LogError($"Database user email '{user.Email}' for UserId {user.UserId} is malformed. Cannot send reset email.");
            return StatusCode(500, new { message = "An internal error occurred. Please contact support.", status = "error", status_code = 500 });
        }

        var token = GenerateRefreshToken(); // Using refresh token field for reset token
        user.RefreshToken = token;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddHours(1);
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        string frontendUrl = _configuration["AppSettings:FrontendUrl"] ?? "http://localhost:3000"; // Default fallback
        string resetLink = $"{frontendUrl}/reset-password?email={WebUtility.UrlEncode(user.Email!)}&token={WebUtility.UrlEncode(token)}";

        SendResetPasswordEmail(user.Email!, resetLink);

        _logger.LogInformation($"Password reset link generated and email attempt initiated for {request.Email}.");
        return Ok(new { message = "Password reset link sent to your email.", status = "success", status_code = 200 });
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        _logger.LogInformation($"Reset password attempt for email: {request.Email}");

        if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Token) || string.IsNullOrEmpty(request.NewPassword) || string.IsNullOrEmpty(request.ConfirmPassword))
        {
            _logger.LogWarning("Reset password failed: Missing required fields.");
            return BadRequest(new { message = "Email, token, new password, and confirm password are required.", status = "error", status_code = 400 });
        }

        if (request.NewPassword != request.ConfirmPassword)
        {
            _logger.LogWarning($"Reset password failed for email {request.Email}: Passwords do not match.");
            return BadRequest(new { message = "New password and confirm password do not match.", status = "error", status_code = 400 });
        }

        //if (request.NewPassword.Length < 8 ||
        //    !request.NewPassword.Any(char.IsUpper) ||
        //    !request.NewPassword.Any(char.IsLower) ||
        //    !request.NewPassword.Any(char.IsDigit) ||
        //    !request.NewPassword.Any(c => "!@#$%^&*()_+-=[]{}|;':\",./<>?".Contains(c)))
        //{
        //    _logger.LogWarning($"Reset password failed for email {request.Email}: New password does not meet complexity requirements.");
        //    return BadRequest(new { message = "New password must be at least 8 characters long, include upper and lower case letters, a digit, and a special character.", status = "error", status_code = 400 });
        //}

        var user = await _context.Users.FirstOrDefaultAsync(u =>
            u.Email == request.Email &&
            u.RefreshToken == request.Token &&
            u.RefreshTokenExpiryTime > DateTime.UtcNow &&
            !u.IsDeleted);

        if (user == null)
        {
            _logger.LogWarning($"Reset password failed for email {request.Email}: Invalid or expired reset token.");
            return BadRequest(new { message = "Invalid or expired reset token. Please try requesting a new reset link.", status = "error", status_code = 400 });
        }

        // Direct plain text password storage as per your working code
        user.PasswordHash = request.NewPassword;
        user.RefreshToken = null; // Invalidate the used token
        user.RefreshTokenExpiryTime = null;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Password reset successfully for email: {request.Email}.");
        return Ok(new { message = "Password has been reset successfully.", status = "success", status_code = 200 });
    }

    private void SendResetPasswordEmail(string toEmail, string resetLink)
    {
        var emailBody = $@"
        <html>
        <head>
            <style>
                body {{ font-family: Arial; background-color: #f5f5f5; padding: 20px; }}
                .container {{ max-width: 600px; margin: auto; background: #fff; padding: 30px; border-radius: 8px; }}
                .btn {{ display: inline-block; padding: 10px 20px; background: #007BFF; color: white; text-decoration: none; border-radius: 5px; }}
            </style>
        </head>
        <body>
            <div class='container'>
                <h2>Password Reset Request</h2>
                <p>Hello,</p>
                <p>Click below to reset your password:</p>
                <a href='{resetLink}' class='btn'>Reset Password</a>
                <p>If you didn't request this, ignore this email.</p>
                <p>Thanks,<br/>Bizfree Team</p>
            </div>
        </body>
        </html>";

        var smtpHost = _configuration["EmailSettings:SmtpHost"] ?? "smtp.gmail.com";
        var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
        var smtpUser = _configuration["EmailSettings:SmtpUser"] ?? "tata.punchgrey29@gmail.com";
        var smtpPass = _configuration["EmailSettings:SmtpPass"] ?? "wjnq oyph hgjo utaf";
        var fromEmail = _configuration["EmailSettings:FromEmail"] ?? "tata.punchgrey29@gmail.com";
        var fromName = _configuration["EmailSettings:FromName"] ?? "BizfreeApp Support";

        // --- IMPORTANT: Validate the 'From' email from configuration ---
        if (!IsValidEmail(fromEmail))
        {
            _logger.LogError($"Configuration Error: 'EmailSettings:FromEmail' is invalid. Value: '{fromEmail}'. Cannot send password reset email.");
            return; // Stop email sending if the configured 'from' email is invalid
        }

        // --- IMPORTANT: Validate the 'To' email received by the method ---
        if (!IsValidEmail(toEmail))
        {
            _logger.LogError($"Attempted to send email to an invalid address: '{toEmail}'. Cannot send password reset email.");
            return; // Stop email sending if the 'to' email is invalid
        }

        _logger.LogInformation($"Attempting to send email from: '{fromEmail}' to: '{toEmail}'");

        var message = new MailMessage
        {
            From = new MailAddress(fromEmail, fromName),
            Subject = "Password Reset",
            Body = emailBody,
            IsBodyHtml = true
        };

        message.To.Add(new MailAddress(toEmail));

        using var smtp = new SmtpClient(smtpHost)
        {
            Port = smtpPort,
            Credentials = new NetworkCredential(smtpUser, smtpPass),
            EnableSsl = true
        };

        try
        {
            smtp.Send(message);
            _logger.LogInformation($"Password reset email sent to {toEmail}");
        }
        catch (SmtpException ex)
        {
            _logger.LogError(ex, $"Failed to send password reset email to {toEmail}. SMTP Error: {ex.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"An unexpected error occurred while sending email to {toEmail}.");
        }
    }

    // Helper method to validate email format
    private bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }
        try
        {
            // Using MailAddress constructor for basic validation
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address.Equals(email, StringComparison.InvariantCultureIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}