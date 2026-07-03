namespace MailArchive.Application.Contracts.Auth;

public record LoginRequest(
    string Email,
    string Password
);