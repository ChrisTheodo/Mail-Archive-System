using MailArchive.Application.Contracts.Users;

namespace MailArchive.Application.Contracts.Auth;

public record LoginResponse(
    string AccessToken,
    string TokenType,
    DateTime ExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    UserResponse User
);