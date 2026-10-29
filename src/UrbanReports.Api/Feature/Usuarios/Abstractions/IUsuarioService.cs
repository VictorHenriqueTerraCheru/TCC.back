using UrbanReports.Api.Common;

namespace UrbanReports.Api.Users;

public interface IUserService
{
    Task<Result<GetMeResponse>> GetMeAsync(Guid userId, CancellationToken ct);

    Task<Result<GetMeResponse>> UpdateMeAsync(Guid userId, UpdateMeRequest request, CancellationToken ct);

    Task<Paged<GetHistoryItem>> GetHistoryAsync(Guid userId, GetHistoryRequest request, CancellationToken ct);
}
