namespace MailArchive.Application.Contracts.Imports;

public record ImportParserPreviewEmailResponse(
    int Index,
    string? InternetMessageId,
    string? FolderPath,
    string SenderEmail,
    string? SenderName,
    string? Subject,
    DateTime? SentAt,
    DateTime? ReceivedAt,
    int RecipientCount,
    int AttachmentCount,
    IReadOnlyCollection<string> RecipientEmails,
    IReadOnlyCollection<string> AttachmentFileNames,
    string? BodyPreview
);