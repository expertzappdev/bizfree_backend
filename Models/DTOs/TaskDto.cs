namespace BizfreeApp.Models.DTOs
{
    public class TaskDto
    {
        public int TaskId { get; set; }
        public string? Title { get; set; }
        public string? StatusName { get; set; }
        public DateOnly? DueDate { get; set; }
        public string? PriorityName { get; set; }
        public string? Description { get; set; }
        public int? AssignedToUserId { get; set; } // Add this
        public string? AssignedToUserName { get; set; } // Add this

        public int? TaskListId { get; set; }
        public string? ListName { get; set; }
        public string? TaskListDescription { get; set; }
        public int? ListOrder { get; set; }
        public int? ProjectId { get; set; } // Added for completeness
        public string? CompanyName { get; set; }
        public int? ParentTaskId { get; set; } // Added this line for Parent Task ID

        public List<TaskDto>? Subtasks { get; set; }
        public List<TaskDocumentDto>? Documents { get; set; }


    }
}
