using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BizfreeApp.Models.DTOs;
using BizfreeApp.Models;
using Microsoft.AspNetCore.Authorization;
using System.Linq.Expressions;
using System;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using BizfreeApp.Services; // Add this using directive for IUploadHandler
using System.Collections.Generic; // Added for IEnumerable<object> in GetProjectMembers

namespace BizfreeApp.Controllers
{
    [Route("api/[controller]")]
    [Authorize]
    [ApiController]
    public class ProjectController : ControllerBase
    {
        private readonly Data.ApplicationDbContext _context;
        private readonly ILogger<ProjectController> _logger;
        private readonly IUploadHandler _uploadHandler; // Inject IUploadHandler

        public ProjectController(Data.ApplicationDbContext context, ILogger<ProjectController> logger, IUploadHandler uploadHandler)
        {
            _context = context;
            _logger = logger;
            _uploadHandler = uploadHandler; // Initialize IUploadHandler
        }

        // Helper method for consistent success responses
        private ActionResult<ApiResponse<T>> Success<T>(string message, T? data, int statusCode = StatusCodes.Status200OK)
        {
            return Ok(new ApiResponse<T>(message, "Success", statusCode, data));
        }

        // Helper method for consistent error responses
        private ActionResult<ApiResponse<T>> Error<T>(string message, int statusCode, object? errorDetails = null)
        {
            _logger.LogError("API Error: {Message} - Status Code: {StatusCode} - Details: {Details}", message, statusCode, errorDetails);
            return StatusCode(statusCode, new ApiResponse<T>(message, "Error", statusCode, data: default(T)));
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int parsedUserId))
            {
                return parsedUserId;
            }
            _logger.LogWarning("UserId claim not found or could not be parsed.");
            return null;
        }

        private int? GetCurrentUserCompanyId()
        {
            var companyIdClaim = User.Claims.FirstOrDefault(c => c.Type == "CompanyId");
            if (companyIdClaim != null && int.TryParse(companyIdClaim.Value, out int parsedCompanyId))
            {
                return parsedCompanyId;
            }
            _logger.LogWarning("CompanyId claim not found or could not be parsed.");
            return null;
        }

        private int? GetCurrentUserRoleId()
        {
            var roleIdClaim = User.Claims.FirstOrDefault(c => c.Type == "RoleId");
            if (roleIdClaim != null && int.TryParse(roleIdClaim.Value, out int parsedRoleId))
            {
                return parsedRoleId;
            }
            _logger.LogWarning("RoleId claim not found or could not be parsed.");
            return null;
        }

        // GET: api/Project
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<object>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<PagedResult<object>>>> GetProjects(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 5,
            [FromQuery] string sortBy = "DueDate",
            [FromQuery] string sortOrder = "desc",
            [FromQuery] string? search = null,
            [FromQuery] string? status = null,
            [FromQuery] DateOnly? startDateFrom = null,
            [FromQuery] DateOnly? startDateTo = null,
            [FromQuery] DateOnly? endDateFrom = null,
            [FromQuery] DateOnly? endDateTo = null,
            [FromQuery] int? companyIdFilter = null
            )
        {
            try
            {
                _logger.LogInformation("Fetching projects based on user role with pagination and filtering.");

                var currentUserId = GetCurrentUserId();
                var currentCompanyId = GetCurrentUserCompanyId();
                var currentRoleId = GetCurrentUserRoleId();

                if (!currentUserId.HasValue || !currentCompanyId.HasValue || !currentRoleId.HasValue)
                {
                    return Error<PagedResult<object>>("Authentication information (UserId, CompanyId, RoleId) is missing.", StatusCodes.Status401Unauthorized);
                }

                const int SuperAdminRoleId = 1;
                const int CompanyAdminRoleId = 2;
                const int EmployeeRoleId = 3;
                const int DepartmentHeadRoleId = 4;

                IQueryable<Project> query = _context.Projects
                    .Where(p => p.IsDeleted != true);

                if (currentRoleId == SuperAdminRoleId)
                {
                    _logger.LogInformation($"Super Admin (User ID: {currentUserId}, Company ID: {currentCompanyId}) is fetching all projects.");
                    if (companyIdFilter.HasValue)
                    {
                        query = query.Where(p => p.CompanyId == companyIdFilter.Value);
                    }
                }
                else if (currentRoleId == CompanyAdminRoleId || currentRoleId == DepartmentHeadRoleId)
                {
                    _logger.LogInformation($"Company Admin/Department Head (User ID: {currentUserId}, Company ID: {currentCompanyId}) is fetching projects for their company.");
                    query = query.Where(p => p.CompanyId == currentCompanyId.Value);
                    if (companyIdFilter.HasValue && companyIdFilter.Value != currentCompanyId.Value)
                    {
                        return Error<PagedResult<object>>("You do not have permission to filter projects for other companies.", StatusCodes.Status403Forbidden);
                    }
                }
                else if (currentRoleId == EmployeeRoleId)
                {
                    _logger.LogInformation($"Employee (User ID: {currentUserId}, Company ID: {currentCompanyId}) is fetching projects they are assigned to.");
                    query = query.Where(p => p.ProjectMembers.Any(pm => pm.UserId == currentUserId.Value));
                }
                else
                {
                    _logger.LogWarning($"Unknown Role ID: {currentRoleId} for User ID: {currentUserId}. Access denied.");
                    return Error<PagedResult<object>>("Access denied due to unknown role.", StatusCodes.Status403Forbidden);
                }

                // Apply Filtering
                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(p => p.Name!.Contains(search) || (p.Description != null && p.Description.Contains(search)));
                }

                if (!string.IsNullOrWhiteSpace(status))
                {
                    query = query.Where(p => p.Status == status);
                }

                if (startDateFrom.HasValue)
                {
                    query = query.Where(p => p.StartDate.HasValue && p.StartDate.Value >= startDateFrom.Value);
                }

                if (startDateTo.HasValue)
                {
                    query = query.Where(p => p.StartDate.HasValue && p.StartDate.Value <= startDateTo.Value);
                }

                if (endDateFrom.HasValue)
                {
                    query = query.Where(p => p.EndDate.HasValue && p.EndDate.Value >= endDateFrom.Value);
                }

                if (endDateTo.HasValue)
                {
                    query = query.Where(p => p.EndDate.HasValue && p.EndDate.Value <= endDateTo.Value);
                }

                // Get total count BEFORE pagination
                var totalProjects = await query.CountAsync();

                // Apply Sorting
                var projectPropertyMap = new Dictionary<string, Expression<Func<Project, object>>>
                {
                    { "projectid", p => p.ProjectId },
                    { "name", p => p.Name! },
                    { "status", p => p.Status! },
                    { "startdate", p => p.StartDate! },
                    { "enddate", p => p.EndDate! },
                    { "createdat", p => p.CreatedAt },
                    { "companyid", p => p.CompanyId! },
                    { "companyname", p => p.Company!.CompanyName! }
                };

                if (sortBy.ToLower() == "companyname")
                {
                    query = query.Include(p => p.Company);
                }

                if (projectPropertyMap.TryGetValue(sortBy.ToLower(), out var sortExpression))
                {
                    if (sortOrder.ToLower() == "desc")
                    {
                        query = query.OrderByDescending(sortExpression);
                    }
                    else
                    {
                        query = query.OrderBy(sortExpression);
                    }
                }
                else
                {
                    query = query.OrderByDescending(p => p.CreatedAt);
                }

                // Apply Pagination and Projection
                var items = await query
                    // Existing Includes
                    .Include(p => p.ProjectMembers).ThenInclude(pm => pm.User)
                        // NEW: Include CompanyUser through User for ProfilePhotoUrl
                        .ThenInclude(u => u.CompanyUserUsers)
                    .Include(p => p.TaskLists).ThenInclude(tl => tl.Tasks).ThenInclude(t => t.AssignedToNavigation)
                        // NEW: Include CompanyUser for AssignedToNavigation
                        .ThenInclude(u => u.CompanyUserUsers)
                    // NEW: Include ProjectDocuments
                    .Include(p => p.ProjectDocuments)
                        .ThenInclude(pd => pd.UploadedByUser)
                            .ThenInclude(u => u.CompanyUserUsers)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(p => new
                    {
                        p.ProjectId,
                        p.Name,
                        p.Description,
                        p.Status,
                        p.StartDate,
                        p.EndDate,
                        p.IsActive,
                        p.CreatedAt,
                        p.UpdatedAt,
                        p.Progression,
                        Company = p.Company != null ? new
                        {
                            p.Company.CompanyId,
                            CompanyName = p.Company.CompanyName
                        } : null,
                        CreatedBy = p.CreatedByNavigation != null ? new
                        {
                            p.CreatedByNavigation.UserId,
                            p.CreatedByNavigation.Email
                        } : null,
                        UpdatedBy = p.UpdatedByNavigation != null ? new
                        {
                            p.UpdatedByNavigation.UserId,
                            p.UpdatedByNavigation.Email
                        } : null,
                        ProjectMembers = p.ProjectMembers.Select(pm => new
                        {
                            pm.Id,
                            pm.UserId,
                            pm.JoinedAt,
                            User = pm.User != null ? new
                            {
                                pm.User.UserId,
                                pm.User.Email,
                                // Safely get ProfilePhotoUrl, FirstName, LastName from the first CompanyUser record
                                // This assumes a user typically has one relevant CompanyUser record for the current context.
                                ProfilePhotoUrl = pm.User.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId) != null ?
                                                    pm.User.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId).ProfilePhotoUrl : null,
                                FirstName = pm.User.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId) != null ?
                                                pm.User.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId).FirstName : null,
                                LastName = pm.User.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId) != null ?
                                                pm.User.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId).LastName : null
                            } : null
                        }).ToList(),
                        TaskLists = p.TaskLists
                            .Where(tl => tl.IsDeleted != true)
                            .OrderBy(tl => tl.ListOrder)
                            .Select(tl => new
                            {
                                tl.TaskListId,
                                tl.ListName,
                                tl.Description,
                                tl.ListOrder,
                                tl.Status,
                                tl.IsActive,
                                tl.CreatedAt,
                                tl.UpdatedAt,
                                Tasks = tl.Tasks
                                    .Where(t => t.IsDeleted != true)
                                    .OrderBy(t => t.TaskOrder)
                                    .Select(t => new
                                    {
                                        t.TaskId,
                                        t.Title,
                                        t.Description,
                                        t.DueDate,
                                        t.CompanyId,
                                        t.TaskListId,
                                        t.EstimatedHours,
                                        t.ActualHours,
                                        t.TaskOrder,
                                        t.IsActive,
                                        t.CreatedAt,
                                        AssignedTo = t.AssignedToNavigation != null ? new
                                        {
                                            t.AssignedToNavigation.UserId,
                                            t.AssignedToNavigation.Email,
                                            // Safely get ProfilePhotoUrl, FirstName, LastName for the AssignedTo user
                                            ProfilePhotoUrl = t.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == t.CompanyId) != null ?
                                                                t.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == t.CompanyId).ProfilePhotoUrl : null,
                                            FirstName = t.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == t.CompanyId) != null ?
                                                            t.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == t.CompanyId).FirstName : null,
                                            LastName = t.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == t.CompanyId) != null ?
                                                            t.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == t.CompanyId).LastName : null
                                        } : null,
                                        Progression = t.Progression
                                    })
                                    .ToList()
                            })
                            .ToList(),
                        // NEW: Include ProjectDocuments here
                        ProjectDocuments = p.ProjectDocuments
                            .Where(pd => pd.IsDeleted != true) // Filter out deleted documents
                            .Select(pd => new
                            {
                                pd.DocumentId,
                                pd.DocumentName,
                                pd.DocumentType,
                                pd.FilePath,
                                pd.FileSize,
                                pd.Description,
                                pd.Version,
                                pd.IsActive,
                                pd.CreatedAt,
                                UploadedBy = pd.UploadedByUser != null ? new
                                {
                                    pd.UploadedByUser.UserId,
                                    pd.UploadedByUser.Email,
                                    // Get FirstName, LastName, and ProfilePhotoUrl from CompanyUser for the uploader
                                    ProfilePhotoUrl = pd.UploadedByUser.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId) != null ?
                                                        pd.UploadedByUser.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId).ProfilePhotoUrl : null,
                                    FirstName = pd.UploadedByUser.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId) != null ?
                                                    pd.UploadedByUser.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId).FirstName : null,
                                    LastName = pd.UploadedByUser.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId) != null ?
                                                    pd.UploadedByUser.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId).LastName : null
                                } : null
                            })
                            .ToList()
                    })
                    .ToListAsync();

                var pagedResult = new PagedResult<object>
                {
                    Items = items,
                    TotalCount = totalProjects,
                    PageNumber = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalProjects / pageSize),
                    TotalTasks = items.Cast<dynamic>()
                                        .SelectMany(p => (IEnumerable<dynamic>)p.TaskLists)
                                        .SelectMany(tl => (IEnumerable<dynamic>)tl.Tasks)
                                        .Count()
                };

                _logger.LogInformation($"Successfully retrieved {items.Count()} projects (total: {totalProjects}) for User ID: {currentUserId} with Role ID: {currentRoleId}.");
                return Success("Projects retrieved successfully.", pagedResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching projects with role-based access, sorting, and filtering.");
                return Error<PagedResult<object>>("An error occurred while retrieving projects.", StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        // GET: api/Project/5
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<object>>> GetProject(int id)
        {
            try
            {
                _logger.LogInformation($"Fetching project with ID: {id} based on user role.");

                var currentUserId = GetCurrentUserId();
                var currentCompanyId = GetCurrentUserCompanyId();
                var currentRoleId = GetCurrentUserRoleId();

                if (!currentUserId.HasValue || !currentCompanyId.HasValue || !currentRoleId.HasValue)
                {
                    return Unauthorized(new ApiResponse<object>("Authentication information (UserId, CompanyId, RoleId) is missing.", "Error", 401));
                }

                const int SuperAdminRoleId = 1;
                const int CompanyAdminRoleId = 2;
                const int EmployeeRoleId = 3;
                const int DepartmentHeadRoleId = 4;

                IQueryable<Project> query = _context.Projects
                    .Include(p => p.Company)
                    .Include(p => p.CreatedByNavigation)
                    .Include(p => p.UpdatedByNavigation)
                    .Include(p => p.ProjectMembers)
                        .ThenInclude(pm => pm.User)
                            .ThenInclude(u => u.CompanyUserUsers)
                    .Include(p => p.TaskLists)
                        .ThenInclude(tl => tl.Tasks)
                            .ThenInclude(t => t.AssignedToNavigation)
                                .ThenInclude(u => u.CompanyUserUsers)
                    .Include(p => p.TaskLists)
                        .ThenInclude(tl => tl.Tasks)
                            .ThenInclude(t => t.Priority)
                    .Include(p => p.TaskLists)
                        .ThenInclude(tl => tl.Tasks)
                            .ThenInclude(t => t.StatusNavigation)
                    // NEW: Include ProjectDocuments and related User/CompanyUser data
                    .Include(p => p.ProjectDocuments)
                        .ThenInclude(pd => pd.UploadedByUser)
                            .ThenInclude(u => u.CompanyUserUsers)
                    .Where(p => p.ProjectId == id && p.IsDeleted != true);

                if (currentRoleId == SuperAdminRoleId)
                {
                    _logger.LogInformation($"Super Admin (User ID: {currentUserId}, Company ID: {currentCompanyId}) is fetching project ID: {id}.");
                }
                else if (currentRoleId == CompanyAdminRoleId)
                {
                    _logger.LogInformation($"Company Admin (User ID: {currentUserId}, Company ID: {currentCompanyId}) is fetching project ID: {id} within their company.");
                    query = query.Where(p => p.CompanyId == currentCompanyId.Value);
                }
                else if (currentRoleId == DepartmentHeadRoleId)
                {
                    _logger.LogInformation($"Department Head (User ID: {currentUserId}, Company ID: {currentCompanyId}) is fetching project ID: {id} within their company.");
                    query = query.Where(p => p.CompanyId == currentCompanyId.Value);
                }
                else if (currentRoleId == EmployeeRoleId)
                {
                    _logger.LogInformation($"Employee (User ID: {currentUserId}, Company ID: {currentCompanyId}) is fetching project ID: {id} they are assigned to.");
                    query = query.Where(p => p.ProjectMembers.Any(pm => pm.UserId == currentUserId.Value));
                }
                else
                {
                    _logger.LogWarning($"Unknown Role ID: {currentRoleId} for User ID: {currentUserId}. Access denied for project ID: {id}.");
                    return StatusCode(403, new ApiResponse<object>("Access denied due to unknown role.", "Error", 403));
                }

                var project = await query
                    .Select(p => new
                    {
                        p.ProjectId,
                        p.CompanyId,
                        p.Name,
                        p.Description,
                        p.Status,
                        p.StartDate,
                        p.EndDate,
                        p.IsActive,
                        p.CreatedAt,
                        p.UpdatedAt,
                        p.Progression,
                        Company = p.Company != null ? new
                        {
                            p.Company.CompanyId,
                            CompanyName = p.Company.CompanyName
                        } : null,
                        CreatedBy = p.CreatedByNavigation != null ? new
                        {
                            p.CreatedByNavigation.UserId,
                            p.CreatedByNavigation.Email
                        } : null,
                        UpdatedBy = p.UpdatedByNavigation != null ? new
                        {
                            p.UpdatedByNavigation.UserId,
                            p.UpdatedByNavigation.Email
                        } : null,
                        ProjectMembers = p.ProjectMembers.Select(pm => new
                        {
                            pm.Id,
                            pm.UserId,
                            pm.JoinedAt,
                            User = pm.User != null ? new
                            {
                                pm.User.UserId,
                                pm.User.Email,
                                // Safely get ProfilePhotoUrl, FirstName, LastName from the first CompanyUser record
                                ProfilePhotoUrl = pm.User.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId) != null ?
                                                    pm.User.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId).ProfilePhotoUrl : null,
                                FirstName = pm.User.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId) != null ?
                                                pm.User.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId).FirstName : null,
                                LastName = pm.User.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId) != null ?
                                                pm.User.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId).LastName : null
                            } : null
                        }).ToList(),
                        TaskLists = p.TaskLists
                            .Where(tl => tl.IsDeleted != true)
                            .OrderBy(tl => tl.ListOrder)
                            .Select(tl => new
                            {
                                tl.TaskListId,
                                tl.ListName,
                                tl.Description,
                                tl.ListOrder,
                                tl.Status,
                                tl.IsActive,
                                tl.CreatedAt,
                                tl.UpdatedAt,
                                Tasks = tl.Tasks
                                    .Where(t => t.IsDeleted != true)
                                    .OrderBy(t => t.TaskOrder)
                                    .Select(t => new
                                    {
                                        t.TaskId,
                                        t.Title,
                                        t.Description,
                                        t.DueDate,
                                        t.CompanyId,
                                        t.TaskListId,
                                        t.EstimatedHours,
                                        t.ActualHours,
                                        t.TaskOrder,
                                        t.IsActive,
                                        t.CreatedAt,
                                        AssignedTo = t.AssignedToNavigation != null ? new
                                        {
                                            t.AssignedToNavigation.UserId,
                                            t.AssignedToNavigation.Email,
                                            // Safely get ProfilePhotoUrl, FirstName, LastName for the AssignedTo user
                                            ProfilePhotoUrl = t.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == t.CompanyId) != null ?
                                                                t.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == t.CompanyId).ProfilePhotoUrl : null,
                                            FirstName = t.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == t.CompanyId) != null ?
                                                            t.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == t.CompanyId).FirstName : null,
                                            LastName = t.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == t.CompanyId) != null ?
                                                            t.AssignedToNavigation.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == t.CompanyId).LastName : null
                                        } : null,
                                        Progression = t.Progression
                                    })
                                    .ToList()
                            })
                            .ToList(),
                        // NEW: Include ProjectDocuments here
                        ProjectDocuments = p.ProjectDocuments
                            .Where(pd => pd.IsDeleted != true) // Filter out deleted documents
                            .Select(pd => new
                            {
                                pd.DocumentId,
                                pd.DocumentName,
                                pd.DocumentType,
                                pd.FilePath,
                                pd.FileSize,
                                pd.Description,
                                pd.Version,
                                pd.IsActive,
                                pd.CreatedAt,
                                UploadedBy = pd.UploadedByUser != null ? new
                                {
                                    pd.UploadedByUser.UserId,
                                    pd.UploadedByUser.Email,
                                    // Get FirstName, LastName, and ProfilePhotoUrl from CompanyUser for the uploader
                                    ProfilePhotoUrl = pd.UploadedByUser.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId) != null ?
                                                        pd.UploadedByUser.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId).ProfilePhotoUrl : null,
                                    FirstName = pd.UploadedByUser.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId) != null ?
                                                    pd.UploadedByUser.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId).FirstName : null,
                                    LastName = pd.UploadedByUser.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId) != null ?
                                                    pd.UploadedByUser.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == p.CompanyId).LastName : null
                                } : null
                            })
                            .ToList()
                    })
                    .FirstOrDefaultAsync();

                if (project == null)
                {
                    _logger.LogWarning($"Project with ID {id} not found or access denied for User ID: {currentUserId}.");
                    return NotFound(new ApiResponse<object>($"Project with ID {id} not found or you do not have permission to view it.", "Error", 404));
                }

                _logger.LogInformation($"Successfully retrieved project with ID: {id} for User ID: {currentUserId}.");
                return Ok(new ApiResponse<object>("Project retrieved successfully", "Success", 200, project));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error occurred while fetching project with ID: {id} with role-based access.");
                return StatusCode(500, new ApiResponse<object>("An error occurred while retrieving the project", "Error", 500, null));
            }
        }
  
// GET: api/Project/{id}/members
[HttpGet("{id}/members")]
        public async Task<ActionResult<ApiResponse<IEnumerable<object>>>> GetProjectMembers(int id)
        {
            try
            {
                _logger.LogInformation($"Fetching members for project ID: {id}.");

                var currentUserId = GetCurrentUserId();
                var currentCompanyId = GetCurrentUserCompanyId();
                var currentRoleId = GetCurrentUserRoleId();

                if (!currentUserId.HasValue || !currentCompanyId.HasValue || !currentRoleId.HasValue)
                {
                    return Error<IEnumerable<object>>("Authentication information (UserId, CompanyId, RoleId) is missing.", StatusCodes.Status401Unauthorized);
                }

                const int SuperAdminRoleId = 1;
                const int CompanyAdminRoleId = 2;
                const int EmployeeRoleId = 3;
                const int DepartmentHeadRoleId = 4;

                var project = await _context.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.ProjectId == id && p.IsDeleted != true);
                if (project == null)
                {
                    _logger.LogWarning($"Project with ID {id} not found for members fetch.");
                    return Error<IEnumerable<object>>($"Project with ID {id} not found.", StatusCodes.Status404NotFound);
                }

                if (currentRoleId == CompanyAdminRoleId || currentRoleId == DepartmentHeadRoleId)
                {
                    if (project.CompanyId != currentCompanyId.Value)
                    {
                        return Error<IEnumerable<object>>("You do not have permission to view members for this project.", StatusCodes.Status403Forbidden);
                    }
                }
                else if (currentRoleId == EmployeeRoleId)
                {
                    bool isMember = await _context.ProjectMembers.AnyAsync(pm => pm.ProjectId == id && pm.UserId == currentUserId.Value && pm.IsDeleted != true);
                    if (!isMember)
                    {
                        return Error<IEnumerable<object>>("You do not have permission to view members for this project.", StatusCodes.Status403Forbidden);
                    }
                }
                else if (currentRoleId != SuperAdminRoleId)
                {
                    return Error<IEnumerable<object>>("Access denied due to unknown role.", StatusCodes.Status403Forbidden);
                }

                var membersEntity = await _context.ProjectMembers
                    .Where(pm => pm.ProjectId == id && pm.IsDeleted != true)
                    .Include(pm => pm.User)
                        .ThenInclude(u => u.CompanyUserUsers)
                    .ToListAsync();

                var members = membersEntity.Select(pm => new
                {
                    pm.Id,
                    pm.UserId,
                    pm.JoinedAt,
                    pm.AddedBy,
                    User = pm.User != null ? new
                    {
                        pm.User.UserId,
                        pm.User.Email,
                        // Safely get ProfilePhotoUrl, FirstName, LastName from the first CompanyUser record for the relevant company
                        ProfilePhotoUrl = pm.User.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == project.CompanyId) != null ?
                                            pm.User.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == project.CompanyId).ProfilePhotoUrl : null,
                        FirstName = pm.User.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == project.CompanyId) != null ?
                                    pm.User.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == project.CompanyId).FirstName : null,
                        LastName = pm.User.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == project.CompanyId) != null ?
                                    pm.User.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == project.CompanyId).LastName : null,
                        EmployeeCode = pm.User.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == project.CompanyId) != null ?
                                            pm.User.CompanyUserUsers.FirstOrDefault(cu => cu.CompanyId == project.CompanyId).EmployeeCode : null
                    } : null
                }).ToList();

                _logger.LogInformation($"Retrieved {members.Count} members for project ID: {id}.");
                return Success<IEnumerable<object>>("Members retrieved successfully.", members);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error occurred while fetching members for project ID: {id}");
                return Error<IEnumerable<object>>("An error occurred while retrieving project members", StatusCodes.Status500InternalServerError);
            }
        }

        // POST: api/Project
        [HttpPost]
        public async Task<ActionResult<ApiResponse<object>>> CreateProject([FromBody] ProjectInputDto dto)
        {
            try
            {
                _logger.LogInformation("Attempting to create a new project.");

                var createdByUserId = GetCurrentUserId();
                var currentCompanyId = GetCurrentUserCompanyId();
                var currentRoleId = GetCurrentUserRoleId();

                if (!createdByUserId.HasValue || !currentCompanyId.HasValue || !currentRoleId.HasValue)
                {
                    return Unauthorized(new ApiResponse<object>("Authentication information (UserId, CompanyId, RoleId) is missing.", "Error", 401));
                }

                const int SuperAdminRoleId = 1;
                const int CompanyAdminRoleId = 2;
                const int DepartmentHeadRoleId = 4;

                if (currentRoleId != SuperAdminRoleId && currentRoleId != CompanyAdminRoleId && currentRoleId != DepartmentHeadRoleId)
                {
                    _logger.LogWarning($"User ID: {createdByUserId} with Role ID: {currentRoleId} attempted to create a project. Access denied.");
                    return StatusCode(403, new ApiResponse<object>("You do not have permission to create projects.", "Error", 403));
                }

                if ((currentRoleId == CompanyAdminRoleId || currentRoleId == DepartmentHeadRoleId) && dto.CompanyId != currentCompanyId.Value)
                {
                    _logger.LogWarning($"Company Admin/Department Head (User ID: {createdByUserId}, Company ID: {currentCompanyId}) attempted to create project for a different company ID: {dto.CompanyId}.");
                    return StatusCode(403, new ApiResponse<object>("Company Admins and Department Heads can only create projects for their own company.", "Error", 403));
                }

                var companyExists = await _context.Companies.AnyAsync(c => c.CompanyId == dto.CompanyId);
                if (!companyExists)
                {
                    _logger.LogWarning($"Attempt to create project with non-existent CompanyId: {dto.CompanyId} by User ID: {createdByUserId}");
                    return BadRequest(new ApiResponse<object>($"Company with ID {dto.CompanyId} does not exist.", "Error", 400));
                }

                var project = new Project
                {
                    Name = dto.Name,
                    Description = dto.Description,
                    Status = dto.Status ?? "Planned",
                    StartDate = dto.StartDate,
                    EndDate = dto.EndDate,
                    CompanyId = dto.CompanyId,
                    IsDeleted = false,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = createdByUserId
                };

                _context.Projects.Add(project);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Project created successfully with ID: {project.ProjectId} by user {createdByUserId}.");
                return CreatedAtAction(
                    nameof(GetProject),
                    new { id = project.ProjectId },
                    new ApiResponse<object>("Project created successfully", "Success", 201, new { projectId = project.ProjectId })
                );
            }
            catch (DbUpdateException dbEx)
            {
                var innerException = dbEx.InnerException;
                _logger.LogError(dbEx, "DbUpdateException occurred while creating project. Inner Exception: {InnerMessage}", innerException?.Message);
                return StatusCode(500, new ApiResponse<object>("An error occurred while saving the project to the database.", "Error", 500, new { databaseError = dbEx.Message, innerError = innerException?.Message }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while creating project.");
                return StatusCode(500, new ApiResponse<object>("An unexpected error occurred while creating the project.", "Error", 500, null));
            }
        }

        // PUT: api/Project/5
        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponse<object>>> UpdateProject(int id, [FromBody] ProjectUpdateDto dto)
        {
            try
            {
                _logger.LogInformation($"Attempting to update project with ID: {id}");

                var updatedByUserId = GetCurrentUserId();
                var currentCompanyId = GetCurrentUserCompanyId();
                var currentRoleId = GetCurrentUserRoleId();

                if (!updatedByUserId.HasValue || !currentCompanyId.HasValue || !currentRoleId.HasValue)
                {
                    return Unauthorized(new ApiResponse<object>("Authentication information (UserId, CompanyId, RoleId) is missing.", "Error", 401));
                }

                const int SuperAdminRoleId = 1;
                const int CompanyAdminRoleId = 2;
                const int DepartmentHeadRoleId = 4;

                var existingProject = await _context.Projects
                    .FirstOrDefaultAsync(p => p.ProjectId == id && p.IsDeleted != true);

                if (existingProject == null)
                {
                    _logger.LogWarning($"Project with ID {id} not found for update by User ID: {updatedByUserId}");
                    return NotFound(new ApiResponse<object>($"Project with ID {id} not found", "Error", 404));
                }

                // Authorization check for update
                if (currentRoleId == SuperAdminRoleId)
                {
                    _logger.LogInformation($"Super Admin (User ID: {updatedByUserId}) is updating project ID: {id}.");
                }
                else if (currentRoleId == CompanyAdminRoleId)
                {
                    if (existingProject.CompanyId != currentCompanyId.Value)
                    {
                        _logger.LogWarning($"Company Admin (User ID: {updatedByUserId}, Company ID: {currentCompanyId}) attempted to update project ID: {id} which belongs to a different company ({existingProject.CompanyId}).");
                        return StatusCode(403, new ApiResponse<object>("You do not have permission to update projects outside your company.", "Error", 403));
                    }
                    _logger.LogInformation($"Company Admin (User ID: {updatedByUserId}) is updating project ID: {id} within their company.");
                }
                else if (currentRoleId == DepartmentHeadRoleId)
                {
                    if (existingProject.CompanyId != currentCompanyId.Value)
                    {
                        _logger.LogWarning($"Department Head (User ID: {updatedByUserId}, Company ID: {currentCompanyId}) attempted to update project ID: {id} which belongs to a different company ({existingProject.CompanyId}).");
                        return StatusCode(403, new ApiResponse<object>("You do not have permission to update projects outside your company.", "Error", 403));
                    }
                    _logger.LogInformation($"Department Head (User ID: {updatedByUserId}) is updating project ID: {id} within their company.");
                }
                else
                {
                    _logger.LogWarning($"User ID: {updatedByUserId} with Role ID: {currentRoleId} attempted to update project ID: {id}. Access denied.");
                    return StatusCode(403, new ApiResponse<object>("You do not have permission to update projects.", "Error", 403));
                }

                // Update allowed fields
                existingProject.Name = dto.Name;
                existingProject.Description = dto.Description;
                existingProject.Status = dto.Status ?? existingProject.Status;
                existingProject.StartDate = dto.StartDate;
                existingProject.EndDate = dto.EndDate;
                existingProject.UpdatedAt = DateTime.UtcNow;
                existingProject.UpdatedBy = updatedByUserId;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Project with ID {id} updated successfully by User ID: {updatedByUserId}.");
                return Ok(new ApiResponse<object>("Project updated successfully", "Success", 200, null));
            }
            catch (DbUpdateException dbEx)
            {
                var innerException = dbEx.InnerException;
                _logger.LogError(dbEx, "DbUpdateException occurred while updating project with ID: {ProjectId}. Inner Exception: {InnerMessage}", id, innerException?.Message);
                return StatusCode(500, new ApiResponse<object>("An error occurred while saving project updates to the database.", "Error", 500, new { databaseError = dbEx.Message, innerError = innerException?.Message }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An unexpected error occurred while updating project with ID: {id}");
                return StatusCode(500, new ApiResponse<object>("An unexpected error occurred while updating the project.", "Error", 500, null));
            }
        }

        // DELETE: api/Project/5
        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteProject(int id)
        {
            try
            {
                _logger.LogInformation($"Attempting to soft-delete project with ID: {id}");

                var updatedByUserId = GetCurrentUserId();
                var currentCompanyId = GetCurrentUserCompanyId();
                var currentRoleId = GetCurrentUserRoleId();

                if (!updatedByUserId.HasValue || !currentCompanyId.HasValue || !currentRoleId.HasValue)
                {
                    return Unauthorized(new ApiResponse<object>("Authentication information (UserId, CompanyId, RoleId) is missing.", "Error", 401));
                }

                const int SuperAdminRoleId = 1;
                const int CompanyAdminRoleId = 2;
                const int DepartmentHeadRoleId = 4;

                var project = await _context.Projects.FirstOrDefaultAsync(p => p.ProjectId == id && p.IsDeleted != true);
                if (project == null)
                {
                    _logger.LogWarning($"Project with ID {id} not found for deletion by User ID: {updatedByUserId}");
                    return NotFound(new ApiResponse<object>($"Project with ID {id} not found", "Error", 404));
                }

                if (currentRoleId == SuperAdminRoleId)
                {
                    _logger.LogInformation($"Super Admin (User ID: {updatedByUserId}) is soft-deleting project ID: {id}.");
                }
                else if (currentRoleId == CompanyAdminRoleId)
                {
                    if (project.CompanyId != currentCompanyId.Value)
                    {
                        _logger.LogWarning($"Company Admin (User ID: {updatedByUserId}, Company ID: {currentCompanyId}) attempted to soft-delete project ID: {id} which belongs to a different company ({project.CompanyId}).");
                        return StatusCode(403, new ApiResponse<object>("You do not have permission to delete projects outside your company.", "Error", 403));
                    }
                    _logger.LogInformation($"Company Admin (User ID: {updatedByUserId}) is soft-deleting project ID: {id} within their company.");
                }
                else if (currentRoleId == DepartmentHeadRoleId)
                {
                    if (project.CompanyId != currentCompanyId.Value)
                    {
                        _logger.LogWarning($"Department Head (User ID: {updatedByUserId}, Company ID: {currentCompanyId}) attempted to soft-delete project ID: {id} which belongs to a different company ({project.CompanyId}).");
                        return StatusCode(403, new ApiResponse<object>("You do not have permission to delete projects outside your company.", "Error", 403));
                    }
                    _logger.LogInformation($"Department Head (User ID: {updatedByUserId}) is soft-deleting project ID: {id} within their company.");
                }
                else
                {
                    _logger.LogWarning($"User ID: {updatedByUserId} with Role ID: {currentRoleId} attempted to soft-delete project ID: {id}. Access denied.");
                    return StatusCode(403, new ApiResponse<object>("You do not have permission to delete projects.", "Error", 403));
                }

                project.IsDeleted = true; // Soft delete
                project.UpdatedAt = DateTime.UtcNow;
                project.UpdatedBy = updatedByUserId;

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Project with ID {id} soft-deleted successfully by User ID: {updatedByUserId}.");
                return Ok(new ApiResponse<object>("Project soft-deleted successfully.", "Success", 200, null));
            }
            catch (DbUpdateException dbEx)
            {
                var innerException = dbEx.InnerException;
                _logger.LogError(dbEx, "DbUpdateException occurred while deleting project with ID: {ProjectId}. Inner Exception: {InnerMessage}", id, innerException?.Message);
                return StatusCode(500, new ApiResponse<object>("An error occurred while saving project deletion to the database.", "Error", 500, new { databaseError = dbEx.Message, innerError = innerException?.Message }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An unexpected error occurred while deleting project with ID: {id}");
                return StatusCode(500, new ApiResponse<object>("An unexpected error occurred while deleting the project.", "Error", 500, null));
            }
        }
        // POST: api/Project/{projectId}/members
        [HttpPost("{projectId}/members")]
        public async Task<ActionResult<ApiResponse<object>>> AddProjectMember(int projectId, [FromBody] AddProjectMemberDto dto)
        {
            try
            {
                _logger.LogInformation($"Attempting to add member {dto.UserId} to project ID: {projectId}.");

                var currentUserId = GetCurrentUserId();
                var currentCompanyId = GetCurrentUserCompanyId();
                var currentRoleId = GetCurrentUserRoleId();

                if (!currentUserId.HasValue || !currentCompanyId.HasValue || !currentRoleId.HasValue)
                {
                    return Error<object>("Authentication information (UserId, CompanyId, RoleId) is missing.", StatusCodes.Status401Unauthorized);
                }

                const int SuperAdminRoleId = 1;
                const int CompanyAdminRoleId = 2;
                const int DepartmentHeadRoleId = 4;

                var project = await _context.Projects
                    .FirstOrDefaultAsync(p => p.ProjectId == projectId && p.IsDeleted != true);
                if (project == null)
                {
                    return Error<object>($"Project with ID {projectId} not found.", StatusCodes.Status404NotFound);
                }

                if (currentRoleId != SuperAdminRoleId)
                {
                    if (currentRoleId != CompanyAdminRoleId && currentRoleId != DepartmentHeadRoleId)
                    {
                        return Error<object>("You do not have permission to add project members.", StatusCodes.Status403Forbidden);
                    }
                    if (project.CompanyId != currentCompanyId.Value)
                    {
                        return Error<object>("You do not have permission to add members to projects outside your company.", StatusCodes.Status403Forbidden);
                    }
                }

                var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == dto.UserId && !u.IsDeleted);
                if (user == null)
                {
                    return Error<object>($"User with ID {dto.UserId} not found.", StatusCodes.Status404NotFound);
                }

                if (currentRoleId != SuperAdminRoleId && user.CompanyId != currentCompanyId.Value)
                {
                    return Error<object>("Cannot add a user from another company.", StatusCodes.Status403Forbidden);
                }

                var existingMember = await _context.ProjectMembers
                    .FirstOrDefaultAsync(pm => pm.ProjectId == projectId && pm.UserId == dto.UserId && pm.IsDeleted != true);
                if (existingMember != null)
                {
                    return Error<object>("User is already a member of the project.", StatusCodes.Status400BadRequest, "fail");
                }

                var member = new ProjectMember
                {
                    ProjectId = projectId,
                    UserId = dto.UserId,
                    AddedBy = currentUserId,
                    JoinedAt = DateOnly.FromDateTime(DateTime.UtcNow),
                    IsDeleted = false
                };

                _context.ProjectMembers.Add(member);
                await _context.SaveChangesAsync();

                return Success<object>("Member added to project successfully.", null, StatusCodes.Status201Created);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error occurred while adding member to project ID: {projectId}");
                return Error<object>("An error occurred while adding the member to the project.", StatusCodes.Status500InternalServerError);
            }
        }

        // DELETE: api/Project/{projectId}/members/{userId}
        [HttpDelete("{projectId}/members/{userId}")]
        public async Task<ActionResult<ApiResponse<object>>> RemoveProjectMember(int projectId, int userId)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var currentCompanyId = GetCurrentUserCompanyId();
                var currentRoleId = GetCurrentUserRoleId();

                if (!currentUserId.HasValue || !currentCompanyId.HasValue || !currentRoleId.HasValue)
                {
                    return Error<object>("Authentication information (UserId, CompanyId, RoleId) is missing.", StatusCodes.Status401Unauthorized);
                }

                const int SuperAdminRoleId = 1;
                const int CompanyAdminRoleId = 2;
                const int DepartmentHeadRoleId = 4;

                var project = await _context.Projects
                    .FirstOrDefaultAsync(p => p.ProjectId == projectId && p.IsDeleted != true);
                if (project == null)
                {
                    return Error<object>($"Project with ID {projectId} not found.", StatusCodes.Status404NotFound);
                }

                if (currentRoleId != SuperAdminRoleId)
                {
                    if (currentRoleId != CompanyAdminRoleId && currentRoleId != DepartmentHeadRoleId)
                    {
                        return Error<object>("You do not have permission to remove project members.", StatusCodes.Status403Forbidden);
                    }
                    if (project.CompanyId != currentCompanyId.Value)
                    {
                        return Error<object>("You do not have permission to remove members from projects outside your company.", StatusCodes.Status403Forbidden);
                    }
                }

                var member = await _context.ProjectMembers
                    .FirstOrDefaultAsync(pm => pm.ProjectId == projectId && pm.UserId == userId && pm.IsDeleted != true);
                if (member == null)
                {
                    return Error<object>("Member not found in this project.", StatusCodes.Status404NotFound);
                }

                member.IsDeleted = true;
                // member.UpdatedAt = DateTime.UtcNow; // If you have UpdatedAt in ProjectMember model
                member.AddedBy = currentUserId; // Can be a "RemovedBy" field if you add one

                await _context.SaveChangesAsync();

                return Success<object>("Member removed from project successfully.", null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error occurred while removing member from project ID: {projectId}");
                return Error<object>("An error occurred while removing the member from the project.", StatusCodes.Status500InternalServerError);
            }
        }

        // POST: api/Project/{projectId}/documents
        [HttpPost("{projectId}/documents")]
        [Consumes("multipart/form-data")] // Important for file uploads
        [ProducesResponseType(typeof(ApiResponse<ProjectDocument>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<ProjectDocument>>> UploadProjectDocument(
            int projectId,
            [FromForm] ProjectDocumentUploadDto dto) // Use [FromForm] for multipart/form-data
        {
            try
            {
                _logger.LogInformation($"Attempting to upload document for project ID: {projectId}. Document Name: {dto.DocumentName}");

                var currentUserId = GetCurrentUserId();
                var currentCompanyId = GetCurrentUserCompanyId();
                var currentRoleId = GetCurrentUserRoleId();

                if (!currentUserId.HasValue || !currentCompanyId.HasValue || !currentRoleId.HasValue)
                {
                    return Error<ProjectDocument>("Authentication information (UserId, CompanyId, RoleId) is missing.", StatusCodes.Status401Unauthorized);
                }

                // Role-based authorization for uploading project documents
                // Typically, Company Admins, Department Heads, and Super Admins can upload.
                // Employees might also be allowed if they are members of the project.
                const int SuperAdminRoleId = 1;
                const int CompanyAdminRoleId = 2;
                const int EmployeeRoleId = 3;
                const int DepartmentHeadRoleId = 4;

                var project = await _context.Projects.FirstOrDefaultAsync(p => p.ProjectId == projectId && p.IsDeleted != true);
                if (project == null)
                {
                    return Error<ProjectDocument>($"Project with ID {projectId} not found.", StatusCodes.Status404NotFound);
                }

                // Check if the current user has permission to upload documents for this project
                bool hasPermission = false;
                if (currentRoleId == SuperAdminRoleId)
                {
                    hasPermission = true;
                }
                else if (currentRoleId == CompanyAdminRoleId || currentRoleId == DepartmentHeadRoleId)
                {
                    if (project.CompanyId == currentCompanyId.Value)
                    {
                        hasPermission = true;
                    }
                }
                else if (currentRoleId == EmployeeRoleId)
                {
                    // Employees can upload if they are a member of the project AND the project belongs to their company
                    bool isProjectMember = await _context.ProjectMembers.AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == currentUserId.Value && pm.IsDeleted != true);
                    if (isProjectMember && project.CompanyId == currentCompanyId.Value)
                    {
                        hasPermission = true;
                    }
                }

                if (!hasPermission)
                {
                    _logger.LogWarning($"User ID: {currentUserId} with Role ID: {currentRoleId} attempted to upload document for project ID: {projectId}. Access denied.");
                    return Error<ProjectDocument>("You do not have permission to upload documents for this project.", StatusCodes.Status403Forbidden);
                }

                // Validate DTO
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state for project document upload: {Errors}", ModelState);
                    return BadRequest(new ApiResponse<object>("Invalid document data provided.", "Error", StatusCodes.Status400BadRequest, ModelState));
                }

                if (dto.File == null || dto.File.Length == 0)
                {
                    return Error<ProjectDocument>("No file uploaded or file is empty.", StatusCodes.Status400BadRequest);
                }

                // Use the UploadHandler service to handle the file saving and DB entry
                var uploadedDocument = await _uploadHandler.UploadProjectDocumentAsync(
                    currentUserId.Value,
                    currentCompanyId.Value,
                    projectId,
                    dto.File,
                    dto.DocumentName,
                    dto.Description,
                    dto.Version
                );

                _logger.LogInformation($"Document '{uploadedDocument.DocumentName}' (ID: {uploadedDocument.DocumentId}) uploaded successfully for project ID: {projectId}.");
                return Success("Project document uploaded successfully.", uploadedDocument, StatusCodes.Status201Created);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Validation error during project document upload for project ID {ProjectId}.", projectId);
                return Error<ProjectDocument>(ex.Message, StatusCodes.Status400BadRequest);
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Argument error during project document upload for project ID {ProjectId}.", projectId);
                return Error<ProjectDocument>(ex.Message, StatusCodes.Status400BadRequest);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "File system error during project document upload for project ID {ProjectId}.", projectId);
                return Error<ProjectDocument>("A file system error occurred during upload. Please try again.", StatusCodes.Status500InternalServerError, ex.Message);
            }
            catch (DbUpdateException dbEx)
            {
                var innerException = dbEx.InnerException;
                _logger.LogError(dbEx, "Database error during project document upload for project ID {ProjectId}. Inner Exception: {InnerMessage}", projectId, innerException?.Message);
                return Error<ProjectDocument>("A database error occurred while saving document information.", StatusCodes.Status500InternalServerError, new { databaseError = dbEx.Message, innerError = innerException?.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An unexpected error occurred while uploading document for project ID: {projectId}");
                return Error<ProjectDocument>("An unexpected error occurred while uploading the project document.", StatusCodes.Status500InternalServerError);
            }
        }
    }
}