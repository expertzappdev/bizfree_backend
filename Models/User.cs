﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BizfreeApp.Models;

[Table("users")]
public partial class User
{
    [Key]
    [Column("user_id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int UserId { get; set; }

    [Column("email")]
    [StringLength(255)]
    public string Email { get; set; } = null!; // Ensure non-nullable strings have a default or are initialized

    [Column("password_hash")]
    public string PasswordHash { get; set; } = null!;

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; }

    [Column("created_at", TypeName = "timestamp without time zone")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at", TypeName = "timestamp without time zone")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public int? UpdatedBy { get; set; }

    [Column("role_id")]
    public int? RoleId { get; set; }

    [Column("refresh_token_expiry_time")]
    public DateTime? RefreshTokenExpiryTime { get; set; }

    [Column("refresh_token")]
    public string? RefreshToken { get; set; }

    [Column("company_id")] 
    public int? CompanyId { get; set; } 

    [ForeignKey("CompanyId")]
    [InverseProperty("Users")] 
    public virtual Company Company { get; set; } 


    [InverseProperty("User")]
    public virtual ICollection<Client> Clients { get; set; } = new List<Client>();

    [InverseProperty("UpdatedByNavigation")]
    public virtual ICollection<CompanyModule> CompanyModules { get; set; } = new List<CompanyModule>();

    [InverseProperty("CreatedByNavigation")]
    public virtual ICollection<CompanyUser> CompanyUserCreatedByNavigations { get; set; } = new List<CompanyUser>();

    [InverseProperty("UpdatedByNavigation")]
    public virtual ICollection<CompanyUser> CompanyUserUpdatedByNavigations { get; set; } = new List<CompanyUser>();

    [InverseProperty("User")]
    public virtual ICollection<CompanyUser> CompanyUserUsers { get; set; } = new List<CompanyUser>();

    [InverseProperty("CreatedByNavigation")]
    public virtual ICollection<Department> DepartmentCreatedByNavigations { get; set; } = new List<Department>();

    [InverseProperty("UpdatedByNavigation")]
    public virtual ICollection<Department> DepartmentUpdatedByNavigations { get; set; } = new List<Department>();

    [InverseProperty("CreatedByNavigation")]
    public virtual ICollection<Package> PackageCreatedByNavigations { get; set; } = new List<Package>();

    [InverseProperty("UpdatedByNavigation")]
    public virtual ICollection<Package> PackageUpdatedByNavigations { get; set; } = new List<Package>();

    [InverseProperty("CreatedByNavigation")]
    public virtual ICollection<Project> ProjectCreatedByNavigations { get; set; } = new List<Project>();

    [InverseProperty("AddedByNavigation")]
    public virtual ICollection<ProjectMember> ProjectMemberAddedByNavigations { get; set; } = new List<ProjectMember>();

    [InverseProperty("User")]
    public virtual ICollection<ProjectMember> ProjectMemberUsers { get; set; } = new List<ProjectMember>();

    [InverseProperty("UpdatedByNavigation")]
    public virtual ICollection<Project> ProjectUpdatedByNavigations { get; set; } = new List<Project>();

    [ForeignKey("RoleId")]
    [InverseProperty("Users")]
    public virtual Role? Role { get; set; }

    [InverseProperty("User")]
    public virtual ICollection<TaskTimelog> TaskTimelogs { get; set; } = new List<TaskTimelog>();

    [InverseProperty("UploadedByNavigation")]
    public virtual ICollection<Taskattachment> Taskattachments { get; set; } = new List<Taskattachment>();

    [InverseProperty("User")]
    public virtual ICollection<Taskcomment> Taskcomments { get; set; } = new List<Taskcomment>();

    [InverseProperty("AssignedToNavigation")]
    public virtual ICollection<Task> Tasks { get; set; } = new List<Task>();

    [InverseProperty("CreatedByNavigation")]
    public virtual ICollection<Taskstatus> TaskstatusCreatedByNavigations { get; set; } = new List<Taskstatus>();

    [InverseProperty("UpdatedByNavigation")]
    public virtual ICollection<Taskstatus> TaskstatusUpdatedByNavigations { get; set; } = new List<Taskstatus>();

    [InverseProperty("UploadedByUser")]
    public virtual ICollection<TaskDocument> TaskDocuments { get; set; } = new List<TaskDocument>();
    [InverseProperty("UploadedByUser")]
    public virtual ICollection<ProjectDocument> ProjectDocuments { get; set; } = new List<ProjectDocument>();

    [InverseProperty("CreatedByUser")]
    public virtual ICollection<Task> TasksCreated { get; set; } = new List<Task>();

    [InverseProperty("UpdatedByUser")]
    public virtual ICollection<Task> TasksUpdated { get; set; } = new List<Task>();
}
