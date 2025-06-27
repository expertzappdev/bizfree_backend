using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims; // Required for accessing user claims
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization; // Required for [Authorize] attribute
using BizfreeApp.Models; // Your existing models namespace
using BizfreeApp.DTOs; // The namespace for your new DTOs
using BizfreeApp.Services;
using System; // For DateTime.UtcNow and Guid

namespace BizfreeApp.Controllers; // Your API controllers namespace

[Route("api/[controller]")] // Defines the base route for this controller (e.g., /api/CompanyUsers)
[ApiController] // Indicates that this class is an ASP.NET Core API controller
//[Authorize] // Requires authentication for all actions in this controller
public class CompanyUsersController : ControllerBase
{
    private readonly BizfreeApp.Data.ApplicationDbContext _context;
    private readonly IUploadHandler _uploadHandler; // Inject IUploadHandler

    public CompanyUsersController(BizfreeApp.Data.ApplicationDbContext context,
                                  IUploadHandler uploadHandler)
    {
        _context = context;
        _uploadHandler = uploadHandler;
    }

    // --- Helper methods to extract claims ---

    /// <summary>
    /// Extracts the CompanyId from the authenticated user's claims.
    /// </summary>
    /// <returns>The CompanyId or null if not found/invalid.</returns>
    private int? GetCompanyIdFromClaims()
    {
        // First, check for the custom "CompanyId" claim as seen in your token
        var companyIdClaim = User.Claims.FirstOrDefault(c => c.Type == "CompanyId");

        // If not found, check for the standard ClaimTypes.GroupSid as a fallback from your policy setup
        if (companyIdClaim == null)
        {
            companyIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.GroupSid);
        }

        if (companyIdClaim != null && int.TryParse(companyIdClaim.Value, out int companyId))
        {
            return companyId;
        }
        return null;
    }

    /// <summary>
    /// Extracts the UserId from the authenticated user's claims.
    /// This will be used for CreatedBy and UpdatedBy fields.
    /// </summary>
    /// <returns>The UserId or null if not found/invalid.</returns>
    private int? GetCurrentUserIdFromClaims()
    {
        // Prioritize the custom "UserId" claim which is present in your token
        var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");

        // As a fallback, also check for the standard ClaimTypes.NameIdentifier
        if (userIdClaim == null)
        {
            userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        }

        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
        {
            return userId;
        }
        return null;
    }


    // --- READ Operations ---

    /// <summary>
    /// Retrieves a list of all company users with their associated user and department details.
    /// The department name will be included from the Department model, and all relevant details from the User model.
    /// This endpoint filters data by the CompanyId present in the authenticated user's token.
    /// </summary>
    /// <returns>An ActionResult containing a list of CompanyUserDetailsDto objects.</returns>
    [HttpGet("details")] // Defines the specific route for this action (e.g., /api/CompanyUsers/details)
    [ProducesResponseType(typeof(IEnumerable<CompanyUserDetailsDto>), 200)] // Documents the successful response type
    [ProducesResponseType(500)] // Documents potential server errors
    [ProducesResponseType(401)] // Unauthorized if Company ID claim is missing
    public async Task<ActionResult<IEnumerable<CompanyUserDetailsDto>>> GetCompanyUserDetails()
    {
        try
        {
            int? currentCompanyId = GetCompanyIdFromClaims();
            if (!currentCompanyId.HasValue) return Unauthorized("Company ID claim missing or invalid.");

            // Log the company ID being used for debugging
            Console.WriteLine($"GetCompanyUserDetails: Filtering by CompanyId: {currentCompanyId.Value}");

            // Query the CompanyUser table
            var companyUsers = await _context.CompanyUsers
                .Include(cu => cu.User)          // Eager load the related User data
                .Include(cu => cu.Department)    // Eager load the related Department data
                .Where(cu => !(cu.IsDeleted ?? false)) // Filter out soft-deleted users
                .Where(cu => cu.CompanyId == currentCompanyId.Value) // Apply company filter based on token
                .Select(cu => new CompanyUserDetailsDto // Project the results into our DTO
                {
                    // Map CompanyUser properties
                    UserId = cu.UserId, // Now the main identifier for the DTO
                    CompanyEmployeeId = cu.EmployeeId, // Renamed for clarity
                    RoleId = cu.RoleId,
                    CompanyId = cu.CompanyId,
                    DepartmentId = cu.DepartmentId,
                    IsActive = cu.IsActive,
                    EmploymentType = cu.EmploymentType,
                    Address = cu.Address,
                    AddressLine1 = cu.AddressLine1,
                    City = cu.City,
                    State = cu.State,
                    PostalCode = cu.PostalCode,
                    Country = cu.Country, // Added
                    Gender = cu.Gender,
                    BloodGroup = cu.BloodGroup,
                    PhoneNumber = cu.PhoneNumber,
                    DateOfBirth = cu.DateOfBirth,
                    MaritalStatus = cu.MaritalStatus,
                    JoiningDate = cu.JoiningDate,
                    EmployeeCode = cu.EmployeeCode,
                    ProfilePhotoUrl = cu.ProfilePhotoUrl,
                    Description = cu.Description,
                    IsDeleted = cu.IsDeleted,
                    CreatedAt = cu.CreatedAt,
                    CreatedBy = cu.CreatedBy,
                    UpdatedAt = cu.UpdatedAt,
                    UpdatedBy = cu.UpdatedBy,
                    CheckAdmin = cu.CheckAdmin,
                    FirstName = cu.FirstName,
                    LastName = cu.LastName,
                    AnniversaryDate = cu.AnniversaryDate,
                    ReportToUserId = cu.ReportToUserId,

                    // Map Emergency Contact Details
                    EmergencyContactName = cu.EmergencyContactName, // Added
                    EmergencyContactRelation = cu.EmergencyContactRelation, // Added
                    EmergencyContactNumber = cu.EmergencyContactNumber, // Added

                    // Map User properties
                    UserEmail = cu.User != null ? cu.User.Email : string.Empty,
                    // UserPasswordHash and RefreshToken fields removed for security
                    UserIsActive = cu.User != null ? cu.User.IsActive : false,
                    UserIsDeleted = cu.User != null ? cu.User.IsDeleted : false,
                    UserCreatedAt = cu.User != null ? cu.User.CreatedAt : (DateTime?)null,
                    UserUpdatedAt = cu.User != null ? cu.User.UpdatedAt : (DateTime?)null,
                    UserUpdatedBy = cu.User != null ? cu.User.UpdatedBy : (int?)null,
                    UserRoleId = cu.User != null ? cu.User.RoleId : (int?)null,
                    UserCompanyId = cu.User != null ? cu.User.CompanyId : (int?)null,

                    // Map DepartmentName
                    DepartmentName = cu.Department != null ? cu.Department.DepartmentName : null
                })
                .ToListAsync(); // Execute the query asynchronously

            return Ok(companyUsers); // Return the DTOs with a 200 OK status
        }
        catch (Exception ex)
        {
            // Log the exception (e.g., using ILogger in a real application)
            Console.WriteLine($"Error retrieving company user details: {ex.Message}");
            return StatusCode(500, "An error occurred while retrieving company user details.");
        }
    }

    /// <summary>
    /// Retrieves details for a specific company user by their UserId.
    /// Includes associated user and department details.
    /// This endpoint filters data by the CompanyId present in the authenticated user's token.
    /// </summary>
    /// <param name="userId">The unique identifier (UserId) of the user to retrieve.</param>
    /// <returns>An ActionResult containing a single CompanyUserDetailsDto or NotFound if not found.</returns>
    [HttpGet("details/user/{userId}")] // Route changed to use 'user/{userId}'
    [ProducesResponseType(typeof(CompanyUserDetailsDto), 200)]
    [ProducesResponseType(404)] // Not Found if user not found for specified UserId and CompanyId
    [ProducesResponseType(401)] // Unauthorized if Company ID claim is missing
    [ProducesResponseType(500)]
    public async Task<ActionResult<CompanyUserDetailsDto>> GetCompanyUserDetailsByUserId(int userId)
    {
        try
        {
            int? currentCompanyId = GetCompanyIdFromClaims();
            if (!currentCompanyId.HasValue) return Unauthorized("Company ID claim missing or invalid.");

            // Log the company ID and user ID being used for debugging
            Console.WriteLine($"GetCompanyUserDetailsByUserId: Filtering by CompanyId: {currentCompanyId.Value} and UserId: {userId}");

            // Find the CompanyUser record using the UserId from the route AND the CompanyId from the token
            var companyUser = await _context.CompanyUsers
                .Include(cu => cu.User)
                .Include(cu => cu.Department)
                .Where(cu => cu.UserId == userId && !(cu.IsDeleted ?? false)) // Filter by UserId from route
                .Where(cu => cu.CompanyId == currentCompanyId.Value) // Apply company filter from token
                .Select(cu => new CompanyUserDetailsDto
                {
                    // Map CompanyUser properties
                    UserId = cu.UserId, // Now the main identifier for the DTO
                    CompanyEmployeeId = cu.EmployeeId, // Renamed for clarity, represents employee_id from company_users table
                    RoleId = cu.RoleId,
                    CompanyId = cu.CompanyId,
                    DepartmentId = cu.DepartmentId,
                    IsActive = cu.IsActive,
                    EmploymentType = cu.EmploymentType,
                    Address = cu.Address,
                    AddressLine1 = cu.AddressLine1,
                    City = cu.City,
                    State = cu.State,
                    PostalCode = cu.PostalCode,
                    Country = cu.Country, // Added
                    Gender = cu.Gender,
                    BloodGroup = cu.BloodGroup,
                    PhoneNumber = cu.PhoneNumber,
                    DateOfBirth = cu.DateOfBirth,
                    MaritalStatus = cu.MaritalStatus,
                    JoiningDate = cu.JoiningDate,
                    EmployeeCode = cu.EmployeeCode,
                    ProfilePhotoUrl = cu.ProfilePhotoUrl,
                    Description = cu.Description,
                    IsDeleted = cu.IsDeleted,
                    CreatedAt = cu.CreatedAt,
                    CreatedBy = cu.CreatedBy,
                    UpdatedAt = cu.UpdatedAt,
                    UpdatedBy = cu.UpdatedBy,
                    CheckAdmin = cu.CheckAdmin,
                    FirstName = cu.FirstName,
                    LastName = cu.LastName,
                    AnniversaryDate = cu.AnniversaryDate,
                    ReportToUserId = cu.ReportToUserId,

                    // Map Emergency Contact Details
                    EmergencyContactName = cu.EmergencyContactName, // Added
                    EmergencyContactRelation = cu.EmergencyContactRelation, // Added
                    EmergencyContactNumber = cu.EmergencyContactNumber, // Added

                    // Map User properties
                    UserEmail = cu.User != null ? cu.User.Email : string.Empty,
                    // UserPasswordHash and RefreshToken fields removed for security
                    UserIsActive = cu.User != null ? cu.User.IsActive : false,
                    UserIsDeleted = cu.User != null ? cu.User.IsDeleted : false,
                    UserCreatedAt = cu.User != null ? cu.User.CreatedAt : (DateTime?)null,
                    UserUpdatedAt = cu.User != null ? cu.User.UpdatedAt : (DateTime?)null,
                    UserUpdatedBy = cu.User != null ? cu.User.UpdatedBy : (int?)null,
                    UserRoleId = cu.User != null ? cu.User.RoleId : (int?)null,
                    UserCompanyId = cu.User != null ? cu.User.CompanyId : (int?)null,

                    // Map DepartmentName
                    DepartmentName = cu.Department != null ? cu.Department.DepartmentName : null
                })
                .FirstOrDefaultAsync(); // Get the first matching user or null

            if (companyUser == null)
            {
                // This message explicitly logs why a 404 is returned, including CompanyId
                Console.WriteLine($"Company user with UserId {userId} not found for CompanyId {currentCompanyId.Value} (or is deleted).");
                return NotFound($"Company user with UserId {userId} not found."); // Return 404 if not found
            }

            return Ok(companyUser); // Return the DTO with a 200 OK status
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error retrieving company user with UserId {userId}: {ex.Message}");
            return StatusCode(500, "An error occurred while retrieving company user details.");
        }
    }

    // --- CREATE Operation ---

    /// <summary>
    /// Creates a new User record (where UserId is auto-incremented) and then
    /// creates a new CompanyUser record linked to the newly created User.
    /// CompanyId for both User and CompanyUser will be extracted from the authenticated user's token.
    /// </summary>
    /// <param name="createDto">The DTO containing information for the new User and CompanyUser.</param>
    /// <returns>An ActionResult representing the result of the creation, typically with the new resource's URI.</returns>
    [HttpPost] // Responds to POST /api/CompanyUsers
    [ProducesResponseType(typeof(CompanyUserDetailsDto), 201)] // Created
    [ProducesResponseType(400)] // Bad Request (e.g., validation errors)
    [ProducesResponseType(401)] // Unauthorized (if claims are missing)
    [ProducesResponseType(409)] // Conflict (e.g., duplicate user email)
    [ProducesResponseType(500)]
    public async Task<ActionResult<CompanyUserDetailsDto>> CreateCompanyUser([FromBody] CompanyUserCreateDto createDto)
    {
        // Model validation automatically handled by [ApiController] attribute.
        // If ModelState is invalid, a 400 Bad Request is returned automatically.

        // Extract CompanyId from the authenticated user's token.
        int? companyIdFromToken = GetCompanyIdFromClaims();
        if (!companyIdFromToken.HasValue)
        {
            return Unauthorized("Company ID claim missing or invalid in token.");
        }

        // Extract current user's ID for CreatedBy from the authenticated user's token.
        int? createdByUserId = GetCurrentUserIdFromClaims();
        if (!createdByUserId.HasValue)
        {
            return Unauthorized("User ID claim missing or invalid in token for the authenticated user.");
        }

        // Log the company ID and current user ID being used for debugging
        Console.WriteLine($"CreateCompanyUser: Using CompanyId from token: {companyIdFromToken.Value}, CreatedBy UserId: {createdByUserId.Value}");


        // Start a database transaction for atomicity (ensures both User and CompanyUser are created or neither are)
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 1. Create the new User record first
            // Check if UserEmail already exists to prevent duplicates
            if (await _context.Users.AnyAsync(u => u.Email == createDto.UserEmail && !(u.IsDeleted)))
            {
                ModelState.AddModelError("UserEmail", "A user with this email already exists.");
                return Conflict(ModelState); // 409 Conflict
            }

            var newUser = new User
            {
                Email = createDto.UserEmail,
                PasswordHash = createDto.UserPasswordHash, // Ensure this is hashed securely before saving
                IsActive = true, // Default to active on user creation
                IsDeleted = false, // Always false on creation
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow, // Set initial update time
                UpdatedBy = createdByUserId, // Set initial updated by
                RoleId = createDto.UserRoleId, // Optional role for the User itself
                RefreshToken = null, // Set during login process
                RefreshTokenExpiryTime = null, // Set during login process
                CompanyId = companyIdFromToken // Assign company ID from token
            };

            _context.Users.Add(newUser);
            try
            {
                await _context.SaveChangesAsync(); // This will populate newUser.UserId with the auto-incremented value
            }
            catch (DbUpdateException userEx)
            {
                await transaction.RollbackAsync(); // Rollback user creation
                Console.WriteLine($"Database error during User creation: {userEx.Message}");
                return StatusCode(500, $"Failed to create new user (email: {createDto.UserEmail}). Please check if UserRoleId (if provided) exists and CompanyId ({companyIdFromToken}) from token refers to an existing company. Inner Error: {userEx.InnerException?.Message ?? userEx.Message}");
            }


            // 2. Create the CompanyUser record using the newly created User's UserId
            // Generate a unique EmployeeCode since it's no longer provided in DTO
            string newEmployeeCode = $"EMP_{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";
            // In a real app, you might want a more controlled sequence or format for employee codes.
            // Also, check for uniqueness in case of GUID collision (highly unlikely but possible)
            while (await _context.CompanyUsers.AnyAsync(cu => cu.EmployeeCode == newEmployeeCode))
            {
                newEmployeeCode = $"EMP_{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";
            }

            var companyUser = new CompanyUser
            {
                UserId = newUser.UserId, // Use the auto-incremented UserId from the newly created User
                RoleId = createDto.RoleId, // This is the CompanyUser's role
                CompanyId = companyIdFromToken.Value, // Use CompanyId from token
                DepartmentId = createDto.DepartmentId,
                IsActive = true, // Set explicitly, as it's not from DTO now
                EmploymentType = createDto.EmploymentType,
                Address = createDto.Address,
                AddressLine1 = createDto.AddressLine1,
                City = createDto.City,
                State = createDto.State,
                PostalCode = createDto.PostalCode,
                Country = createDto.Country, // Mapped from DTO
                Gender = createDto.Gender,
                BloodGroup = createDto.BloodGroup,
                PhoneNumber = createDto.PhoneNumber,
                DateOfBirth = createDto.DateOfBirth,
                MaritalStatus = createDto.MaritalStatus,
                JoiningDate = DateOnly.FromDateTime(DateTime.UtcNow), // Set explicitly, as it's not from DTO now
                EmployeeCode = newEmployeeCode, // Assigned the generated code
                ProfilePhotoUrl = createDto.ProfilePhotoUrl,
                Description = createDto.Description,

                // Emergency Contact Details
                //EmergencyContactName = createDto.EmergencyContactName, // Mapped from DTO
                //EmergencyContactRelation = createDto.EmergencyContactRelation, // Mapped from DTO
                //EmergencyContactNumber = createDto.EmergencyContactNumber, // Mapped from DTO

                CheckAdmin = createDto.CheckAdmin,
                FirstName = createDto.FirstName,
                LastName = createDto.LastName,
                AnniversaryDate = createDto.AnniversaryDate,
                ReportToUserId = createDto.ReportToUserId,

                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = createdByUserId.Value // User ID of who created this record
            };

            _context.CompanyUsers.Add(companyUser);
            try
            {
                await _context.SaveChangesAsync(); // This will populate companyUser.EmployeeId
            }
            catch (DbUpdateException companyUserEx)
            {
                await transaction.RollbackAsync(); // Rollback both user and company user creation
                Console.WriteLine($"Database error during CompanyUser creation: {companyUserEx.Message}");
                return StatusCode(500, $"Failed to create company user. Please check if RoleId ({createDto.RoleId}), DepartmentId (if provided: {createDto.DepartmentId}) exist. Inner Error: {companyUserEx.InnerException?.Message ?? companyUserEx.Message}");
            }

            await transaction.CommitAsync(); // Commit the transaction if both saves are successful

            // After saving, reload with related data to return the full DTO
            var createdUserDetailDto = await _context.CompanyUsers
                .Include(cu => cu.User)
                .Include(cu => cu.Department)
                .Where(cu => cu.EmployeeId == companyUser.EmployeeId)
                .Select(cu => new CompanyUserDetailsDto
                {
                    UserId = cu.UserId,
                    CompanyEmployeeId = cu.EmployeeId,
                    RoleId = cu.RoleId,
                    CompanyId = cu.CompanyId,
                    DepartmentId = cu.DepartmentId,
                    IsActive = cu.IsActive,
                    EmploymentType = cu.EmploymentType,
                    Address = cu.Address,
                    AddressLine1 = cu.AddressLine1,
                    City = cu.City,
                    State = cu.State,
                    PostalCode = cu.PostalCode,
                    Country = cu.Country, // Mapped
                    Gender = cu.Gender,
                    BloodGroup = cu.BloodGroup,
                    PhoneNumber = cu.PhoneNumber,
                    DateOfBirth = cu.DateOfBirth,
                    MaritalStatus = cu.MaritalStatus,
                    JoiningDate = cu.JoiningDate,
                    EmployeeCode = cu.EmployeeCode,
                    ProfilePhotoUrl = cu.ProfilePhotoUrl,
                    Description = cu.Description,
                    IsDeleted = cu.IsDeleted,
                    CreatedAt = cu.CreatedAt,
                    CreatedBy = cu.CreatedBy,
                    UpdatedAt = cu.UpdatedAt,
                    UpdatedBy = cu.UpdatedBy,
                    CheckAdmin = cu.CheckAdmin,
                    FirstName = cu.FirstName,
                    LastName = cu.LastName,
                    AnniversaryDate = cu.AnniversaryDate,
                    ReportToUserId = cu.ReportToUserId,

                    // Emergency Contact Details
                    EmergencyContactName = cu.EmergencyContactName, // Mapped
                    EmergencyContactRelation = cu.EmergencyContactRelation, // Mapped
                    EmergencyContactNumber = cu.EmergencyContactNumber, // Mapped

                    UserEmail = cu.User != null ? cu.User.Email : string.Empty,
                    // UserPasswordHash and RefreshToken fields removed for security
                    UserIsActive = cu.User != null ? cu.User.IsActive : false,
                    UserIsDeleted = cu.User != null ? cu.User.IsDeleted : false,
                    UserCreatedAt = cu.User != null ? cu.User.CreatedAt : (DateTime?)null,
                    UserUpdatedAt = cu.User != null ? cu.User.UpdatedAt : (DateTime?)null,
                    UserUpdatedBy = cu.User != null ? cu.User.UpdatedBy : (int?)null,
                    UserRoleId = cu.User != null ? cu.User.RoleId : (int?)null,
                    UserCompanyId = cu.User != null ? cu.User.CompanyId : (int?)null,

                    DepartmentName = cu.Department != null ? cu.Department.DepartmentName : null
                })
                .FirstOrDefaultAsync();

            if (createdUserDetailDto == null)
            {
                return StatusCode(500, "Failed to retrieve the newly created company user details after creation.");
            }

            return CreatedAtAction(nameof(GetCompanyUserDetailsByUserId), new { userId = createdUserDetailDto.UserId }, createdUserDetailDto);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Console.WriteLine($"An unexpected error occurred during company user creation: {ex.Message}");
            return StatusCode(500, $"An unexpected error occurred while creating the company user: {ex.Message}");
        }
    }

    // --- UPDATE Operation ---

    /// <summary>
    /// Updates an existing CompanyUser record identified by UserId.
    /// This endpoint filters data by the CompanyId present in the authenticated user's token.
    /// It can also update the associated User's email.
    /// </summary>
    /// <param name="userId">The UserId of the CompanyUser to update.</param>
    /// <param name="updateDto">The DTO containing the updated information.</param>
    /// <returns>An ActionResult indicating the result of the update (NoContent, NotFound, BadRequest, or InternalServerError).</returns>
    [HttpPut("user/{userId}")] // Route changed to use 'user/{userId}'
    [ProducesResponseType(204)] // No Content
    [ProducesResponseType(400)] // Bad Request
    [ProducesResponseType(404)] // Not Found if user not found for specified UserId and CompanyId
    [ProducesResponseType(401)] // Unauthorized if Company ID claim is missing
    [ProducesResponseType(409)] // Conflict (e.g., duplicate user email during update)
    [ProducesResponseType(500)]
    public async Task<IActionResult> UpdateCompanyUser(int userId, [FromBody] CompanyUserUpdateDto updateDto)
    {
        try
        {
            int? currentCompanyId = GetCompanyIdFromClaims();
            if (!currentCompanyId.HasValue) return Unauthorized("Company ID claim missing or invalid.");

            // Log the company ID and user ID being used for debugging
            Console.WriteLine($"UpdateCompanyUser: Filtering by CompanyId: {currentCompanyId.Value} and UserId: {userId}");

            // Find the CompanyUser record, including the related User record for email updates
            var companyUser = await _context.CompanyUsers
                                            .Include(cu => cu.User) // Eager load User for email update
                                            .FirstOrDefaultAsync(cu => cu.UserId == userId && cu.CompanyId == currentCompanyId.Value && !(cu.IsDeleted ?? false));

            if (companyUser == null)
            {
                Console.WriteLine($"Company user with UserId {userId} not found for CompanyId {currentCompanyId.Value} (or is deleted).");
                return NotFound($"Company user with UserId {userId} not found or is deleted within your company.");
            }

            int? updatedByUserId = GetCurrentUserIdFromClaims();
            if (!updatedByUserId.HasValue)
            {
                return Unauthorized("User ID claim missing or invalid in token.");
            }

            // Update associated User's email if provided and different
            if (!string.IsNullOrWhiteSpace(updateDto.UserEmail) && companyUser.User != null && companyUser.User.Email != updateDto.UserEmail)
            {
                // Check for duplicate email before updating
                if (await _context.Users.AnyAsync(u => u.Email == updateDto.UserEmail && u.UserId != userId && !(u.IsDeleted)))
                {
                    ModelState.AddModelError("UserEmail", "Another user with this email already exists.");
                    return Conflict(ModelState); // 409 Conflict
                }
                companyUser.User.Email = updateDto.UserEmail;
                companyUser.User.UpdatedAt = DateTime.UtcNow;
                companyUser.User.UpdatedBy = updatedByUserId;
            }

            // Update CompanyUser properties from DTO
            companyUser.RoleId = updateDto.RoleId; // RoleId is required in DTO for updates
            companyUser.DepartmentId = updateDto.DepartmentId;
            // IsActive is not updated from DTO; it retains its current value
            companyUser.EmploymentType = updateDto.EmploymentType;
            companyUser.Address = updateDto.Address;
            companyUser.AddressLine1 = updateDto.AddressLine1;
            companyUser.City = updateDto.City;
            companyUser.State = updateDto.State;
            companyUser.PostalCode = updateDto.PostalCode;
            companyUser.Country = updateDto.Country; // Mapped from DTO
            companyUser.Gender = updateDto.Gender;
            companyUser.BloodGroup = updateDto.BloodGroup;
            companyUser.PhoneNumber = updateDto.PhoneNumber;
            companyUser.DateOfBirth = updateDto.DateOfBirth;
            companyUser.MaritalStatus = updateDto.MaritalStatus;
            // JoiningDate is not updated from DTO; it retains its current value
            // EmployeeCode is not updated from DTO; it retains its current value
            companyUser.ProfilePhotoUrl = updateDto.ProfilePhotoUrl;
            companyUser.Description = updateDto.Description;

            // Emergency Contact Details
            //companyUser.EmergencyContactName = updateDto.EmergencyContactName; // Mapped from DTO
            //companyUser.EmergencyContactRelation = updateDto.EmergencyContactRelation; // Mapped from DTO
            //companyUser.EmergencyContactNumber = updateDto.EmergencyContactNumber; // Mapped from DTO

            companyUser.CheckAdmin = updateDto.CheckAdmin;
            companyUser.FirstName = updateDto.FirstName;
            companyUser.LastName = updateDto.LastName;
            companyUser.AnniversaryDate = updateDto.AnniversaryDate;
            companyUser.ReportToUserId = updateDto.ReportToUserId;

            companyUser.UpdatedAt = DateTime.UtcNow;
            companyUser.UpdatedBy = updatedByUserId.Value; // Use actual user ID from token

            await _context.SaveChangesAsync(); // Save changes to the database

            return NoContent(); // 204 No Content, indicating successful update with no content to return
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await _context.CompanyUsers.AnyAsync(e => e.UserId == userId)) // Check by UserId now
            {
                return NotFound($"Company user with UserId {userId} not found.");
            }
            throw; // Re-throw the exception for other error handling
        }
        catch (DbUpdateException ex)
        {
            Console.WriteLine($"Database error during company user update: {ex.Message}");
            return StatusCode(500, "A database error occurred while updating the company user. Please check related IDs (UserId, RoleId, CompanyId, DepartmentId).");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating company user with UserId {userId}: {ex.Message}");
            return StatusCode(500, "An error occurred while updating the company user.");
        }
    }

    // --- DELETE Operation (Soft Delete) ---

    /// <summary>
    /// Soft deletes a CompanyUser identified by UserId by setting its IsDeleted flag to true.
    /// This endpoint filters data by the CompanyId present in the authenticated user's token.
    /// </summary>
    /// <param name="userId">The UserId of the CompanyUser to soft delete.</param>
    /// <returns>An ActionResult indicating the result (NoContent, NotFound, or InternalServerError).</returns>
    [HttpDelete("user/{userId}")] // Route changed to use 'user/{userId}'
    [ProducesResponseType(204)] // No Content
    [ProducesResponseType(404)] // Not Found if user not found for specified UserId and CompanyId
    [ProducesResponseType(401)] // Unauthorized if Company ID claim is missing
    [ProducesResponseType(500)]
    public async Task<IActionResult> SoftDeleteCompanyUser(int userId)
    {
        try
        {
            int? currentCompanyId = GetCompanyIdFromClaims();
            if (!currentCompanyId.HasValue) return Unauthorized("Company ID claim missing or invalid.");

            // Log the company ID and user ID being used for debugging
            Console.WriteLine($"SoftDeleteCompanyUser: Filtering by CompanyId: {currentCompanyId.Value} and UserId: {userId}");

            // Find the CompanyUser record using the UserId from the route AND the CompanyId from the token
            var companyUser = await _context.CompanyUsers
                                            .FirstOrDefaultAsync(cu => cu.UserId == userId && cu.CompanyId == currentCompanyId.Value && !(cu.IsDeleted ?? false));

            if (companyUser == null)
            {
                Console.WriteLine($"Company user with UserId {userId} not found for CompanyId {currentCompanyId.Value} (or is already deleted).");
                return NotFound($"Company user with UserId {userId} not found or already deleted within your company.");
            }

            // Extract current user's ID for UpdatedBy from the authenticated user's token.
            int? updatedByUserId = GetCurrentUserIdFromClaims();
            if (!updatedByUserId.HasValue)
            {
                return Unauthorized("User ID claim missing or invalid in token.");
            }

            companyUser.IsDeleted = true; // Perform soft delete
            companyUser.UpdatedAt = DateTime.UtcNow;
            companyUser.UpdatedBy = updatedByUserId.Value; // Use actual user ID from token

            await _context.SaveChangesAsync();

            return NoContent(); // 204 No Content, indicating successful deletion
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error soft deleting company user with UserId {userId}: {ex.Message}");
            return StatusCode(500, "An error occurred while soft deleting the company user.");
        }
    }

    [HttpPost("upload-profile-photo/{userId}")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> UploadProfilePhoto(int userId, IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file provided.");
        }

        try
        {
            var url = await _uploadHandler.UploadProfilePhotoAsync(userId, file);
            return Ok(new { profilePhotoUrl = url });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error uploading profile photo for UserId {userId}: {ex.Message}");
            return StatusCode(500, "An error occurred while uploading the profile photo.");
        }
    }
}