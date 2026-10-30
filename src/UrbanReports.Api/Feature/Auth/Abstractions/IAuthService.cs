using UrbanReports.Api.Common;

namespace UrbanReports.Api.Auth;

public interface IAuthService
{
    Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request);

    Task<Result<AuthResponse>> LoginAsync(LoginRequest request);
}
