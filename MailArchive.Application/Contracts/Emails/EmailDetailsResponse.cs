namespace MailArchive.Application.Contracts.Emails;

public record EmailDetailsResponse(
    Guid Id,
    Guid MailboxId,
    string? MailboxDisplayName,
    Guid ImportBatchId,
    string? InternetMessageId,
    string MessageHash,
    string? FolderPath,
    string? SenderEmail,
    string? SenderName,
    string? Subject,
    string? BodyText,
    string? BodyHtml,
    DateTime? SentAt,
    DateTime? ReceivedAt,
    bool HasAttachments,
    DateTime CreatedAt,
    IReadOnlyCollection<EmailRecipientResponse> Recipients,
    IReadOnlyCollection<EmailAttachmentResponse> Attachments
);