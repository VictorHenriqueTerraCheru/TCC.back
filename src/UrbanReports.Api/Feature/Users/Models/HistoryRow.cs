using System.ComponentModel.DataAnnotations.Schema;

namespace UrbanReports.Api.Users;

public sealed class HistoryRow
{
    [Column("key")] public Guid Key { get; init; }
    [Column("type")] public int Type { get; init; }
    [Column("report_id")] public Guid? ReportId { get; init; }
    [Column("category_id")] public short CategoryId { get; init; }
    [Column("category_name")] public string CategoryName { get; init; } = "";
    [Column("group")] public string Group { get; init; } = "";
    [Column("icon")] public string Icon { get; init; } = "";
    [Column("address")] public string? Address { get; init; }
    [Column("value")] public short? Value { get; init; }
    [Column("issue_status")] public short? IssueStatus { get; init; }
    [Column("date")] public DateTimeOffset Date { get; init; }
}
