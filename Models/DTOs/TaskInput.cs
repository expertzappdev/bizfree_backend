using System.ComponentModel.DataAnnotations;

namespace BizfreeApp.Models.DTOs
{
    public class TaskInputDto
    {
        [Required]
        [StringLength(255)]
        public string Title { get; set; } = null!;
        public int StatusId { get; set; }
        public DateOnly? DueDate { get; set; }
        public int? PriorityId { get; set; }
        public string? Description { get; set; }
        public int? TaskListId { get; set; } // Include if you allow moving tasks between lists on update
        public int? AssignedToUserId { get; set; } // Add this if you want to set assignment
        public decimal? EstimatedHours { get; set; }
        public decimal? ActualHours { get; set; }
        public int? TaskOrder { get; set; }
        public int? ParentTaskId { get; set; }

    }

}
