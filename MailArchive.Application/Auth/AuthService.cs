using MailArchive.Application.Abstractions;
using MailArchive.Application.Audit;
using MailArchive.Application.Common;
using MailArchive.Application.Contracts.Auth;
using MailArchive.Application.Contracts.Users;
using Microsoft.EntityFrameworkCore;

namespace MailArchive.Application.Auth;

public class AuthService : IAuthService
{
    private readonly IMailArchiveDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IAuditLogService _auditLogService;

    public AuthService(
        IMailArchiveDbContext db,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        IAuditLogService auditLogService)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _auditLogService = auditLogService;
    }

    public async Task<Result<LoginResponse>> LoginAsync(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _db.Users
            .FirstOrDefaultAsync(x => x.Email == email);

        if (user == null)
        {
            await _auditLogService.LogAsync(
                action: "LoginFailed",
                entityType: "Auth");

            return Result<LoginResponse>.Failure("InvalidCredentials");
        }

        if (!user.IsActive)
        {
            await _auditLogService.LogAsync(
                action: "LoginFailedInactiveUser",
                entityType: "User",
                entityId: user.Id,
                userIdOverride: user.Id);

            return Result<LoginResponse>.Failure("UserInactive");
        }

        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            await _auditLogService.LogAsync(
                action: "LoginFailedPasswordNotConfigured",
                entityType: "User",
                entityId: user.Id,
                userIdOverride: user.Id);

            return Result<LoginResponse>.Failure("PasswordNotConfigured");
        }

        var passwordValid = _passwordHasher.Verify(request.Password, user.PasswordHash);

        if (!passwordValid)
        {
            await _auditLogService.LogAsync(
                action: "LoginFailed",
                entityType: "User",
                entityId: user.Id,
                userIdOverride: user.Id);

            return Result<LoginResponse>.Failure("InvalidCredentials");
        }

        var token = _jwtTokenGenerator.GenerateToken(user);

        await _auditLogService.LogAsync(
            action: "LoginSucceeded",
            entityType: "User",
            entityId: user.Id,
            userIdOverride: user.Id);

        var response = new LoginResponse(
            token.AccessToken,
            "Bearer",
            token.ExpiresAtUtc,
            new UserResponse(
                user.Id,
                user.Email,
                user.DisplayName,
                user.IsActive
            )
        );

        return Result<LoginResponse>.Success(response);
    }
}