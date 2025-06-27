namespace BizfreeApp.Models.DTOs
{
    public class ProjectUpdateDto
    {
        // Fields that can be updated
        public string? Name { get; set; } // Can be nullable if update doesn't require all fields
        public string? Status { get; set; }
        public string? Description { get; set; }
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }

        // Note: CompanyId is usually NOT updated after creation.
        // If you need to allow it, uncomment this and add validation in the controller.
        // public int? CompanyId { get; set; }
    }
}
