using UrbanReports.Api.Common;

namespace UrbanReports.Api.Groups;

public sealed record GroupResponse(string Key, string Label, ReportType Type, GroupColor Color, int SortOrder);

public sealed record CreateGroupRequest(string Label, ReportType Type, GroupColor Color);

public sealed record EditGroupRequest(string Label, GroupColor Color, int SortOrder);
