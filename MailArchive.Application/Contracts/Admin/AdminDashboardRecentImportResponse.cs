namespace MailArchive.Application.Contracts.Admin;

public record AdminDashboardRecentImportResponse(
    Guid Id,
    string PstFilename,
    Guid MailboxId,
    string? MailboxDisplayName,
    string? MailboxOwnerEmail,
    string Status,
    DateTime StartedAt,
    DateTime? CompletedAt,
    int TotalMessages,
    int ImportedMessages,
    int FailedMessages
);