using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RoomRum.API.Models;

[Table("SchemaVersion")]
public partial class SchemaVersion
{
    [Key]
    public int VersionId { get; set; }

    [StringLength(100)]
    public string VersionName { get; set; } = null!;

    [Precision(0)]
    public DateTime AppliedAt { get; set; }
}
