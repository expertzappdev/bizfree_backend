using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BizfreeApp.Data;
using BizfreeApp.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using BizfreeApp.Models.DTOs; // Ensure this namespace is correct for your DTOs
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using BizfreeApp.Dtos;
// No longer need using BizfreeApp.Dtos; or AutoMapper

namespace BizfreeApp.Controllers;

[Route("api/[controller]")] // Base route: /api/taskstatuses
[ApiController]
[Authorize] // Apply authorization for administrative access
public class TaskstatusesController : ControllerBase
{
    private readonly Data.ApplicationDbContext _context;
    private readonly ILogger<TaskstatusesController> _logger;

    public TaskstatusesController(Data.ApplicationDbContext context, ILogger<TaskstatusesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // Helper method to generate a standardized success response
    private ActionResult<ApiResponse<T>> Success<T>(string message, T? data, int statusCode = StatusCodes.Status200OK)
    {
        return StatusCode(statusCode, new ApiResponse<T>(message, "success", statusCode, data));
    }

    // Helper method to generate a standardized error response for generic ActionResult
    private ActionResult<ApiResponse<T>> Error<T>(string message, int statusCode, string status = "error")
    {
        _logger.LogError("API Error: {Message} - Status Code: {StatusCode}", message, statusCode);
        return StatusCode(statusCode, new ApiResponse<T>(message, status, statusCode, default(T)));
    }

    // Overloaded helper for non-generic IActionResult if needed (though not directly used for main actions)
    private IActionResult Error(string message, int statusCode, string status = "error")
    {
        _logger.LogError("API Error: {Message} - Status Code: {StatusCode}", message, statusCode);
        return StatusCode(statusCode, new ApiResponse<object>(message, status, statusCode, null));
    }

    // Helper to get current user ID (needs to be implemented based on your auth setup)
    private int? GetCurrentUserId()
    {
        var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId"); // Or ClaimTypes.NameIdentifier
        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int parsedUserId))
        {
            return parsedUserId;
        }
        _logger.LogWarning("UserId claim not found or could not be parsed for audit fields.");
        return null; // Return null if user ID cannot be determined
    }

    // GET: api/Taskstatuses
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<TaskstatusDto>>), StatusCodes.Status200OK)] // Change IEnumerable to List here
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<List<TaskstatusDto>>>> GetAllTaskstatuses() // Change IEnumerable to List here
    {
        try
        {
            _logger.LogInformation("Attempting to retrieve all task statuses.");
            var statuses = await _context.Taskstatuses.ToListAsync();

            // Manual mapping from Taskstatus model to TaskstatusDto
            var statusDtos = statuses.Select(s => new TaskstatusDto
            {
                StatusId = s.StatusId,
                Name = s.Name,
                StatusColor = s.StatusColor
            }).ToList(); // .ToList() returns a List<TaskstatusDto>

            _logger.LogInformation($"Successfully retrieved {statusDtos.Count()} task statuses.");
            return Success("Task statuses retrieved successfully.", statusDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while retrieving task statuses.");
            return Error<List<TaskstatusDto>>("An error occurred while retrieving task statuses.", StatusCodes.Status500InternalServerError); // Change IEnumerable to List here
        }
    }

    // GET: api/Taskstatuses/5
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<TaskstatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<TaskstatusDto>>> GetTaskstatus(int id)
    {
        try
        {
            _logger.LogInformation($"Attempting to retrieve task status with ID: {id}");
            var taskstatus = await _context.Taskstatuses.FindAsync(id);

            if (taskstatus == null)
            {
                _logger.LogWarning($"Task status with ID {id} not found.");
                return Error<TaskstatusDto>($"Task status with ID {id} not found.", StatusCodes.Status404NotFound, "not_found");
            }

            _logger.LogInformation($"Successfully retrieved task status with ID: {id}");
            // Manual mapping from Taskstatus model to TaskstatusDto
            var taskstatusDto = new TaskstatusDto
            {
                StatusId = taskstatus.StatusId,
                Name = taskstatus.Name,
                StatusColor = taskstatus.StatusColor // Assuming StatusColor exists on Taskstatus model
            };
            return Success("Task status retrieved successfully.", taskstatusDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error occurred while retrieving task status with ID: {id}");
            return Error<TaskstatusDto>($"An error occurred while retrieving task status with ID {id}.", StatusCodes.Status500InternalServerError);
        }
    }

    // POST: api/Taskstatuses
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<TaskstatusDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<TaskstatusDto>>> CreateTaskstatus([FromBody] CreateTaskstatusDto createTaskstatusDto)
    {
        try
        {
            _logger.LogInformation($"Attempting to create a new task status: {createTaskstatusDto.Name}");

            // Check for duplicate name to maintain data integrity
            if (await _context.Taskstatuses.AnyAsync(ts => ts.Name == createTaskstatusDto.Name))
            {
                _logger.LogWarning($"Attempted to create duplicate task status name: {createTaskstatusDto.Name}");
                return Error<TaskstatusDto>($"A task status with the name '{createTaskstatusDto.Name}' already exists.", StatusCodes.Status409Conflict, "conflict");
            }

            // Manual mapping from CreateTaskstatusDto to Taskstatus model
            var taskstatus = new Taskstatus
            {
                Name = createTaskstatusDto.Name,
                StatusColor = createTaskstatusDto.StatusColor, // Assuming StatusColor exists on Taskstatus model
                // Other default fields
                CreatedAt = DateTime.UtcNow,
                CreatedBy = GetCurrentUserId(),
                UpdatedAt = DateTime.UtcNow, // Set initial UpdatedAt
                UpdatedBy = GetCurrentUserId(), // Set initial UpdatedBy
            };

            _context.Taskstatuses.Add(taskstatus);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Task status '{taskstatus.Name}' created successfully with ID: {taskstatus.StatusId}");
            // Manual mapping back to TaskstatusDto for the response
            var responseDto = new TaskstatusDto
            {
                StatusId = taskstatus.StatusId,
                Name = taskstatus.Name,
                StatusColor = taskstatus.StatusColor
            };
            return Success("Task status created successfully.", responseDto, StatusCodes.Status201Created);
        }
        catch (DbUpdateException ex)
        {
            // Catch specific DB errors, e.g., unique constraint violations
            var innerException = ex.InnerException as Npgsql.PostgresException; // Assuming PostgreSQL
            if (innerException != null && innerException.SqlState == "23505") // Unique violation
            {
                _logger.LogError(ex, $"Database conflict while creating task status '{createTaskstatusDto.Name}'.");
                return Error<TaskstatusDto>($"A status with this name already exists or ID conflicts: {innerException.Message}", StatusCodes.Status409Conflict, "conflict");
            }
            _logger.LogError(ex, $"A database error occurred while creating task status '{createTaskstatusDto.Name}'.");
            return Error<TaskstatusDto>("A database error occurred while creating the task status.", StatusCodes.Status500InternalServerError);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"An unexpected error occurred while creating task status '{createTaskstatusDto.Name}'.");
            return Error<TaskstatusDto>("An unexpected error occurred while creating the task status.", StatusCodes.Status500InternalServerError);
        }
    }

    // PUT: api/Taskstatuses/5
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApiResponse<TaskstatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<TaskstatusDto>>> UpdateTaskstatus(int id, [FromBody] UpdateTaskstatusDto updateTaskstatusDto)
    {
        try
        {
            _logger.LogInformation($"Attempting to update task status with ID: {id}");

            if (id != updateTaskstatusDto.StatusId)
            {
                _logger.LogWarning($"Mismatch between URL ID ({id}) and body ID ({updateTaskstatusDto.StatusId}) for task status update.");
                return Error<TaskstatusDto>("Status ID in URL does not match ID in body.", StatusCodes.Status400BadRequest, "bad_request");
            }

            var taskstatus = await _context.Taskstatuses.FindAsync(id);
            if (taskstatus == null)
            {
                _logger.LogWarning($"Task status with ID {id} not found for update.");
                return Error<TaskstatusDto>($"Task status with ID {id} not found.", StatusCodes.Status404NotFound, "not_found");
            }

            // Check for duplicate name if the name is being changed
            if (taskstatus.Name != updateTaskstatusDto.Name && await _context.Taskstatuses.AnyAsync(ts => ts.Name == updateTaskstatusDto.Name))
            {
                _logger.LogWarning($"Attempted to update task status ID {id} to duplicate name: {updateTaskstatusDto.Name}");
                return Error<TaskstatusDto>($"A task status with the name '{updateTaskstatusDto.Name}' already exists.", StatusCodes.Status409Conflict, "conflict");
            }

            // Manual mapping from UpdateTaskstatusDto to Taskstatus model
            taskstatus.Name = updateTaskstatusDto.Name;
            taskstatus.StatusColor = updateTaskstatusDto.StatusColor; // Assuming StatusColor exists on Taskstatus model

            // Set audit fields
            taskstatus.UpdatedAt = DateTime.UtcNow;
            taskstatus.UpdatedBy = GetCurrentUserId();

            _context.Entry(taskstatus).State = EntityState.Modified;

            await _context.SaveChangesAsync();

            _logger.LogInformation($"Task status with ID: {id} updated successfully.");
            // Manual mapping back to TaskstatusDto for the response
            var responseDto = new TaskstatusDto
            {
                StatusId = taskstatus.StatusId,
                Name = taskstatus.Name,
                StatusColor = taskstatus.StatusColor
            };
            return Success("Task status updated successfully.", responseDto);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            if (!TaskstatusExists(id))
            {
                _logger.LogError(ex, $"Task status with ID {id} not found during concurrency check.");
                return Error<TaskstatusDto>($"Task status with ID {id} not found during concurrency check.", StatusCodes.Status404NotFound, "not_found");
            }
            else
            {
                _logger.LogError(ex, $"Concurrency conflict while updating task status with ID: {id}.");
                return Error<TaskstatusDto>("Concurrency error: The task status was modified by another user.", StatusCodes.Status409Conflict, "concurrency_conflict");
            }
        }
        catch (DbUpdateException ex)
        {
            var innerException = ex.InnerException as Npgsql.PostgresException;
            if (innerException != null && innerException.SqlState == "23505") // Unique violation
            {
                _logger.LogError(ex, $"Database conflict while updating task status with ID: {id}.");
                return Error<TaskstatusDto>($"A status with this name already exists or ID conflicts: {innerException.Message}", StatusCodes.Status409Conflict, "conflict");
            }
            _logger.LogError(ex, $"A database error occurred while updating task status with ID: {id}.");
            return Error<TaskstatusDto>("A database error occurred while updating the task status.", StatusCodes.Status500InternalServerError);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"An unexpected error occurred while updating task status with ID: {id}.");
            return Error<TaskstatusDto>("An unexpected error occurred while updating the task status.", StatusCodes.Status500InternalServerError);
        }
    }

    // DELETE: api/Taskstatuses/5
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteTaskstatus(int id)
    {
        try
        {
            _logger.LogInformation($"Attempting to delete task status with ID: {id}");
            var taskstatus = await _context.Taskstatuses.FindAsync(id);
            if (taskstatus == null)
            {
                _logger.LogWarning($"Task status with ID {id} not found for deletion.");
                return Error<object>($"Task status with ID {id} not found.", StatusCodes.Status404NotFound, "not_found");
            }

            // IMPORTANT: Check if status is in use by tasks before deleting
            var tasksUsingStatus = await _context.Tasks.AnyAsync(t => t.Status == id);
            if (tasksUsingStatus)
            {
                _logger.LogWarning($"Attempted to delete task status ID {id} which is in use by tasks.");
                return Error<object>("Cannot delete status as it is currently used by one or more tasks. Consider deactivating it instead.", StatusCodes.Status400BadRequest, "bad_request");
            }

            // IMPORTANT: Also check if status is in use by Projects before deleting
            var projectsUsingStatus = await _context.Projects.AnyAsync(p => p.StatusId == id);
            if (projectsUsingStatus)
            {
                _logger.LogWarning($"Attempted to delete task status ID {id} which is in use by projects.");
                return Error<object>("Cannot delete status as it is currently used by one or more projects. Consider deactivating it instead.", StatusCodes.Status400BadRequest, "bad_request");
            }

            _context.Taskstatuses.Remove(taskstatus);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Task status with ID: {id} deleted successfully.");
            return Success<object>("Task status deleted successfully.", null, StatusCodes.Status200OK);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, $"A database error occurred while deleting task status with ID: {id}.");
            return Error<object>("A database error occurred while deleting the task status.", StatusCodes.Status500InternalServerError);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"An unexpected error occurred while deleting task status with ID: {id}.");
            return Error<object>("An unexpected error occurred while deleting the task status.", StatusCodes.Status500InternalServerError);
        }
    }

    private bool TaskstatusExists(int id)
    {
        return _context.Taskstatuses.Any(e => e.StatusId == id);
    }
}