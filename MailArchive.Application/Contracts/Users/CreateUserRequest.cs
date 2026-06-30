namespace MailArchive.Application.Contracts.Users;

public record CreateUserRequest(
    string Email,
    string DisplayName
);