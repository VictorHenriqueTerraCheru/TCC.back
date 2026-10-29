using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using UrbanReports.Api.Common;
using UrbanReports.Api.Persistence;

namespace UrbanReports.Api.Auth;

public sealed class AuthService(UserManager<User> users, JwtTokenService jwt, TimeProvider clock, AppDbContext db, IOptions<IdentityOptions> identity) : IAuthService
{
    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request)
    {
        var email = request.Email.Trim();
        if (await users.FindByEmailAsync(email) is not null)
        {
            return AuthErrors.EmailAlreadyRegistered;
        }

        var user = new User
        {
            Id = Guid.CreateVersion7(),
            Name = request.Name.Trim(),
            Email = email,
            UserName = email,
            CreatedAt = clock.GetUtcNow(),
        };

        IdentityResult result;
        try
        {
            result = await users.CreateAsync(user, request.Password);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return AuthErrors.EmailAlreadyRegistered;
        }

        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName)))
            {
                return AuthErrors.EmailAlreadyRegistered;
            }

            throw new InvalidOperationException(
                "Falha ao criar usuário: " + string.Join("; ", result.Errors.Select(e => e.Code)));
        }

        return CreateResponse(user);
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            return AuthErrors.InvalidCredentials;
        }

        if (await users.IsLockedOutAsync(user))
        {
            return AuthErrors.InvalidCredentials;
        }

        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            await RecordFailureAsync(user.Id);
            return AuthErrors.InvalidCredentials;
        }

        await db.Database.ExecuteSqlAsync(
            $"UPDATE users SET access_failed_count = 0 WHERE id = {user.Id} AND access_failed_count <> 0");
        return CreateResponse(user);
    }

    private async Task RecordFailureAsync(Guid userId)
    {
        var lockout = identity.Value.Lockout;
        var max = lockout.MaxFailedAccessAttempts;
        var minutes = lockout.DefaultLockoutTimeSpan.TotalMinutes;

        await db.Database.ExecuteSqlAsync($"""
            UPDATE users
            SET access_failed_count = CASE WHEN access_failed_count + 1 >= {max} THEN 0 ELSE access_failed_count + 1 END,
                lockout_end = CASE WHEN access_failed_count + 1 >= {max} THEN now() + {minutes} * interval '1 minute' ELSE lockout_end END
            WHERE id = {userId}
            """);
    }

    private AuthResponse CreateResponse(User user)
    {
        var (token, expiresAt) = jwt.Generate(user);
        return new AuthResponse(token, expiresAt, new AuthResponse.UserSummary(user.Id, user.Name, user.Email!, user.Role));
    }
}
