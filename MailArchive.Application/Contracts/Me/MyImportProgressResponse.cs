namespace MailArchive.Application.Contracts.Me;

public record MyImportProgressResponse(
    Guid Id,
    string PstFilename,
    Guid MailboxId,
    string? MailboxDisplayName,
    string Status,
    double ProgressPercent,
    int TotalMessages,
    int ImportedMessages,
    int FailedMessages,
    int ErrorCount,
    bool IsCompleted,
    bool HasErrors,
    DateTime StartedAt,
    DateTime? CompletedAt
);