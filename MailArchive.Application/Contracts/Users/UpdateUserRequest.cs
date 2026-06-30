namespace MailArchive.Application.Contracts.Users;

public record UpdateUserRequest(
    string DisplayName,
    bool IsActive
);