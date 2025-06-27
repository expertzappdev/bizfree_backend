using System.ComponentModel.DataAnnotations;

namespace BizfreeApp.Dtos;

public class UpdateTaskstatusDto
{
    [Required]
    public int StatusId { get; set; }
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = null!;
    public string? StatusColor { get; set; }
}