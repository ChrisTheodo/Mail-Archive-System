namespace MailArchive.Application.Contracts.Dashboard;

public record UserDashboardRecentImportResponse(
    Guid Id,
    string PstFilename,
    Guid MailboxId,
    string? MailboxDisplayName,
    string Status,
    DateTime StartedAt,
    DateTime? CompletedAt,
    int TotalMessages,
    int ImportedMessages,
    int FailedMessages
);