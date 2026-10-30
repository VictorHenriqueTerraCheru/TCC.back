using UrbanReports.Api.Common;

namespace UrbanReports.Api.Groups;

public interface IGroupService
{
    Task<List<GroupResponse>> ListAsync(CancellationToken ct);

    Task<Result<GroupResponse>> CreateAsync(CreateGroupRequest request, CancellationToken ct);

    Task<Result<GroupResponse>> EditAsync(string key, EditGroupRequest request, CancellationToken ct);

    Task<Result<Empty>> DeleteAsync(string key, CancellationToken ct);
}
