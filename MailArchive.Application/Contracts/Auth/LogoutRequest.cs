namespace MailArchive.Application.Contracts.Auth;

public record LogoutRequest(
    string? RefreshToken
);