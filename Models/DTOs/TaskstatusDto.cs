namespace BizfreeApp.Dtos;

public class TaskstatusDto
{
    public int StatusId { get; set; }
    public string? Name { get; set; }
    public string? StatusColor { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? CreatedAt { get; set; }
    public int? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    // If you add a ColorCode, include it here:
    // public string? ColorCode { get; set; }
}