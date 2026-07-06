using System.Security.Cryptography;
using MailArchive.Application.Abstractions;
using MailArchive.Application.Audit;
using MailArchive.Application.Common;
using MailArchive.Application.Contracts.Auth;
using MailArchive.Application.Contracts.Users;
using MailArchive.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MailArchive.Application.Auth;

public class AuthService : IAuthService
{
    private const int RefreshTokenDays = 7;

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

        var response = await CreateLoginResponseAsync(user);

        await _auditLogService.LogAsync(
            action: "LoginSucceeded",
            entityType: "User",
            entityId: user.Id,
            userIdOverride: user.Id);

        return Result<LoginResponse>.Success(response);
    }

    public async Task<Result<LoginResponse>> RefreshAsync(RefreshTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return Result<LoginResponse>.Failure("RefreshTokenRequired");

        var refreshTokenHash = HashRefreshToken(request.RefreshToken);

        var storedRefreshToken = await _db.RefreshTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == refreshTokenHash);

        if (storedRefreshToken == null)
            return Result<LoginResponse>.Failure("RefreshTokenInvalid");

        if (storedRefreshToken.RevokedAt.HasValue)
            return Result<LoginResponse>.Failure("RefreshTokenRevoked");

        if (storedRefreshToken.ExpiresAt <= DateTime.UtcNow)
            return Result<LoginResponse>.Failure("RefreshTokenExpired");

        if (!storedRefreshToken.User.IsActive)
            return Result<LoginResponse>.Failure("UserInactive");

        var rawNewRefreshToken = GenerateRawRefreshToken();
        var newRefreshTokenHash = HashRefreshToken(rawNewRefreshToken);
        var newRefreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(RefreshTokenDays);

        storedRefreshToken.RevokedAt = DateTime.UtcNow;
        storedRefreshToken.ReplacedByTokenHash = newRefreshTokenHash;

        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = storedRefreshToken.UserId,
            TokenHash = newRefreshTokenHash,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = newRefreshTokenExpiresAtUtc,
            RevokedAt = null,
            ReplacedByTokenHash = null
        });

        await _db.SaveChangesAsync();

        var token = _jwtTokenGenerator.GenerateToken(storedRefreshToken.User);

        await _auditLogService.LogAsync(
            action: "TokenRefreshed",
            entityType: "User",
            entityId: storedRefreshToken.UserId,
            userIdOverride: storedRefreshToken.UserId);

        var response = new LoginResponse(
            token.AccessToken,
            "Bearer",
            token.ExpiresAtUtc,
            rawNewRefreshToken,
            newRefreshTokenExpiresAtUtc,
            new UserResponse(
                storedRefreshToken.User.Id,
                storedRefreshToken.User.Email,
                storedRefreshToken.User.DisplayName,
                storedRefreshToken.User.IsActive
            )
        );

        return Result<LoginResponse>.Success(response);
    }

    private async Task<LoginResponse> CreateLoginResponseAsync(User user)
    {
        var token = _jwtTokenGenerator.GenerateToken(user);

        var rawRefreshToken = GenerateRawRefreshToken();
        var refreshTokenHash = HashRefreshToken(rawRefreshToken);
        var refreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(RefreshTokenDays);

        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = refreshTokenExpiresAtUtc,
            RevokedAt = null,
            ReplacedByTokenHash = null
        });

        await _db.SaveChangesAsync();

        return new LoginResponse(
            token.AccessToken,
            "Bearer",
            token.ExpiresAtUtc,
            rawRefreshToken,
            refreshTokenExpiresAtUtc,
            new UserResponse(
                user.Id,
                user.Email,
                user.DisplayName,
                user.IsActive
            )
        );
    }

    private static string GenerateRawRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);

        return Convert.ToBase64String(bytes);
    }

    private static string HashRefreshToken(string refreshToken)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(refreshToken));

        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}