using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BizfreeApp.Models; 
using BizfreeApp.Models.DTOs; 
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System; 
using System.Linq.Expressions; 

namespace BizfreeApp.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TimelogsController : ControllerBase
{
    private readonly Data.ApplicationDbContext _context;

    public TimelogsController(Data.ApplicationDbContext context)
    {
        _context = context;
    }

    protected ActionResult<ApiResponse<T>> Success<T>(string message, T? data = default, int statusCode = StatusCodes.Status200OK)
    {
        return Ok(new ApiResponse<T>(message, "Success", statusCode, data));
    }

    protected ActionResult<ApiResponse<T>> Error<T>(string message, int statusCode = StatusCodes.Status400BadRequest, object? errorDetails = null)
    {
        var apiResponse = new ApiResponse<T>(message, "Error", statusCode, default, errorDetails);

        if (statusCode == StatusCodes.Status400BadRequest)
        {
            return BadRequest(apiResponse);
        }
        else if (statusCode == StatusCodes.Status404NotFound)
        {
            return NotFound(apiResponse);
        }
        else
        {
            return StatusCode(statusCode, apiResponse);
        }
    }

    protected IActionResult Error(string message, int statusCode = StatusCodes.Status400BadRequest, object? errorDetails = null)
    {
        var apiResponse = new ApiResponse<object>(message, "Error", statusCode, default, errorDetails);

        if (statusCode == StatusCodes.Status400BadRequest)
        {
            return BadRequest(apiResponse);
        }
        else if (statusCode == StatusCodes.Status404NotFound)
        {
            return NotFound(apiResponse);
        }
        else
        {
            return StatusCode(statusCode, apiResponse);
        }
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TaskTimelog>>> GetTaskTimelogs()
    {
        return await _context.TaskTimelogs.ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TaskTimelog>> GetTaskTimelog(int id)
    {
        var taskTimelog = await _context.TaskTimelogs.FindAsync(id);

        if (taskTimelog == null)
        {
            return NotFound();
        }

        return taskTimelog;
    }

    [HttpGet("filtered")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<TaskTimelog>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<TaskTimelog>>>> GetFilteredTimelogs(
        [FromQuery] int? userId,
        [FromQuery] int? projectId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 5,
        [FromQuery] string? sortBy = "loggedat",
        [FromQuery] string? sortOrder = "desc",
        [FromQuery] string? searchKeyword = null,
        [FromQuery] int? statusId = null,
        [FromQuery] int? priorityId = null,
        [FromQuery] int? assignedToUserId = null,
        [FromQuery] DateOnly? dueDateFrom = null,
        [FromQuery] DateOnly? dueDateTo = null)
    {

        IQueryable<TaskTimelog> query = _context.TaskTimelogs.Include(tt => tt.Task);

        DateTime currentLocalTime = DateTime.Now;
        DateTime endDate = currentLocalTime.Date.AddDays(1).AddTicks(-1); // End of today
        DateTime startDate = currentLocalTime.Date.AddDays(-30); // Start of 30 days ago

        query = query.Where(tt => tt.LoggedAt.Date >= startDate && tt.LoggedAt.Date <= endDate);

        if (userId.HasValue)
        {
            query = query.Where(tt => tt.UserId == userId.Value);
        }

        if (projectId.HasValue)
        {
            query = query.Where(tt => tt.Task != null && tt.Task.ProjectId == projectId.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchKeyword))
        {
            string lowerCaseSearchKeyword = searchKeyword.ToLower();
            query = query.Where(tt =>
                (tt.Description != null && tt.Description.ToLower().Contains(lowerCaseSearchKeyword)) ||
                (tt.Task != null && tt.Task.Title != null && tt.Task.Title.ToLower().Contains(lowerCaseSearchKeyword)) ||
                (tt.Task != null && tt.Task.Description != null && tt.Task.Description.ToLower().Contains(lowerCaseSearchKeyword))
            );
        }

        if (statusId.HasValue)
        {
            query = query.Where(tt => tt.Task != null && tt.Task.Status == statusId.Value);
        }

        if (priorityId.HasValue)
        {
            query = query.Where(tt => tt.Task != null && tt.Task.PriorityId == priorityId.Value);
        }

        if (assignedToUserId.HasValue)
        {
            query = query.Where(tt => tt.Task != null && tt.Task.AssignedTo == assignedToUserId.Value);
        }

        if (dueDateFrom.HasValue)
        {
            query = query.Where(tt => tt.Task != null && tt.Task.DueDate.HasValue && tt.Task.DueDate.Value >= dueDateFrom.Value);
        }

        if (dueDateTo.HasValue)
        {
            query = query.Where(tt => tt.Task != null && tt.Task.DueDate.HasValue && tt.Task.DueDate.Value <= dueDateTo.Value);
        }

        int totalCount = await query.CountAsync();

        var sortPropertyMap = new Dictionary<string, Expression<Func<TaskTimelog, object>>>
        {
            { "id", tt => tt.Id },
            { "taskid", tt => tt.TaskId! },
            { "userid", tt => tt.UserId! },
            { "loggedat", tt => tt.LoggedAt },
            { "duration", tt => tt.Duration! },
            { "description", tt => tt.Description! },
            { "tasktitle", tt => tt.Task!.Title! },
            { "taskstatusname", tt => tt.Task!.StatusNavigation!.Name! }, 
            { "taskpriorityname", tt => tt.Task!.Priority!.Name! },      
            { "taskassignedtouserid", tt => tt.Task!.AssignedTo! },
            { "taskduedate", tt => tt.Task!.DueDate! }
        };

        if (!string.IsNullOrWhiteSpace(sortBy) && sortPropertyMap.TryGetValue(sortBy.ToLower(), out var sortExpression))
        {
            query = (sortOrder?.ToLower() == "desc") ? query.OrderByDescending(sortExpression) : query.OrderBy(sortExpression);
        }
        else
        {
            query = query.OrderBy(tt => tt.Id); 
        }
        var items = await query.Skip((pageNumber - 1) * pageSize)
                               .Take(pageSize)
                               .Select(tt => new TaskTimelog 
                               {
                                   Id = tt.Id,
                                   TaskId = tt.TaskId,
                                   UserId = tt.UserId,
                                   LoggedAt = tt.LoggedAt,
                                   Duration = tt.Duration,
                                   Description = tt.Description
                               })
                               .ToListAsync();

        int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var pagedResult = new PagedResult<TaskTimelog>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalPages = totalPages,
            TotalTasks = totalCount, 
            SortBy = sortBy,
            SortOrder = sortOrder,
            SearchKeyword = searchKeyword
        };

        return Success("Timelogs retrieved successfully.", pagedResult);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<TaskTimelog>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<TaskTimelog>>> PostTaskTimelog(TaskTimelog taskTimelog)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(taskTimelog.Duration ?? "", @"^(?:2[0-3]|[01]?[0-9]):[0-5][0-9]$"))
        {
            return Error<TaskTimelog>("Duration must be in HH:MM format (e.g., '08:30').", StatusCodes.Status400BadRequest);
        }

        taskTimelog.LoggedAt = DateTime.Now;

        _context.TaskTimelogs.Add(taskTimelog);
        await _context.SaveChangesAsync();

        return Success("Timelog created successfully.", taskTimelog, StatusCodes.Status201Created);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PutTaskTimelog(int id, TaskTimelog taskTimelog)
    {
        if (id != taskTimelog.Id)
        {
            return Error("ID mismatch.", StatusCodes.Status400BadRequest); 
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(taskTimelog.Duration ?? "", @"^(?:2[0-3]|[01]?[0-9]):[0-5][0-9]$"))
        {
            return Error("Duration must be in HH:MM format (e.g., '08:30').", StatusCodes.Status400BadRequest); 
        }

        var existingTimelog = await _context.TaskTimelogs.FindAsync(id);

        if (existingTimelog == null)
        {
            return Error("Timelog not found.", StatusCodes.Status404NotFound); 
        }


        existingTimelog.TaskId = taskTimelog.TaskId;
        existingTimelog.UserId = taskTimelog.UserId;
        existingTimelog.Duration = taskTimelog.Duration;
        existingTimelog.Description = taskTimelog.Description;
        existingTimelog.LoggedAt = DateTime.Now;

        _context.Entry(existingTimelog).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!TaskTimelogExists(id))
            {
                return Error("Timelog not found after concurrent update.", StatusCodes.Status404NotFound);
            }
            else
            {
                throw; 
            }
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTaskTimelog(int id)
    {
        var taskTimelog = await _context.TaskTimelogs.FindAsync(id);
        if (taskTimelog == null)
        {
            return Error("Timelog not found.", StatusCodes.Status404NotFound);
        }

        _context.TaskTimelogs.Remove(taskTimelog);
        await _context.SaveChangesAsync();

        return NoContent(); 
    }

    private bool TaskTimelogExists(int id)
    {
        return _context.TaskTimelogs.Any(e => e.Id == id);
    }
}