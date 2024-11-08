using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ydb.Showcase.Entities;

[Table("ScheduleTask")]
public class ScheduleTaskEntity
{
    [Column(TypeName = "Utf8")]
    public Guid Id { get; set; }

    [Column(TypeName = "Utf8")]
    public string Type { get; set; }

    [Column(TypeName = "Timestamp")]
    public DateTime? LastStartUtc { get; set; }

    [Column(TypeName = "Timestamp")]
    public DateTime? LastNonSuccessEndUtc { get; set; }

    [Column(TypeName = "Timestamp")]
    public DateTime? LastSuccessUtc { get; set; }

    [Column(TypeName = "Utf8")]
    public string Error { get; set; } = "";
}