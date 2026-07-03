namespace MailArchive.Application.Auth;

public record JwtTokenResult(
    string AccessToken,
    DateTime ExpiresAtUtc
);