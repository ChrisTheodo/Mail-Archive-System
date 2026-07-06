namespace MailArchive.Application.Contracts.Emails;

public record EmailListResponse(
    Guid Id,
    Guid MailboxId,
    string MailboxDisplayName,
    string? InternetMessageId,
    string FolderPath,
    string SenderEmail,
    string? SenderName,
    string? Subject,
    DateTime? SentAt,
    DateTime? ReceivedAt,
    bool HasAttachments,
    IReadOnlyCollection<string> RecipientEmails,
    IReadOnlyCollection<string> AttachmentFileNames,
    string? SearchSnippet
);