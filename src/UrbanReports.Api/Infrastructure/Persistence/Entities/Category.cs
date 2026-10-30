using UrbanReports.Api.Common;

namespace UrbanReports.Api.Persistence;

public sealed class Category
{
    public short Id { get; set; }
    public required string Name { get; set; }
    public required string Group { get; set; }
    public ReportType Type { get; set; }
    public required string Icon { get; set; }

    public bool Active { get; set; } = true;
}
