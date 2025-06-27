using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BizfreeApp.Models;

[Table("taskstatus")]
public partial class Taskstatus
{
    [Key]
    [Column("status_id")]
    public int StatusId { get; set; }

    [Column("name")]
    [StringLength(100)]
    public string? Name { get; set; }

    // --- NEW PROPERTY ADDITION ---
    [Column("status_color")]
    [StringLength(50)]
    public string? StatusColor { get; set; }
    // --- END NEW PROPERTY ADDITION ---

    [Column("created_by")]
    public int? CreatedBy { get; set; }

    [Column("created_at", TypeName = "timestamp without time zone")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at", TypeName = "timestamp without time zone")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public int? UpdatedBy { get; set; }

    [ForeignKey("CreatedBy")]
    [InverseProperty("TaskstatusCreatedByNavigations")]
    public virtual User? CreatedByNavigation { get; set; }

    [InverseProperty("StatusNavigation")]
    public virtual ICollection<Task> Tasks { get; set; } = new List<Task>();

    [ForeignKey("UpdatedBy")]
    [InverseProperty("TaskstatusUpdatedByNavigations")]
    public virtual User? UpdatedByNavigation { get; set; }

    [InverseProperty("StatusNavigation")] // Ensure this points to the new navigation property in Project
    public virtual ICollection<Project> Projects { get; set; } = new List<Project>();
}