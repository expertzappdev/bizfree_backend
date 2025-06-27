﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BizfreeApp.Models;

[Table("rolespermissions")]
public partial class Rolespermission
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("role_id")]
    public int RoleId { get; set; }

    [Column("permission_id")]
    public int PermissionId { get; set; }

    [Column("company_id")]
    public int CompanyId { get; set; }

    [ForeignKey("CompanyId")]
    [InverseProperty("Rolespermissions")]
    public virtual Company Company { get; set; } = null!;

    [ForeignKey("PermissionId")]
    [InverseProperty("Rolespermissions")]
    public virtual Permission Permission { get; set; } = null!;

    [ForeignKey("RoleId")]
    [InverseProperty("Rolespermissions")]
    public virtual Role Role { get; set; } = null!;
}
