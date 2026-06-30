namespace MailArchive.Application.Contracts.Mailboxes;

public record CreateMailboxRequest(
    Guid OwnerUserId,
    string DisplayName
);