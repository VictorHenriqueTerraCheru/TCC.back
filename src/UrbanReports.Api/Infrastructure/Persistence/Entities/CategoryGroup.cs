using UrbanReports.Api.Common;

namespace UrbanReports.Api.Persistence;

public sealed class CategoryGroup
{
    public required string Key { get; set; }
    public required string Label { get; set; }
    public ReportType Type { get; set; }
    public GroupColor Color { get; set; }
    public int SortOrder { get; set; }
}
