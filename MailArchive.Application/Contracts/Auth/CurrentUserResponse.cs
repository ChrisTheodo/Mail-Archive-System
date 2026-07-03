namespace MailArchive.Application.Contracts.Auth;

public record CurrentUserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string Role
);