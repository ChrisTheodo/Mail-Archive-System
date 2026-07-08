namespace MailArchive.Application.Contracts.Dashboard;

public record UserDashboardRecentEmailResponse(
    Guid Id,
    Guid MailboxId,
    string? MailboxDisplayName,
    string? InternetMessageId,
    string? FolderPath,
    string SenderEmail,
    string? SenderName,
    string? Subject,
    DateTime? SentAt,
    DateTime? ReceivedAt,
    bool HasAttachments,
    IReadOnlyCollection<string> AttachmentFileNames,
    string? BodyPreview
);