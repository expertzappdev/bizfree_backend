using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BizfreeApp.Models;

[Table("projects")]
public partial class Project
{
    [Key]
    [Column("project_id")]
    public int ProjectId { get; set; }

    [Column("company_id")]
    public int CompanyId { get; set; }

    [Column("code")]
    [StringLength(50)]
    public string? Code { get; set; }

    [Column("name")]
    [StringLength(255)]
    public string Name { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }

    [Column("status")]
    [StringLength(100)]
    public string? Status { get; set; }

    [Column("start_date")]
    public DateOnly? StartDate { get; set; }

    [Column("end_date")]
    public DateOnly? EndDate { get; set; }

    [Column("is_active")]
    public bool? IsActive { get; set; }

    [Column("is_deleted")]
    public bool? IsDeleted { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("created_by")]
    public int? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public int? UpdatedBy { get; set; }

    [Column("status_id")] // Assuming your database column is named 'status_id'
    public int? StatusId { get; set; } // Change this property type and name

    // Add this navigation property (Crucial for EF Core to link to Taskstatus)
    [ForeignKey("StatusId")]
    [InverseProperty("Projects")] // Assuming your Taskstatus model has public virtual ICollection<Project> Projects { get; set; }
    public virtual Taskstatus? StatusNavigation { get; set; }

    [ForeignKey("CompanyId")]
    [InverseProperty("Projects")]
    public virtual Company Company { get; set; } = null!;

    [ForeignKey("CreatedBy")]
    [InverseProperty("ProjectCreatedByNavigations")]
    public virtual User? CreatedByNavigation { get; set; }

    [InverseProperty("Project")]
    public virtual ICollection<ProjectMember> ProjectMembers { get; set; } = new List<ProjectMember>();

    [InverseProperty("Project")]
    public virtual ICollection<Task> Tasks { get; set; } = new List<Task>();

    [ForeignKey("UpdatedBy")]
    [InverseProperty("ProjectUpdatedByNavigations")]
    public virtual User? UpdatedByNavigation { get; set; }

    [InverseProperty("Project")]
    public virtual ICollection<ProjectDocument> ProjectDocuments { get; set; } = new List<ProjectDocument>();

    // Add this to your Project model
    [InverseProperty("Project")]
    public virtual ICollection<TaskList> TaskLists { get; set; } = new List<TaskList>();

    // Non-mapped field for Progression
    [NotMapped]
    public int Progression
    {
        get
        {
            // Use a static Random instance to avoid getting the same sequence of numbers
            // if this property is accessed rapidly.
            // For a production application, consider a more robust way to manage Random instances.
            return _random.Next(0, 101); // 0 to 100 inclusive
        }
    }

    private static readonly Random _random = new Random();
}