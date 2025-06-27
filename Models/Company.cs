using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BizfreeApp.Models;

[Table("companies")]
[Index("CompanyEmail", Name = "companies_company_email_key", IsUnique = true)]
public partial class Company
{
    [Key]
    [Column("company_id")]
    public int CompanyId { get; set; }

    [Column("company_name")]
    [StringLength(255)]
    public string CompanyName { get; set; } = null!;

    [Column("company_address")]
    public string? CompanyAddress { get; set; }

    [Column("company_email")]
    [StringLength(255)]
    public string? CompanyEmail { get; set; }

    [Column("company_phone")]
    public long? CompanyPhone { get; set; }

    [Column("company_url")]
    [StringLength(255)]
    public string? CompanyUrl { get; set; }

    [Column("company_logo_url")]
    public string? CompanyLogoUrl { get; set; }

    [Column("is_active")]
    public bool? IsActive { get; set; }

    [Column("admin_user_id")]
    public int? AdminUserId { get; set; }

    [Column("package_id")]
    public int? PackageId { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [InverseProperty("Company")]
    public virtual ICollection<Client> Clients { get; set; } = new List<Client>();

    [InverseProperty("Company")]
    public virtual ICollection<CompanyModule> CompanyModules { get; set; } = new List<CompanyModule>();

    [InverseProperty("Company")]
    public virtual ICollection<CompanyUser> CompanyUsers { get; set; } = new List<CompanyUser>();

    [InverseProperty("Company")]
    public virtual ICollection<Department> Departments { get; set; } = new List<Department>();

    [ForeignKey("PackageId")]
    [InverseProperty("Companies")]
    public virtual Package? Package { get; set; }

    [InverseProperty("Company")]
    public virtual ICollection<Project> Projects { get; set; } = new List<Project>();

    [InverseProperty("Company")]
    public virtual ICollection<Role> Roles { get; set; } = new List<Role>();

    [InverseProperty("Company")]
    public virtual ICollection<User> Users { get; set; } = new List<User>();

    [InverseProperty("Company")]
    public virtual ICollection<Rolespermission> Rolespermissions { get; set; } = new List<Rolespermission>();

    //[InverseProperty("Company")]
    //public virtual ICollection<Taskpriority> Taskpriorities { get; set; } = new List<Taskpriority>();

    [InverseProperty("Company")]
    public virtual ICollection<Task> Tasks { get; set; } = new List<Task>();

    //[InverseProperty("Company")]
    //public virtual ICollection<Taskstatus> Taskstatuses { get; set; } = new List<Taskstatus>();

    [InverseProperty("Company")]
    public virtual ICollection<TaskDocument> TaskDocuments { get; set; } = new List<TaskDocument>();
    [InverseProperty("Company")]
    public virtual ICollection<ProjectDocument> ProjectDocuments { get; set; } = new List<ProjectDocument>();
}
