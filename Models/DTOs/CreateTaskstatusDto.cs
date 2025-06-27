using System.ComponentModel.DataAnnotations;

namespace BizfreeApp.Dtos;

public class CreateTaskstatusDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = null!;
    public string? StatusColor { get; set; }    // No CompanyId needed
    // If you add a ColorCode, include it here:
    // public string? ColorCode { get; set; }
}