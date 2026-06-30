namespace MailArchive.Application.Contracts.Mailboxes;

public record MailboxResponse(
    Guid Id,
    string DisplayName,
    Guid? OwnerUserId,
    string? OwnerEmail
);