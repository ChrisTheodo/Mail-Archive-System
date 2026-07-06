namespace MailArchive.Application.Contracts.Auth;

public record RefreshTokenRequest(
    string RefreshToken
);