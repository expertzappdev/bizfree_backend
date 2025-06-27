using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BizfreeApp.Models;
using BizfreeApp.Models.DTOs;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Http; // For IFormFile and StatusCodes
using BizfreeApp.Services; // To inject IUploadHandler
using Microsoft.Extensions.Logging; // For ILogger
using System.IO; // For Path.GetFileNameWithoutExtension

namespace BizfreeApp.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class TasksController : ControllerBase
    {
        private readonly Data.ApplicationDbContext _context;
        private readonly IUploadHandler _uploadHandler; // Inject IUploadHandler
        private readonly ILogger<TasksController> _logger; // Inject ILogger

        public TasksController(Data.ApplicationDbContext context, IUploadHandler uploadHandler, ILogger<TasksController> logger)
        {
            _context = context;
            _uploadHandler = uploadHandler;
            _logger = logger;
        }

        // Helper Method to get the current Company's ID from claims
        private int? GetCompanyIdFromClaims()
        {
            var companyIdClaim = User.Claims.FirstOrDefault(c => c.Type == "CompanyId");
            if (companyIdClaim != null && int.TryParse(companyIdClaim.Value, out int companyId))
            {
                return companyId;
            }
            _logger.LogWarning("Company ID claim not found or could not be parsed.");
            return null;
        }

        // Helper Method to get the current User's ID from claims
        private int? GetCurrentUserIdFromClaims()
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
            {
                return userId;
            }
            _logger.LogWarning("User ID claim not found or could not be parsed.");
            return null;
        }

        // Helper Method to get the current User's Role ID from claims
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

        // Helper method to generate a standardized success response for ActionResult<ApiResponse<T>>
        private ActionResult<ApiResponse<T>> Success<T>(string message, T? data, int statusCode = StatusCodes.Status200OK)
        {
            return StatusCode(statusCode, new ApiResponse<T>(message, "success", statusCode, data));
        }

        // Overloaded Helper method to generate a standardized error response for ActionResult<ApiResponse<T>>
        private ActionResult<ApiResponse<T>> Error<T>(string message, int statusCode, string status = "error")
        {
            return StatusCode(statusCode, new ApiResponse<T>(message, status, statusCode, default(T)));
        }

        // Overloaded Helper method to generate a standardized error response for plain IActionResult
        private IActionResult Error(string message, int statusCode, string status = "error")
        {
            // Explicitly create an ApiResponse<object> for non-generic IActionResult returns
            return StatusCode(statusCode, new ApiResponse<object>(message, status, statusCode, null));
        }


        // Helper method to get a Project specific to the company
        private async Task<Project?> GetProjectForCompany(int projectId, int companyId)
        {
            return await _context.Projects
                                 .Where(p => p.ProjectId == projectId && p.CompanyId == companyId)
                                 .FirstOrDefaultAsync();
        }

        // Helper method to get a TaskList specific to the company and project
        private async Task<TaskList?> GetTaskListForCompanyAndProject(int taskListId, int projectId, int companyId)
        {
            return await _context.TaskLists
                                 .Where(tl => tl.TaskListId == taskListId && tl.ProjectId == projectId && tl.CompanyId == companyId)
                                 .FirstOrDefaultAsync();
        }

        // Helper method to get a TaskList by its ID and Company ID, regardless of Project ID in the path.
        private async Task<TaskList?> GetTaskListForCompany(int taskListId, int companyId)
        {
            return await _context.TaskLists
                                 .Where(tl => tl.TaskListId == taskListId && tl.CompanyId == companyId)
                                 .FirstOrDefaultAsync();
        }

        // Helper method to get a Task specific to the company by its TaskId (flatter API)
        private async Task<Models.Task?> GetTaskForCompany(int taskId, int companyId)
        {
            return await _context.Tasks
                                 .Include(t => t.TaskList)
                                 .Include(t => t.StatusNavigation)
                                 .Include(t => t.Priority)
                                 .Include(t => t.AssignedToNavigation)
                                 .Include(t => t.Company)
                                 // Include SubTasks and their related navigation properties
                                 .Include(t => t.SubTasks.Where(st => !st.IsDeleted))
                                     .ThenInclude(st => st.StatusNavigation)
                                 .Include(t => t.SubTasks.Where(st => !st.IsDeleted))
                                     .ThenInclude(st => st.Priority)
                                 .Include(t => t.SubTasks.Where(st => !st.IsDeleted))
                                     .ThenInclude(st => st.AssignedToNavigation)
                                 // Include Documents and their related navigation properties (e.g., CreatedByNavigation for CreatedByName)
                                 .Include(t => t.TaskDocuments.Where(td => !td.IsDeleted)) // Assuming IsDeleted on TaskDocument
                                 .Where(t => t.TaskId == taskId && t.CompanyId == companyId)
                                 .FirstOrDefaultAsync();
        }


        // GET: api/Tasks/tasklists/{taskListId}/tasks
        // This endpoint retrieves all tasks within a specific task list for the authorized company, with filtering, paging, and sorting.
        [HttpGet("tasklists/{taskListId}/tasks")]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<TaskDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<PagedResult<TaskDto>>>> GetTasksByTaskList(
            int taskListId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 5,
            [FromQuery] string sortBy = "DueDate",
            [FromQuery] string sortOrder = "desc",
            [FromQuery] string? search = null,
            [FromQuery] int? statusId = null,
            [FromQuery] int? priorityId = null,
            [FromQuery] int? assignedToUserId = null,
            [FromQuery] DateOnly? dueDateFrom = null,
            [FromQuery] DateOnly? dueDateTo = null)
        {
            var companyId = GetCompanyIdFromClaims();
            if (!companyId.HasValue)
            {
                return Error<PagedResult<TaskDto>>("Company ID not found in claims.", StatusCodes.Status401Unauthorized);
            }

            var taskList = await GetTaskListForCompany(taskListId, companyId.Value);
            if (taskList == null || taskList.IsDeleted)
            {
                return Error<PagedResult<TaskDto>>("Task List not found or not accessible.", StatusCodes.Status404NotFound);
            }

            IQueryable<Models.Task> query = _context.Tasks
                .Include(t => t.StatusNavigation)
                .Include(t => t.Priority)
                .Include(t => t.TaskList)
                .Include(t => t.AssignedToNavigation)
                .Include(t => t.Company)
                // Include subtasks and their related navigation properties
                .Include(t => t.SubTasks.Where(st => !st.IsDeleted))
                    .ThenInclude(st => st.StatusNavigation)
                .Include(t => t.SubTasks.Where(st => !st.IsDeleted))
                    .ThenInclude(st => st.Priority)
                .Include(t => t.SubTasks.Where(st => !st.IsDeleted))
                    .ThenInclude(st => st.AssignedToNavigation)
                // Include documents and their related navigation properties
                .Include(t => t.TaskDocuments.Where(td => !td.IsDeleted))
                .Where(t => t.TaskListId == taskListId &&
                            t.ProjectId == taskList.ProjectId &&
                            t.CompanyId == companyId.Value &&
                            !t.IsDeleted &&
                            !t.ParentTaskId.HasValue); // Filter to get only top-level tasks

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(t => t.Title!.Contains(search) || (t.Description != null && t.Description.Contains(search)));
            }

            if (statusId.HasValue)
            {
                query = query.Where(t => t.Status == statusId.Value);
            }

            if (priorityId.HasValue)
            {
                query = query.Where(t => t.PriorityId == priorityId.Value);
            }

            if (assignedToUserId.HasValue)
            {
                query = query.Where(t => t.AssignedTo == assignedToUserId.Value);
            }

            if (dueDateFrom.HasValue)
            {
                query = query.Where(t => t.DueDate.HasValue && t.DueDate.Value >= dueDateFrom.Value);
            }

            if (dueDateTo.HasValue)
            {
                query = query.Where(t => t.DueDate.HasValue && t.DueDate.Value <= dueDateTo.Value);
            }

            var totalTasks = await query.CountAsync();

            var taskPropertyMap = new Dictionary<string, Expression<Func<Models.Task, object>>>
            {
                { "taskid", t => t.TaskId },
                { "title", t => t.Title! },
                { "statusname", t => t.StatusNavigation!.Name! },
                { "duedate", t => t.DueDate! },
                { "priorityname", t => t.Priority!.Name! },
                { "assignedtouserid", t => t.AssignedTo! },
                { "listname", t => t.TaskList!.ListName! },
                { "taskorder", t => t.TaskOrder! },
                { "companyname", t => t.Company!.CompanyName! }
            };

            if (taskPropertyMap.TryGetValue(sortBy.ToLower(), out var sortExpression))
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
                query = query.OrderBy(t => t.TaskId);
            }

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new TaskDto
                {
                    TaskId = t.TaskId,
                    Title = t.Title,
                    StatusName = t.StatusNavigation != null ? t.StatusNavigation.Name : null,
                    DueDate = t.DueDate,
                    PriorityName = t.Priority != null ? t.Priority.Name : null,
                    Description = t.Description,
                    AssignedToUserId = t.AssignedTo,
                    TaskListId = t.TaskList != null ? t.TaskList.TaskListId : null,
                    ListName = t.TaskList != null ? t.TaskList.ListName : null,
                    TaskListDescription = t.TaskList != null ? t.TaskList.Description : null,
                    ListOrder = t.TaskList != null ? t.TaskList.ListOrder : null,
                    ProjectId = t.ProjectId,
                    CompanyName = t.Company != null ? t.Company.CompanyName : null,
                    ParentTaskId = t.ParentTaskId,
                    Subtasks = t.SubTasks.Select(st => new TaskDto
                    {
                        TaskId = st.TaskId,
                        Title = st.Title,
                        StatusName = st.StatusNavigation != null ? st.StatusNavigation.Name : null,
                        DueDate = st.DueDate,
                        PriorityName = st.Priority != null ? st.Priority.Name : null,
                        Description = st.Description,
                        AssignedToUserId = st.AssignedTo,
                        TaskListId = st.TaskListId,
                        ListName = st.TaskList != null ? st.TaskList.ListName : null,
                        TaskListDescription = st.TaskList != null ? st.TaskList.Description : null,
                        ListOrder = st.TaskList != null ? st.TaskList.ListOrder : null,
                        ProjectId = st.ProjectId,
                        CompanyName = st.Company != null ? st.Company.CompanyName : null,
                        ParentTaskId = st.ParentTaskId
                    }).ToList(),
                    Documents = t.TaskDocuments.Select(td => new TaskDocumentDto // Project documents into TaskDocumentDto
                    {
                        DocumentId = td.DocumentId,
                        TaskId = td.TaskId,
                        DocumentName = td.DocumentName,
                        FilePath = td.FilePath,
                        DocumentType = td.DocumentType,
                        Description = td.Description,
                    }).ToList()
                })
                .ToListAsync();

            var pagedResult = new PagedResult<TaskDto>
            {
                Items = items,
                TotalCount = totalTasks,
                PageNumber = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalTasks / pageSize),
                TotalTasks = totalTasks
            };

            return Success("Tasks retrieved successfully.", pagedResult);
        }

        // GET: api/Tasks/{taskId}
        // This endpoint retrieves a specific task by its ID (flatter API).
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<TaskDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<TaskDto>>> GetTaskById(int id)
        {
            var companyId = GetCompanyIdFromClaims();
            if (!companyId.HasValue)
            {
                return Error<TaskDto>("Company ID not found in claims.", StatusCodes.Status401Unauthorized);
            }

            var task = await GetTaskForCompany(id, companyId.Value);

            if (task == null || task.IsDeleted)
            {
                return Error<TaskDto>("Task not found or not accessible.", StatusCodes.Status404NotFound);
            }

            var taskDto = new TaskDto
            {
                TaskId = task.TaskId,
                Title = task.Title,
                StatusName = task.StatusNavigation?.Name,
                DueDate = task.DueDate,
                PriorityName = task.Priority?.Name,
                Description = task.Description,
                AssignedToUserId = task.AssignedTo,
                TaskListId = task.TaskList?.TaskListId,
                ListName = task.TaskList?.ListName,
                TaskListDescription = task.TaskList?.Description,
                ListOrder = task.TaskList?.ListOrder,
                ProjectId = task.ProjectId,
                CompanyName = task.Company?.CompanyName,
                ParentTaskId = task.ParentTaskId,
                Subtasks = task.SubTasks.Select(st => new TaskDto
                {
                    TaskId = st.TaskId,
                    Title = st.Title,
                    StatusName = st.StatusNavigation != null ? st.StatusNavigation.Name : null,
                    DueDate = st.DueDate,
                    PriorityName = st.Priority != null ? st.Priority.Name : null,
                    Description = st.Description,
                    AssignedToUserId = st.AssignedTo,
                    TaskListId = st.TaskListId,
                    ListName = st.TaskList != null ? st.TaskList.ListName : null,
                    TaskListDescription = st.TaskList != null ? st.TaskList.Description : null,
                    ListOrder = st.TaskList != null ? st.TaskList.ListOrder : null,
                    ProjectId = st.ProjectId,
                    CompanyName = st.Company != null ? st.Company.CompanyName : null,
                    ParentTaskId = st.ParentTaskId
                }).ToList(),
                Documents = task.TaskDocuments.Select(td => new TaskDocumentDto
                {
                    DocumentId = td.DocumentId,
                    TaskId = td.TaskId,
                    DocumentName = td.DocumentName,
                    FilePath = td.FilePath,
                    DocumentType = td.DocumentType,
                    Description = td.Description,
                }).ToList()
            };

            return Success("Task retrieved successfully.", taskDto);
        }

        // GET: api/Tasks/users/mytasks
        // This endpoint retrieves all tasks assigned to the current user across all projects and task lists
        // within their company.
        [HttpGet("users/mytasks")]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<TaskDto>>), StatusCodes.Status200OK)] // Changed to PagedResult
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<ApiResponse<PagedResult<TaskDto>>>> GetMyTasks( // Changed return type
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 5,
            [FromQuery] string sortBy = "DueDate",
            [FromQuery] string sortOrder = "desc",
            [FromQuery] string? search = null,
            [FromQuery] int? statusId = null,
            [FromQuery] int? priorityId = null,
            // [FromQuery] int? assignedToUserId = null, // Removed as it's implicit for "my tasks"
            [FromQuery] DateOnly? dueDateFrom = null,
            [FromQuery] DateOnly? dueDateTo = null)
        {
            var companyId = GetCompanyIdFromClaims();
            if (!companyId.HasValue)
            {
                return Error<PagedResult<TaskDto>>("Company ID not found in claims.", StatusCodes.Status401Unauthorized);
            }

            var currentUserId = GetCurrentUserIdFromClaims();
            if (!currentUserId.HasValue)
            {
                return Error<PagedResult<TaskDto>>("User ID not found in claims.", StatusCodes.Status401Unauthorized);
            }

            IQueryable<Models.Task> query = _context.Tasks
                .Include(t => t.StatusNavigation)
                .Include(t => t.Priority)
                .Include(t => t.TaskList)
                .Include(t => t.AssignedToNavigation)
                .Include(t => t.Company)
                // Include subtasks and their related navigation properties for 'My Tasks'
                .Include(t => t.SubTasks.Where(st => !st.IsDeleted))
                    .ThenInclude(st => st.StatusNavigation)
                .Include(t => t.SubTasks.Where(st => !st.IsDeleted))
                    .ThenInclude(st => st.Priority)
                .Include(t => t.SubTasks.Where(st => !st.IsDeleted))
                    .ThenInclude(st => st.AssignedToNavigation)
                // Include documents and their related navigation properties
                .Include(t => t.TaskDocuments.Where(td => !td.IsDeleted))
                .Where(t => t.AssignedTo == currentUserId.Value && // Filter by current user
                            t.CompanyId == companyId.Value &&
                            !t.IsDeleted &&
                            !t.ParentTaskId.HasValue); // Only retrieve top-level tasks assigned to user

            // Apply filters
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(t => t.Title!.Contains(search) || (t.Description != null && t.Description.Contains(search)));
            }

            if (statusId.HasValue)
            {
                query = query.Where(t => t.Status == statusId.Value);
            }

            if (priorityId.HasValue)
            {
                query = query.Where(t => t.PriorityId == priorityId.Value);
            }

            // assignedToUserId filter is not needed here as it's fixed to currentUserId.Value
            // If you wanted to fetch tasks assigned to *any* user within the company,
            // then you would re-introduce assignedToUserId and apply it here.

            if (dueDateFrom.HasValue)
            {
                query = query.Where(t => t.DueDate.HasValue && t.DueDate.Value >= dueDateFrom.Value);
            }

            if (dueDateTo.HasValue)
            {
                query = query.Where(t => t.DueDate.HasValue && t.DueDate.Value <= dueDateTo.Value);
            }

            var totalTasks = await query.CountAsync();

            // Sorting logic (reused from GetTasksByTaskList)
            var taskPropertyMap = new Dictionary<string, Expression<Func<Models.Task, object>>>
            {
                { "taskid", t => t.TaskId },
                { "title", t => t.Title! },
                { "statusname", t => t.StatusNavigation!.Name! },
                { "duedate", t => t.DueDate! },
                { "priorityname", t => t.Priority!.Name! },
                { "assignedtouserid", t => t.AssignedTo! },
                { "listname", t => t.TaskList!.ListName! },
                { "taskorder", t => t.TaskOrder! },
                { "companyname", t => t.Company!.CompanyName! }
            };

            if (taskPropertyMap.TryGetValue(sortBy.ToLower(), out var sortExpression))
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
                query = query.OrderBy(t => t.TaskId); // Default sort if invalid sortBy is provided
            }

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new TaskDto
                {
                    TaskId = t.TaskId,
                    Title = t.Title,
                    StatusName = t.StatusNavigation != null ? t.StatusNavigation.Name : null,
                    DueDate = t.DueDate,
                    PriorityName = t.Priority != null ? t.Priority.Name : null,
                    Description = t.Description,
                    AssignedToUserId = t.AssignedTo,
                    TaskListId = t.TaskList != null ? t.TaskList.TaskListId : null,
                    ListName = t.TaskList != null ? t.TaskList.ListName : null,
                    TaskListDescription = t.TaskList != null ? t.TaskList.Description : null,
                    ListOrder = t.TaskList != null ? t.TaskList.ListOrder : null,
                    ProjectId = t.ProjectId,
                    CompanyName = t.Company != null ? t.Company.CompanyName : null,
                    ParentTaskId = t.ParentTaskId,
                    Subtasks = t.SubTasks.Select(st => new TaskDto
                    {
                        TaskId = st.TaskId,
                        Title = st.Title,
                        StatusName = st.StatusNavigation != null ? st.StatusNavigation.Name : null,
                        DueDate = st.DueDate,
                        PriorityName = st.Priority != null ? st.Priority.Name : null,
                        Description = st.Description,
                        AssignedToUserId = st.AssignedTo,
                        TaskListId = st.TaskListId,
                        ListName = st.TaskList != null ? st.TaskList.ListName : null,
                        TaskListDescription = st.TaskList != null ? st.TaskList.Description : null,
                        ListOrder = st.TaskList != null ? st.TaskList.ListOrder : null,
                        ProjectId = st.ProjectId,
                        CompanyName = st.Company != null ? st.Company.CompanyName : null,
                        ParentTaskId = st.ParentTaskId
                    }).ToList(),
                    Documents = t.TaskDocuments.Select(td => new TaskDocumentDto
                    {
                        DocumentId = td.DocumentId,
                        TaskId = td.TaskId,
                        DocumentName = td.DocumentName,
                        FilePath = td.FilePath,
                        DocumentType = td.DocumentType,
                        Description = td.Description,
                        CreatedAt = td.CreatedAt,
                    }).ToList()
                })
                .ToListAsync();

            var pagedResult = new PagedResult<TaskDto>
            {
                Items = items,
                TotalCount = totalTasks,
                PageNumber = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalTasks / pageSize),
                TotalTasks = totalTasks
            };

            return Success("My tasks retrieved successfully.", pagedResult);
        }

        // POST: api/Tasks/tasklists/{taskListId}/tasks
        // This endpoint allows creating a new task within a specific task list.
        [HttpPost("tasklists/{taskListId}/tasks")]
        [Authorize(Policy = "CompanyAdminAccess")]
        [ProducesResponseType(typeof(ApiResponse<TaskDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<TaskDto>>> CreateTask(int taskListId, TaskInputDto dto)
        {
            var companyId = GetCompanyIdFromClaims();
            if (!companyId.HasValue)
            {
                return Error<TaskDto>("Company ID not found in claims.", StatusCodes.Status401Unauthorized);
            }

            var currentUserId = GetCurrentUserIdFromClaims();
            if (!currentUserId.HasValue)
            {
                return Error<TaskDto>("User ID not found in claims.", StatusCodes.Status401Unauthorized);
            }

            var taskList = await GetTaskListForCompany(taskListId, companyId.Value);
            if (taskList == null || taskList.IsDeleted)
            {
                return Error<TaskDto>("The specified TaskList does not exist, is deleted, does not belong to your company, or is not accessible.", StatusCodes.Status404NotFound);
            }

            if (dto.TaskListId.HasValue && dto.TaskListId.Value != taskListId)
            {
                return Error<TaskDto>("TaskListId in body must match TaskListId in URL path for task creation.", StatusCodes.Status400BadRequest);
            }

            // A top-level task should not have a ParentTaskId
            if (dto.ParentTaskId.HasValue)
            {
                return Error<TaskDto>("ParentTaskId cannot be set when creating a top-level task directly under a TaskList. Use the subtasks endpoint instead.", StatusCodes.Status400BadRequest);
            }

            int? assignedTo = dto.AssignedToUserId;
            if (!assignedTo.HasValue)
            {
                assignedTo = currentUserId.Value;
            }

            if (assignedTo.HasValue)
            {
                var assignedUser = await _context.Users.FirstOrDefaultAsync(u => u.UserId == assignedTo.Value && u.CompanyId == companyId.Value);
                if (assignedUser == null)
                {
                    return Error<TaskDto>("Assigned user not found or does not belong to your company.", StatusCodes.Status400BadRequest);
                }
            }

            var task = new Models.Task
            {
                Title = dto.Title,
                Status = dto.StatusId,
                DueDate = dto.DueDate,
                PriorityId = dto.PriorityId,
                TaskListId = taskListId,
                Description = dto.Description,
                ProjectId = taskList.ProjectId,
                CompanyId = companyId.Value,
                AssignedTo = assignedTo,
                EstimatedHours = dto.EstimatedHours,
                ActualHours = dto.ActualHours,
                TaskOrder = dto.TaskOrder,
                CreatedBy = currentUserId.Value,
                CreatedAt = DateTimeOffset.UtcNow,
                IsActive = true,
                IsDeleted = false
            };

            _context.Tasks.Add(task);
            await _context.SaveChangesAsync();

            await _context.Entry(task).Reference(t => t.StatusNavigation).LoadAsync();
            await _context.Entry(task).Reference(t => t.Priority).LoadAsync();
            await _context.Entry(task).Reference(t => t.TaskList).LoadAsync();
            await _context.Entry(task).Reference(t => t.AssignedToNavigation).LoadAsync();
            await _context.Entry(task).Reference(t => t.Company).LoadAsync();
            // Subtasks won't exist immediately after creation, no need to load here
            // Documents won't exist immediately after creation, no need to load here

            var taskDto = new TaskDto
            {
                TaskId = task.TaskId,
                Title = task.Title,
                StatusName = task.StatusNavigation?.Name,
                DueDate = task.DueDate,
                PriorityName = task.Priority?.Name,
                Description = task.Description,
                AssignedToUserId = task.AssignedTo,
                TaskListId = task.TaskList?.TaskListId,
                ListName = task.TaskList?.ListName,
                TaskListDescription = task.TaskList?.Description,
                ListOrder = task.TaskList?.ListOrder,
                ProjectId = task.ProjectId,
                CompanyName = task.Company?.CompanyName,
                ParentTaskId = task.ParentTaskId,
                Subtasks = new List<TaskDto>(), // Initialize as empty list for new task
                Documents = new List<TaskDocumentDto>() // Initialize as empty list for new task
            };

            return Success("Task created successfully.", taskDto, StatusCodes.Status201Created);
        }

        // PUT: api/Tasks/{taskId}
        // This endpoint updates an existing task by its ID (flatter API).
        [HttpPut("{id}")]
        [Authorize(Policy = "CompanyAdminAccess")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateTask(int id, TaskInputDto dto)
        {
            var companyId = GetCompanyIdFromClaims();
            if (!companyId.HasValue)
            {
                return Error("Company ID not found in claims.", StatusCodes.Status401Unauthorized);
            }

            var currentUserId = GetCurrentUserIdFromClaims();
            if (!currentUserId.HasValue)
            {
                return Error("User ID not found in claims.", StatusCodes.Status401Unauthorized);
            }

            var task = await GetTaskForCompany(id, companyId.Value);

            if (task == null || task.IsDeleted)
            {
                return Error("Task not found or not accessible.", StatusCodes.Status404NotFound);
            }

            // Prevent changing ParentTaskId via this endpoint for top-level tasks
            if (task.ParentTaskId == null && dto.ParentTaskId.HasValue)
            {
                return Error("Cannot change a top-level task into a subtask via this endpoint.", StatusCodes.Status400BadRequest);
            }
            else if (task.ParentTaskId.HasValue && (!dto.ParentTaskId.HasValue || dto.ParentTaskId.Value != task.ParentTaskId.Value))
            {
                // If it's already a subtask, prevent changing its parent or making it top-level via this endpoint
                return Error("Cannot change a subtask's ParentTaskId or convert it to a top-level task via this endpoint.", StatusCodes.Status400BadRequest);
            }


            if (dto.TaskListId.HasValue && dto.TaskListId.Value != task.TaskListId)
            {
                var newTaskList = await GetTaskListForCompany(dto.TaskListId.Value, companyId.Value);
                if (newTaskList == null || newTaskList.IsDeleted)
                {
                    return Error("The new TaskList specified does not exist, is deleted, or does not belong to your company.", StatusCodes.Status400BadRequest);
                }

                task.TaskListId = dto.TaskListId.Value;
                task.ProjectId = newTaskList.ProjectId;
            }

            if (dto.AssignedToUserId.HasValue)
            {
                var assignedUser = await _context.Users.FirstOrDefaultAsync(u => u.UserId == dto.AssignedToUserId.Value && u.CompanyId == companyId.Value);
                if (assignedUser == null)
                {
                    return Error("The assigned user does not exist or does not belong to your company.", StatusCodes.Status400BadRequest);
                }
                task.AssignedTo = dto.AssignedToUserId.Value;
            }
            else
            {
                task.AssignedTo = null;
            }

            task.Title = dto.Title;
            task.Status = dto.StatusId;
            task.DueDate = dto.DueDate;
            task.PriorityId = dto.PriorityId;
            task.Description = dto.Description;
            task.EstimatedHours = dto.EstimatedHours;
            task.ActualHours = dto.ActualHours;
            task.TaskOrder = dto.TaskOrder;
            task.UpdatedAt = DateTimeOffset.UtcNow;
            task.UpdatedBy = currentUserId.Value;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: api/Tasks/{taskId}
        // This endpoint performs a soft delete on a task by setting IsDeleted to true (flatter API).
        [HttpDelete("{id}")]
        [Authorize(Policy = "CompanyAdminAccess")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteTask(int id)
        {
            var companyId = GetCompanyIdFromClaims();
            if (!companyId.HasValue)
            {
                return Error("Company ID not found in claims.", StatusCodes.Status401Unauthorized);
            }

            var currentUserId = GetCurrentUserIdFromClaims();
            if (!currentUserId.HasValue)
            {
                return Error("User ID not found in claims.", StatusCodes.Status401Unauthorized);
            }

            var task = await GetTaskForCompany(id, companyId.Value);
            if (task == null || task.IsDeleted)
            {
                return Error("Task not found or already deleted.", StatusCodes.Status404NotFound);
            }

            // Also soft delete all direct subtasks
            var subtasksToDelete = await _context.Tasks
                                                 .Where(st => st.ParentTaskId == id && st.CompanyId == companyId.Value && !st.IsDeleted)
                                                 .ToListAsync();
            foreach (var subtask in subtasksToDelete)
            {
                subtask.IsDeleted = true;
                subtask.UpdatedAt = DateTimeOffset.UtcNow;
                subtask.UpdatedBy = currentUserId.Value;
            }

            // Also soft delete all documents related to this task
            var documentsToDelete = await _context.TaskDocuments
                                                 .Where(td => td.TaskId == id && td.CompanyId == companyId.Value && !td.IsDeleted)
                                                 .ToListAsync();
            foreach (var document in documentsToDelete)
            {
                document.IsDeleted = true;
                document.UpdatedAt = DateTimeOffset.UtcNow;
            }

            task.IsDeleted = true;
            task.UpdatedAt = DateTimeOffset.UtcNow;
            task.UpdatedBy = currentUserId.Value;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // POST: api/Tasks/projects/{projectId}/tasklists
        // This endpoint creates a new task list within a specific project.
        [HttpPost("projects/{projectId}/tasklists")]
        [Authorize(Policy = "CompanyAdminAccess")]
        [ProducesResponseType(typeof(ApiResponse<TaskListDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<TaskListDto>>> CreateTaskList(int projectId, TaskListInputDto dto)
        {
            var companyId = GetCompanyIdFromClaims();
            if (!companyId.HasValue)
            {
                return Error<TaskListDto>("Company ID not found in claims.", StatusCodes.Status401Unauthorized);
            }

            var project = await GetProjectForCompany(projectId, companyId.Value);
            if (project == null)
            {
                return Error<TaskListDto>("Project not found or not accessible.", StatusCodes.Status404NotFound);
            }

            var currentUserId = GetCurrentUserIdFromClaims();
            if (!currentUserId.HasValue)
            {
                return Error<TaskListDto>("User ID not found in claims.", StatusCodes.Status401Unauthorized);
            }

            var taskList = new TaskList
            {
                ListName = dto.ListName,
                Description = dto.Description,
                ListOrder = dto.ListOrder,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                ProjectId = projectId,
                CompanyId = companyId.Value,
                Status = "active",
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = currentUserId.Value,
                IsActive = true,
                IsDeleted = false
            };

            _context.TaskLists.Add(taskList);
            await _context.SaveChangesAsync();

            var taskListDto = new TaskListDto
            {
                TaskListId = taskList.TaskListId,
                ListName = taskList.ListName,
                Description = taskList.Description,
                ListOrder = taskList.ListOrder,
                Status = taskList.Status,
                ProjectId = taskList.ProjectId,
                CompanyId = taskList.CompanyId,
                StartDate = taskList.StartDate,
                EndDate = taskList.EndDate
            };

            return Success("Task List created successfully.", taskListDto, StatusCodes.Status201Created);
        }

        // GET: api/Tasks/projects/{projectId}/tasklists
        // This endpoint retrieves all task lists for a specific project, with filtering, paging, and sorting.
        [HttpGet("projects/{projectId}/tasklists")]
        [ProducesResponseType(typeof(ApiResponse<PagedResult<TaskListDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<PagedResult<TaskListDto>>>> GetTaskLists(
            int projectId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 5,
            [FromQuery] string sortBy = "DueDate",
            [FromQuery] string sortOrder = "dec",
            [FromQuery] string? search = null,
            [FromQuery] string? status = null,
            [FromQuery] DateOnly? startDateFrom = null,
            [FromQuery] DateOnly? startDateTo = null,
            [FromQuery] DateOnly? endDateFrom = null,
            [FromQuery] DateOnly? endDateTo = null)
        {
            var companyId = GetCompanyIdFromClaims();
            if (!companyId.HasValue)
            {
                return Error<PagedResult<TaskListDto>>("Company ID not found in claims.", StatusCodes.Status401Unauthorized);
            }

            var project = await GetProjectForCompany(projectId, companyId.Value);
            if (project == null)
            {
                return Error<PagedResult<TaskListDto>>("Project not found or not accessible.", StatusCodes.Status404NotFound);
            }

            IQueryable<TaskList> query = _context.TaskLists
                .Where(tl => tl.CompanyId == companyId.Value && tl.ProjectId == projectId && !tl.IsDeleted);

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(tl => tl.Status == status);
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(tl => tl.ListName.Contains(search) ||
                                         (tl.Description != null && tl.Description.Contains(search)));
            }
            if (startDateFrom.HasValue)
            {
                query = query.Where(tl => tl.StartDate.HasValue && tl.StartDate.Value >= startDateFrom.Value);
            }
            if (startDateTo.HasValue)
            {
                query = query.Where(tl => tl.StartDate.HasValue && tl.StartDate.Value <= startDateTo.Value);
            }
            if (endDateFrom.HasValue)
            {
                query = query.Where(tl => tl.EndDate.HasValue && tl.EndDate.Value >= endDateFrom.Value);
            }
            if (endDateTo.HasValue)
            {
                query = query.Where(tl => tl.EndDate.HasValue && tl.EndDate.Value <= endDateTo.Value);
            }

            var totalCount = await query.CountAsync();

            var taskListPropertyMap = new Dictionary<string, Expression<Func<TaskList, object>>>
            {
                { "tasklistid", tl => tl.TaskListId },
                { "listname", tl => tl.ListName },
                { "listorder", tl => tl.ListOrder! },
                { "status", tl => tl.Status },
                { "startdate", tl => tl.StartDate! },
                { "enddate", tl => tl.EndDate! }
            };

            if (taskListPropertyMap.TryGetValue(sortBy.ToLower(), out var sortExpression))
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
                query = query.OrderBy(tl => tl.TaskListId);
            }

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(tl => new TaskListDto
                {
                    TaskListId = tl.TaskListId,
                    ListName = tl.ListName,
                    Description = tl.Description,
                    ListOrder = tl.ListOrder,
                    Status = tl.Status,
                    ProjectId = tl.ProjectId,
                    CompanyId = tl.CompanyId,
                    StartDate = tl.StartDate,
                    EndDate = tl.EndDate
                })
                .ToListAsync();

            return Success("Task lists retrieved successfully.", new PagedResult<TaskListDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                TotalTasks = totalCount
            });
        }

        // GET: api/Tasks/projects/{projectId}/tasklists/{id}
        // This endpoint retrieves a specific task list by its ID within a given project.
        [HttpGet("projects/{projectId}/tasklists/{id}")]
        [ProducesResponseType(typeof(ApiResponse<TaskListDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<TaskListDto>>> GetTaskListById(int projectId, int id)
        {
            var companyId = GetCompanyIdFromClaims();
            if (!companyId.HasValue)
            {
                return Error<TaskListDto>("Company ID not found in claims.", StatusCodes.Status401Unauthorized);
            }

            var project = await GetProjectForCompany(projectId, companyId.Value);
            if (project == null)
            {
                return Error<TaskListDto>("Project not found or not accessible.", StatusCodes.Status404NotFound);
            }

            var taskList = await GetTaskListForCompanyAndProject(id, projectId, companyId.Value);

            if (taskList == null || taskList.IsDeleted)
            {
                return Error<TaskListDto>("Task List not found or not accessible within this project.", StatusCodes.Status404NotFound);
            }

            var taskListDto = new TaskListDto
            {
                TaskListId = taskList.TaskListId,
                ListName = taskList.ListName,
                Description = taskList.Description,
                ListOrder = taskList.ListOrder,
                Status = taskList.Status,
                ProjectId = taskList.ProjectId,
                CompanyId = taskList.CompanyId,
                StartDate = taskList.StartDate,
                EndDate = taskList.EndDate
            };

            return Success("Task List retrieved successfully.", taskListDto);
        }

        // PUT: api/Tasks/projects/{projectId}/tasklists/{id}
        // This endpoint updates an existing task list within a specific project.
        [HttpPut("projects/{projectId}/tasklists/{id}")]
        [Authorize(Policy = "CompanyAdminAccess")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateTaskList(int projectId, int id, TaskListInputDto dto)
        {
            var companyId = GetCompanyIdFromClaims();
            if (!companyId.HasValue)
            {
                return Error("Company ID not found in claims.", StatusCodes.Status401Unauthorized);
            }

            var currentUserId = GetCurrentUserIdFromClaims();
            if (!currentUserId.HasValue)
            {
                return Error("User ID not found in claims.", StatusCodes.Status401Unauthorized);
            }

            var taskList = await GetTaskListForCompanyAndProject(id, projectId, companyId.Value);
            if (taskList == null || taskList.IsDeleted)
            {
                return Error("Task List not found or not accessible within this project.", StatusCodes.Status404NotFound);
            }

            if (projectId != taskList.ProjectId)
            {
                return Error("Cannot change TaskList's ProjectId via this endpoint.", StatusCodes.Status400BadRequest);
            }

            taskList.ListName = dto.ListName;
            taskList.Description = dto.Description;
            taskList.ListOrder = dto.ListOrder;
            taskList.StartDate = dto.StartDate;
            taskList.EndDate = dto.EndDate;
            taskList.UpdatedAt = DateTimeOffset.UtcNow;
            taskList.UpdatedBy = currentUserId.Value;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: api/Tasks/projects/{projectId}/tasklists/{id}
        // This endpoint performs a soft delete on a task list by setting IsDeleted to true.
        [HttpDelete("projects/{projectId}/tasklists/{id}")]
        [Authorize(Policy = "CompanyAdminAccess")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteTaskList(int projectId, int id)
        {
            var companyId = GetCompanyIdFromClaims();
            if (!companyId.HasValue)
            {
                return Error("Company ID not found in claims.", StatusCodes.Status401Unauthorized);
            }

            var currentUserId = GetCurrentUserIdFromClaims();
            if (!currentUserId.HasValue)
            {
                return Error("User ID not found in claims.", StatusCodes.Status401Unauthorized);
            }

            var taskList = await GetTaskListForCompanyAndProject(id, projectId, companyId.Value);
            if (taskList == null || taskList.IsDeleted)
            {
                return Error("Task List not found or already deleted within this project.", StatusCodes.Status404NotFound);
            }

            // Soft delete all tasks and subtasks within this task list
            var tasksToDelete = await _context.Tasks
                                             .Where(t => t.TaskListId == id && t.CompanyId == companyId.Value && !t.IsDeleted)
                                             .ToListAsync();
            foreach (var task in tasksToDelete)
            {
                task.IsDeleted = true;
                task.UpdatedAt = DateTimeOffset.UtcNow;
                task.UpdatedBy = currentUserId.Value;

                // Also soft delete documents related to these tasks
                var documentsToDelete = await _context.TaskDocuments
                                                     .Where(td => td.TaskId == task.TaskId && td.CompanyId == companyId.Value && !td.IsDeleted)
                                                     .ToListAsync();
                foreach (var document in documentsToDelete)
                {
                    document.IsDeleted = true;
                    document.UpdatedAt = DateTimeOffset.UtcNow;
                }
            }


            taskList.IsDeleted = true;
            taskList.UpdatedAt = DateTimeOffset.UtcNow;
            taskList.UpdatedBy = currentUserId.Value;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("{taskId}/documents")]
        [Consumes("multipart/form-data")] // Essential for file uploads
        [ProducesResponseType(typeof(ApiResponse<TaskDocumentUploadResponseDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<TaskDocumentUploadResponseDto>>> UploadTaskDocument(
            int taskId,
            [FromForm] TaskDocumentUploadDto dto) // Using the new TaskDocumentUploadDto
        {
            _logger.LogInformation($"Attempting to upload document for Task ID: {taskId}. Document Name: {dto.DocumentName}");

            var currentUserId = GetCurrentUserIdFromClaims();
            var companyId = GetCompanyIdFromClaims();
            var currentRoleId = GetCurrentUserRoleId();

            if (!currentUserId.HasValue || !companyId.HasValue || !currentRoleId.HasValue)
            {
                return Error<TaskDocumentUploadResponseDto>("Authentication information (UserId, CompanyId, RoleId) is missing.", StatusCodes.Status401Unauthorized);
            }

            // Define your roles (e.g., SuperAdminRoleId, CompanyAdminRoleId, EmployeeRoleId, DepartmentHeadRoleId)
            const int SuperAdminRoleId = 1;
            const int CompanyAdminRoleId = 2;
            const int EmployeeRoleId = 3;
            const int DepartmentHeadRoleId = 4;

            var task = await _context.Tasks
                                    .AsNoTracking() // Use AsNoTracking as we only need to read its properties
                                    .FirstOrDefaultAsync(t => t.TaskId == taskId && t.CompanyId == companyId.Value && !t.IsDeleted);

            if (task == null)
            {
                return Error<TaskDocumentUploadResponseDto>($"Task with ID {taskId} not found or not accessible within your company.", StatusCodes.Status404NotFound);
            }

            bool hasPermission = false;
            if (currentRoleId == SuperAdminRoleId)
            {
                hasPermission = true; // Super Admins can upload to any task
            }
            else if (currentRoleId == CompanyAdminRoleId || currentRoleId == DepartmentHeadRoleId)
            {
                // Company Admins/Department Heads can upload to tasks within their company
                hasPermission = true;
            }
            else if (currentRoleId == EmployeeRoleId)
            {
                // Employees can upload if they are assigned to the task
                if (task.AssignedTo == currentUserId.Value)
                {
                    hasPermission = true;
                }
                else
                {
                    _logger.LogWarning($"Employee (User ID: {currentUserId}) attempted to upload document to Task ID: {taskId} they are not assigned to.");
                }
            }

            if (!hasPermission)
            {
                return Error<TaskDocumentUploadResponseDto>("You do not have permission to upload documents for this task.", StatusCodes.Status403Forbidden);
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for task document upload: {Errors}", ModelState);
                return BadRequest(new ApiResponse<object>("Invalid document data provided.", "Error", StatusCodes.Status400BadRequest, ModelState));
            }

            if (dto.File == null || dto.File.Length == 0)
            {
                return Error<TaskDocumentUploadResponseDto>("No file uploaded or file is empty.", StatusCodes.Status400BadRequest);
            }

            try
            {
                var uploadedDocument = await _uploadHandler.UploadTaskDocumentAsync(
                    currentUserId.Value,
                    companyId.Value,
                    taskId,
                    dto.File,
                    dto.DocumentName,
                    dto.Description,
                    dto.Version
                );

                _logger.LogInformation($"Document '{uploadedDocument.DocumentName}' (ID: {uploadedDocument.DocumentId}) uploaded successfully for Task ID: {taskId}.");

                // Return the lightweight DTO within the ApiResponse
                var responseDto = new TaskDocumentUploadResponseDto
                {
                    DocumentId = uploadedDocument.DocumentId,
                    TaskId = uploadedDocument.TaskId,
                    DocumentName = uploadedDocument.DocumentName,
                    FilePath = uploadedDocument.FilePath,
                    DocumentType = uploadedDocument.DocumentType,
                    FileSize = uploadedDocument.FileSize,
                    Description = uploadedDocument.Description,
                    Version = uploadedDocument.Version,
                    CreatedAt = uploadedDocument.CreatedAt // Assuming CreatedAt is desired in response
                };

                return Success("Task document uploaded successfully.", responseDto, StatusCodes.Status201Created);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, $"Task document upload failed due to invalid operation for Task ID {taskId}.");
                return Error<TaskDocumentUploadResponseDto>(ex.Message, StatusCodes.Status400BadRequest);
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, $"Task document upload failed due to bad arguments for Task ID {taskId}.");
                return Error<TaskDocumentUploadResponseDto>(ex.Message, StatusCodes.Status400BadRequest);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, $"Task document upload failed due to I/O error for Task ID {taskId}.");
                return Error<TaskDocumentUploadResponseDto>($"Failed to save document due to a file system error: {ex.Message}", StatusCodes.Status500InternalServerError);
            }
            catch (DbUpdateException dbEx)
            {
                var innerException = dbEx.InnerException;
                _logger.LogError(dbEx, "Database error during task document upload for Task ID {TaskId}. Inner Exception: {InnerMessage}", taskId, innerException?.Message);
                return Error<TaskDocumentUploadResponseDto>("A database error occurred while saving document information.", StatusCodes.Status500InternalServerError);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An unexpected error occurred while uploading document for Task ID: {taskId}");
                return Error<TaskDocumentUploadResponseDto>("An unexpected error occurred while uploading the task document.", StatusCodes.Status500InternalServerError);
            }
        }
    }
}