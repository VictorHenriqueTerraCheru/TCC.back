using Microsoft.AspNetCore.Identity;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Persistence;

public sealed class User : IdentityUser<Guid>
{
    public required string Name { get; set; }
    public Role Role { get; set; } = Role.Citizen;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
}
