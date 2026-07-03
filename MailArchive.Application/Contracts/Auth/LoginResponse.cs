using MailArchive.Application.Contracts.Users;

namespace MailArchive.Application.Contracts.Auth;

public record LoginResponse(
    string AccessToken,
    string TokenType,
    DateTime ExpiresAtUtc,
    UserResponse User
);