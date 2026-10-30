using UrbanReports.Api.Common;

namespace UrbanReports.Api.Auth;

public sealed record RegisterRequest(string Name, string Email, string Password, bool AcceptedTerms);

public sealed record LoginRequest(string Email, string Password);

public sealed record AuthResponse(string Token, DateTimeOffset ExpiresAt, AuthResponse.UserSummary User)
{
    public sealed record UserSummary(Guid Id, string Name, string Email, Role Role);
}
