using System;
using System.Collections.Generic;
using BizfreeApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BizfreeApp.Data;

public partial class ApplicationDbContext : DbContext
{
    public ApplicationDbContext()
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Client> Clients { get; set; }
    public virtual DbSet<Company> Companies { get; set; }
    public virtual DbSet<CompanyModule> CompanyModules { get; set; }
    public virtual DbSet<CompanyUser> CompanyUsers { get; set; }
    public virtual DbSet<Department> Departments { get; set; }
    public virtual DbSet<Module> Modules { get; set; }
    public virtual DbSet<Package> Packages { get; set; }
    public virtual DbSet<Packagemodule> Packagemodules { get; set; }
    public virtual DbSet<Permission> Permissions { get; set; }
    public virtual DbSet<Project> Projects { get; set; } // Ensure this DbSet is present
    public virtual DbSet<ProjectDocument> ProjectDocuments { get; set; }
    public virtual DbSet<ProjectMember> ProjectMembers { get; set; }
    public virtual DbSet<Role> Roles { get; set; }
    public virtual DbSet<Rolespermission> Rolespermissions { get; set; }
    public virtual DbSet<BizfreeApp.Models.Task> Tasks { get; set; }
    public virtual DbSet<TaskTimelog> TaskTimelogs { get; set; }
    public virtual DbSet<Taskattachment> Taskattachments { get; set; }
    public virtual DbSet<Taskcomment> Taskcomments { get; set; }
    public virtual DbSet<TaskDocument> TaskDocuments { get; set; }
    public virtual DbSet<TaskList> TaskLists { get; set; }
    public virtual DbSet<Taskpriority> Taskpriorities { get; set; }
    public virtual DbSet<Taskstatus> Taskstatuses { get; set; }
    public virtual DbSet<User> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseNpgsql("Host=localhost;Database=Bizfree Db;Username=postgres;Password=123456");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        modelBuilder.HasPostgresExtension("uuid-ossp");

        modelBuilder.Entity<Client>(entity =>
        {
            entity.HasKey(e => e.ClientId).HasName("client_pkey");
            entity.Property(e => e.Createdat).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.IsMultiple).HasDefaultValue(false);
            entity.HasOne(d => d.Company).WithMany(p => p.Clients)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_company");
            entity.HasOne(d => d.User).WithMany(p => p.Clients)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_user");
        });

        modelBuilder.Entity<Company>(entity =>
        {
            entity.HasKey(e => e.CompanyId).HasName("companies_pkey");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasOne(d => d.Package).WithMany(p => p.Companies).HasConstraintName("fk_package");
        });

        modelBuilder.Entity<CompanyModule>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("company_modules_pkey");
            entity.Property(e => e.Enabled).HasDefaultValue(true);
            entity.Property(e => e.EnabledAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasOne(d => d.Company).WithMany(p => p.CompanyModules)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_company");
            entity.HasOne(d => d.Module).WithMany(p => p.CompanyModules)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_module");
            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.CompanyModules).HasConstraintName("fk_updated_by");
        });

        modelBuilder.Entity<CompanyUser>(entity =>
        {
            entity.HasKey(e => e.EmployeeId).HasName("company_users_pkey");
            entity.ToTable("company_users");
            entity.HasIndex(e => e.EmployeeCode, "company_users_employee_code_key").IsUnique();
            entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
            entity.Property(e => e.Address).HasColumnName("address");
            entity.Property(e => e.AnniversaryDate).HasColumnName("anniversary_date");
            entity.Property(e => e.CheckAdmin).HasColumnName("check_admin");
            entity.Property(e => e.CompanyId).HasColumnName("company_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DateOfBirth).HasColumnName("date_of_birth");
            entity.Property(e => e.DepartmentId).HasColumnName("department_id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.EmployeeCode)
                .HasMaxLength(100)
                .HasColumnName("employee_code");
            entity.Property(e => e.EmploymentType)
                .HasMaxLength(100)
                .HasColumnName("employment_type");
            entity.Property(e => e.FirstName)
                .HasMaxLength(255)
                .HasColumnName("first_name");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.IsDeleted)
                .HasDefaultValue(false)
                .HasColumnName("is_deleted");
            entity.Property(e => e.JoiningDate).HasColumnName("joining_date");
            entity.Property(e => e.LastName)
                .HasMaxLength(255)
                .HasColumnName("last_name");
            entity.Property(e => e.MaritalStatus)
                .HasMaxLength(50)
                .HasColumnName("marital_status");
            entity.Property(e => e.ProfilePhotoUrl).HasColumnName("profile_photo_url");
            entity.Property(e => e.ReportToUserId).HasColumnName("report_to_user_id");
            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.HasOne(d => d.CheckAdminNavigation).WithMany(p => p.CompanyUserCheckAdminNavigations)
                .HasPrincipalKey(p => p.CheckAdmin)
                .HasForeignKey(d => d.CheckAdmin)
                .HasConstraintName("fk_check_admin");
            entity.HasOne(d => d.ReportToUser).WithMany(p => p.InverseReportToUser)
                .HasForeignKey(d => d.ReportToUserId)
                .HasConstraintName("fk_report_to_user");
            entity.HasOne(d => d.Role).WithMany(p => p.CompanyUserRoles)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_role");
        });

        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasKey(e => e.DeptId).HasName("department_pkey");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasOne(d => d.Company).WithMany(p => p.Departments)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_company");
            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.DepartmentCreatedByNavigations).HasConstraintName("fk_created_by");
            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.DepartmentUpdatedByNavigations).HasConstraintName("fk_updated_by");
        });

        modelBuilder.Entity<Module>(entity =>
        {
            entity.HasKey(e => e.ModuleId).HasName("modules_pkey");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<Package>(entity =>
        {
            entity.HasKey(e => e.PackageId).HasName("packages_pkey");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.TrialDays).HasDefaultValue(0);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.PackageCreatedByNavigations).HasConstraintName("fk_created_by");
            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.PackageUpdatedByNavigations).HasConstraintName("fk_updated_by");
        });

        modelBuilder.Entity<Packagemodule>(entity =>
        {
            entity.HasKey(e => new { e.PackageId, e.ModuleId }).HasName("packagemodule_pkey");
            entity.HasOne(d => d.Module).WithMany(p => p.Packagemodules)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_module");
            entity.HasOne(d => d.Package).WithMany(p => p.Packagemodules)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_package");
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasKey(e => e.PermissionId).HasName("permissions_pkey");
            entity.HasOne(d => d.Module).WithMany(p => p.Permissions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_module");
        });

        // --- START Project Model Configuration (with StatusNavigation) ---
        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.ProjectId).HasName("projects_pkey");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasOne(d => d.Company).WithMany(p => p.Projects)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_company");
            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ProjectCreatedByNavigations).HasConstraintName("fk_created_by");
            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.ProjectUpdatedByNavigations).HasConstraintName("fk_updated_by");

            // New relationship for Project Status
            entity.HasOne(d => d.StatusNavigation) // A Project has one Status
                  .WithMany(p => p.Projects)      // A Taskstatus can apply to many Projects
                  .HasForeignKey(d => d.StatusId)   // The foreign key column is StatusId
                  .IsRequired(false)              // StatusId is nullable
                  .OnDelete(DeleteBehavior.Restrict) // Or .SetNull, .NoAction, etc. based on business logic
                  .HasConstraintName("fk_project_status_id"); // Optional: Give your constraint a name
        });
        // --- END Project Model Configuration ---

        modelBuilder.Entity<ProjectMember>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("project_members_pkey");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.JoinedAt).HasDefaultValueSql("CURRENT_DATE");
            entity.HasOne(d => d.AddedByNavigation).WithMany(p => p.ProjectMemberAddedByNavigations).HasConstraintName("fk_added_by");
            entity.HasOne(d => d.Project).WithMany(p => p.ProjectMembers)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_project");
            entity.HasOne(d => d.User).WithMany(p => p.ProjectMemberUsers)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_user");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("roles_pkey");
            entity.ToTable("roles");
            entity.HasIndex(e => e.CheckAdmin, "uq_roles_check_admin").IsUnique();
            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.CheckAdmin)
                .IsRequired()
                .HasColumnName("check_admin");
            entity.Property(e => e.CompanyId).HasColumnName("company_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("created_at");
            entity.Property(e => e.RoleName)
                .HasMaxLength(100)
                .HasColumnName("role_name");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<Rolespermission>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("rolespermissions_pkey");
            entity.HasOne(d => d.Company).WithMany(p => p.Rolespermissions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_company");
            entity.HasOne(d => d.Permission).WithMany(p => p.Rolespermissions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_permission");
            entity.HasOne(d => d.Role).WithMany(p => p.Rolespermissions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_role");
        });

        // --- START Task Model Configuration (with ParentTask self-reference) ---
        modelBuilder.Entity<BizfreeApp.Models.Task>(entity =>
        {
            entity.HasKey(e => e.TaskId).HasName("tasks_pkey");
            entity.Property(e => e.TaskId).HasColumnName("task_id").ValueGeneratedOnAdd();

            entity.HasOne(d => d.AssignedToNavigation).WithMany(p => p.Tasks).HasConstraintName("tasks_assigned_to_fkey");
            entity.HasOne(d => d.Company).WithMany(p => p.Tasks)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("tasks_company_id_fkey");
            entity.HasOne(d => d.Priority).WithMany(p => p.Tasks).HasConstraintName("tasks_priority_id_fkey");
            entity.HasOne(d => d.Project).WithMany(p => p.Tasks)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("tasks_project_id_fkey");
            entity.HasOne(d => d.StatusNavigation).WithMany(p => p.Tasks).HasConstraintName("tasks_status_fkey");

            // Self-referencing relationship for ParentTask/SubTasks
            entity.HasOne(d => d.ParentTask)      // A Task (subtask) has one ParentTask
                  .WithMany(p => p.SubTasks)      // A ParentTask can have many SubTasks
                  .HasForeignKey(d => d.ParentTaskId) // The foreign key column is ParentTaskId
                  .IsRequired(false)              // ParentTaskId is nullable
                  .OnDelete(DeleteBehavior.SetNull) // When a parent task is deleted, set ParentTaskId to NULL for its subtasks
                  .HasConstraintName("fk_task_parent_task_id"); // Optional: Give your constraint a name
        });
        // --- END Task Model Configuration ---

        //modelBuilder.Entity<TaskTimelog>(entity =>
        //{
        //    entity.HasKey(e => e.Id).HasName("task_timelogs_pkey");
        //    entity.HasOne(d => d.Task).WithMany(p => p.TaskTimelogs)
        //        .OnDelete(DeleteBehavior.Cascade)
        //        .HasConstraintName("task_timelogs_task_id_fkey");
        //    entity.HasOne(d => d.User).WithMany(p => p.TaskTimelogs).HasConstraintName("task_timelogs_user_id_fkey");
        //});

        modelBuilder.Entity<TaskTimelog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("task_timelogs_pkey");

            entity.Property(e => e.LoggedAt)
                  .ValueGeneratedOnAdd();

            // Removed: Soft delete configuration for IsDeleted and global query filter
            // entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            // entity.HasQueryFilter(e => !e.IsDeleted);

            entity.HasOne(d => d.Task)
                  .WithMany(p => p.TaskTimelogs)
                  .HasForeignKey(d => d.TaskId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("task_timelogs_task_id_fkey");

            entity.HasOne(d => d.User)
                  .WithMany(p => p.TaskTimelogs)
                  .HasForeignKey(d => d.UserId)
                  .HasConstraintName("task_timelogs_user_id_fkey");
        });

        modelBuilder.Entity<Taskattachment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("taskattachment_pkey");
            entity.Property(e => e.UploadedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasOne(d => d.Task).WithMany(p => p.Taskattachments)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("taskattachment_task_id_fkey");
            entity.HasOne(d => d.UploadedByNavigation).WithMany(p => p.Taskattachments).HasConstraintName("taskattachment_uploaded_by_fkey");
        });

        modelBuilder.Entity<Taskcomment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("taskcomment_pkey");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasOne(d => d.ParentComment).WithMany(p => p.InverseParentComment).HasConstraintName("taskcomment_parent_comment_id_fkey");
            entity.HasOne(d => d.Task).WithMany(p => p.Taskcomments)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("taskcomment_task_id_fkey");
            entity.HasOne(d => d.User).WithMany(p => p.Taskcomments).HasConstraintName("taskcomment_user_id_fkey");
        });

        // Taskpriority entity configuration: No specific Fluent API change needed for rename if [Column] is used.
        // Keeping scaffolded configuration for it.
        modelBuilder.Entity<Taskpriority>(entity =>
        {
            entity.HasKey(e => e.PriorityId).HasName("taskpriorities_pkey");
            // Your existing scaffolded configurations (if any)
        });

        modelBuilder.Entity<TaskList>(entity =>
        {
            entity.ToTable("task_lists");
            entity.HasKey(e => e.TaskListId);
            entity.Property(e => e.TaskListId)
                    .HasColumnName("task_list_id")
                    .ValueGeneratedOnAdd();
            entity.Property(e => e.ListName)
                    .HasColumnName("list_name")
                    .IsRequired()
                    .HasMaxLength(255);
            entity.Property(e => e.Description)
                    .HasColumnName("description")
                    .HasMaxLength(1000);
            entity.Property(e => e.ListOrder)
                    .HasColumnName("list_order");
        });

        // Taskstatus entity configuration: No specific Fluent API change needed for new column if [Column] is used.
        // Keeping scaffolded configuration for it.
        modelBuilder.Entity<Taskstatus>(entity =>
        {
            entity.HasKey(e => e.StatusId).HasName("taskstatus_pkey");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.TaskstatusCreatedByNavigations).HasConstraintName("taskstatus_created_by_fkey");
            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.TaskstatusUpdatedByNavigations).HasConstraintName("taskstatus_updated_by_fkey");
        });

        //modelBuilder.Entity<User>(entity =>
        //{
        //    entity.HasKey(e => e.UserId).HasName("users_pkey");
        //    entity.Property(e => e.UserId).ValueGeneratedNever();
        //    entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        //    entity.Property(e => e.IsActive).HasDefaultValue(true);
        //    entity.Property(e => e.IsDeleted).HasDefaultValue(false);
        //    entity.HasOne(d => d.Role).WithMany(p => p.Users).HasConstraintName("fk_role");
        //});

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("users_pkey");
            entity.Property(e => e.UserId).ValueGeneratedOnAdd();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);

            entity.HasOne(d => d.Role)
                  .WithMany(p => p.Users)
                  .HasForeignKey(d => d.RoleId)
                  .HasConstraintName("fk_role");

            entity.HasOne(d => d.Company)
                  .WithMany(p => p.Users)
                  .HasForeignKey(d => d.CompanyId)
                  .HasConstraintName("fk_user_company")
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // You will likely need configuration for the CompanyUser entity itself here,
        // defining its primary key, relationships, and any other column mappings.
        // Example (this is a guess, you need to match your actual CompanyUser model):
        //modelBuilder.Entity<CompanyUser>(entity =>
        //{
        //    entity.HasKey(e => e.Id); // Assuming 'Id' is the primary key for CompanyUser

        //    // Define relationships if CompanyUser links back to User and Company
        //    // Example: CompanyUser has one User
        //    entity.HasOne(d => d.User) // Assuming CompanyUser has a 'User' navigation property
        //          .WithMany(p => p.CompanyUserUsers) // User's collection of CompanyUsers
        //          .HasForeignKey(d => d.UserId) // Assuming CompanyUser has a 'UserId' foreign key
        //          .HasConstraintName("fk_companyuser_user")
        //          .OnDelete(DeleteBehavior.Cascade); // Adjust delete behavior as needed

        //    // Example: CompanyUser has one Company
        //    entity.HasOne(d => d.Company) // Assuming CompanyUser has a 'Company' navigation property
        //          .WithMany(p => p.CompanyUserUsers) // Company's collection of CompanyUsers (if exists)
        //                                             // OR it could be WithMany() if Company doesn't have a direct collection to CompanyUser
        //          .HasForeignKey(d => d.CompanyId) // Assuming CompanyUser has a 'CompanyId' foreign key
        //          .HasConstraintName("fk_companyuser_company")
        //          .OnDelete(DeleteBehavior.Cascade); // Adjust delete behavior as needed

        //    // Other properties specific to CompanyUser like FirstName, LastName, EmployeeCode, ProfilePhotoUrl, etc.
        //    // You might map columns here if they differ from property names.
        //    // Example:
        //    // entity.Property(e => e.FirstName).HasColumnName("first_name");
        //    // entity.Property(e => e.LastName).HasColumnName("last_name");
        //    // entity.Property(e => e.EmployeeCode).HasColumnName("employee_code");
        //});


        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}