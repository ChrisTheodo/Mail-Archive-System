namespace MailArchive.Application.Contracts.Users;

public record UserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    bool IsActive
);