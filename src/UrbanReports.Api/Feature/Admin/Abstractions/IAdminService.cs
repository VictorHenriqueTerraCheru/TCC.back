using UrbanReports.Api.Common;

namespace UrbanReports.Api.Admin;

public interface IAdminService
{
    Task<Paged<ListVerificationsItem>> ListVerificationsAsync(ListVerificationsRequest request, CancellationToken ct);

    Task<Result<Empty>> ApproveAsync(Guid id, Guid adminId, CancellationToken ct);

    Task<Result<Empty>> RejectAsync(Guid id, Guid adminId, RejectVerificationRequest request, CancellationToken ct);

    Task<Paged<ListUsersItem>> ListUsersAsync(ListUsersRequest request, CancellationToken ct);

    Task<Result<Empty>> ChangeRoleAsync(Guid id, Guid adminId, ChangeRoleRequest request, CancellationToken ct);
}
