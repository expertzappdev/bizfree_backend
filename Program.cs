using BizfreeApp.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims; // Required for ClaimTypes and custom claims
using BizfreeApp.Services;
using Microsoft.Extensions.FileProviders; // Required for PhysicalFileProvider
using System.IO; // Required for Path.Combine

var builder = WebApplication.CreateBuilder(args);

// Add DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseNpgsql(connectionString);
});

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy.AllowAnyOrigin() // In production, replace with specific origins: .WithOrigins("http://localhost:4200", "https://yourfrontend.com")
                        .AllowAnyMethod()
                        .AllowAnyHeader());
});

// Add controllers
builder.Services.AddControllers().AddJsonOptions(options =>
{
          options.JsonSerializerOptions.MaxDepth = 256; // You might want to increase this as well if cycles are deep
}); 
builder.Services.AddScoped<IUploadHandler, UploadHandler>();

// Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
// Ensure the Key exists and is long enough for HS256 (minimum 32 bytes for SHA256)
var key = Encoding.ASCII.GetBytes(jwtSettings["Key"] ?? throw new InvalidOperationException("JWT Key not found in configuration."));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"],

        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],

        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),

        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero // optional: no extra tolerance time
    };
});

// Configure Authorization services and policies
builder.Services.AddAuthorization(options =>
{
    // Policy for general authenticated users (redundant but good for clarity)
    options.AddPolicy("AuthenticatedUser", policy => policy.RequireAuthenticatedUser());

    // Policy requiring the 'Admin' role (based on the standard ClaimTypes.Role)
    options.AddPolicy("RequireAdminRole", policy => policy.RequireRole("Admin"));

    // Policy requiring a specific custom RoleId (e.g., RoleId "1" for an Admin role)
    options.AddPolicy("RequireSpecificRoleId", policy =>
        policy.RequireClaim("RoleId", "1")); // Assuming "1" is the ID for the admin role in your DB

    // Policy requiring a specific custom CompanyId (e.g., CompanyId "100")
    options.AddPolicy("RequireSpecificCompanyAccess", policy =>
        policy.RequireClaim("CompanyId", "100"));

    // Combined policy: Requires both a CompanyId and RoleId "1" (e.g., Company Admin)
    options.AddPolicy("CompanyAdminAccess", policy =>
        policy.RequireClaim("CompanyId") // Must have a CompanyId claim
              .RequireClaim("RoleId", "1")); // And the RoleId must be "1"

    // Example of a more complex policy using RequireAssertion for dynamic logic
    options.AddPolicy("CanViewSensitiveData", policy =>
        policy.RequireAuthenticatedUser()
              .RequireAssertion(context =>
              {
                  // Check if the user has both CompanyId and RoleId claims
                  var companyIdClaim = context.User.FindFirst("CompanyId");
                  var roleIdClaim = context.User.FindFirst("RoleId");

                  if (companyIdClaim == null || roleIdClaim == null ||
                      !int.TryParse(companyIdClaim.Value, out int companyId) ||
                      !int.TryParse(roleIdClaim.Value, out int roleId))
                  {
                      return false; // Claims missing or invalid
                  }

                  // Example logic: Only allow if companyId is 100 AND roleId is 1 or 2
                  return companyId == 100 && (roleId == 1 || roleId == 2);
              })
    );
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage(); // Good for dev, helps see detailed errors
}
else
{
    app.UseHsts();
}

app.UseCors("AllowAll");

app.UseHttpsRedirection();

var uploadsPath = Path.Combine(builder.Environment.ContentRootPath, "Uploads");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/Uploads"
});

app.UseRouting(); // This should come after UseStaticFiles if static files are not routed by controllers.

// **IMPORTANT: Add Authentication middleware before Authorization**
app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

// Explicitly configure Kestrel to listen on port 80 if running in Docker
//var port = Environment.GetEnvironmentVariable("PORT") ?? "80";
//app.Urls.Add($"http://*:{port}");

app.Run();