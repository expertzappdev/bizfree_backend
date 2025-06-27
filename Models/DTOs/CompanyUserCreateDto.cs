using System;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions; // Required for RegularExpressionAttribute

namespace BizfreeApp.DTOs;

public class CompanyUserCreateDto
{
    // User-related properties for creating the associated User record
    [Required(ErrorMessage = "User email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters.")]
    public string UserEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "User password hash is required.")]
    [StringLength(500, MinimumLength = 6, ErrorMessage = "Password hash must be between 6 and 500 characters.")]
    public string UserPasswordHash { get; set; } = string.Empty;

    [Required(ErrorMessage = "User Role ID is required for the associated User record.")]
    public int UserRoleId { get; set; } // Role for the User table (e.g., general application user)

    // CompanyUser-related properties
    [Required(ErrorMessage = "Role ID is required for the Company User.")]
    public int RoleId { get; set; } // Role specific to the CompanyUser's role within the company

    public int? DepartmentId { get; set; }

    // Removed IsActive as requested. The controller logic will need to set this explicitly (e.g., true by default).
    // public bool? IsActive { get; set; } = true;

    [StringLength(100, ErrorMessage = "Employment type cannot exceed 100 characters.")]
    public string? EmploymentType { get; set; }

    [StringLength(500, ErrorMessage = "Address cannot exceed 500 characters.")]
    public string? Address { get; set; }

    [StringLength(255, ErrorMessage = "Address Line 1 cannot exceed 255 characters.")]
    public string? AddressLine1 { get; set; } // Explicitly kept

    [StringLength(100, ErrorMessage = "City cannot exceed 100 characters.")]
    public string? City { get; set; } // Explicitly kept

    [StringLength(100, ErrorMessage = "State cannot exceed 100 characters.")]
    public string? State { get; set; } // Explicitly kept

    [StringLength(20, ErrorMessage = "Postal Code cannot exceed 20 characters.")]
    public string? PostalCode { get; set; } // Explicitly kept

    [StringLength(100, ErrorMessage = "Country cannot exceed 100 characters.")]
    public string? Country { get; set; } // Newly added to DTO

    [StringLength(20, ErrorMessage = "Gender cannot exceed 20 characters.")]
    public string? Gender { get; set; }

    [StringLength(10, ErrorMessage = "Blood Group cannot exceed 10 characters.")]
    public string? BloodGroup { get; set; }

    [StringLength(50, ErrorMessage = "Phone number cannot exceed 50 characters.")]
    [RegularExpression(@"^[0-9]+$", ErrorMessage = "Phone number must contain only digits.")]
    public string? PhoneNumber { get; set; } // Explicitly kept

    [DataType(DataType.Date)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    public DateOnly? DateOfBirth { get; set; }

    [StringLength(50, ErrorMessage = "Marital status cannot exceed 50 characters.")]
    public string? MaritalStatus { get; set; }

    // Removed JoiningDate as requested.
    // [DataType(DataType.Date)]
    // [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    // [DateGreaterThan("DateOfBirth", ErrorMessage = "Joining Date must be after Date of Birth.")]
    // public DateOnly? JoiningDate { get; set; }

    // Removed EmployeeCode as requested. The controller will need to generate this if the DB requires it.
    [Required(ErrorMessage = "Employee Code is required.")]
    [StringLength(100, ErrorMessage = "Employee code cannot exceed 100 characters.")]
    public string? EmployeeCode { get; set; }

    [StringLength(500, ErrorMessage = "Profile photo URL cannot exceed 500 characters.")]
    public string? ProfilePhotoUrl { get; set; }

    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
    public string? Description { get; set; }

    public int? CheckAdmin { get; set; }

    [Required(ErrorMessage = "First Name is required.")]
    [StringLength(255, ErrorMessage = "First Name cannot exceed 255 characters.")]
    public string? FirstName { get; set; }

    [StringLength(255, ErrorMessage = "Last Name cannot exceed 255 characters.")]
    public string? LastName { get; set; }

    [DataType(DataType.Date)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    public DateOnly? AnniversaryDate { get; set; }

    public int? ReportToUserId { get; set; }

    // Emergency Contact Details
    //[StringLength(255, ErrorMessage = "Emergency Contact Name cannot exceed 255 characters.")]
    //public string? EmergencyContactName { get; set; }

    //[StringLength(100, ErrorMessage = "Emergency Contact Relation cannot exceed 100 characters.")]
    //public string? EmergencyContactRelation { get; set; }

    //[StringLength(50, ErrorMessage = "Emergency Contact Number cannot exceed 50 characters.")]
    //[RegularExpression(@"^[0-9]+$", ErrorMessage = "Emergency Contact number must contain only digits.")]
    //public string? EmergencyContactNumber { get; set; }
}