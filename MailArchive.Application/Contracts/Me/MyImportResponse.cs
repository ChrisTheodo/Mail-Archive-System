namespace MailArchive.Application.Contracts.Me;

public record MyImportResponse(
    Guid Id,
    string PstFilename,
    Guid MailboxId,
    string? MailboxDisplayName,
    string Status,
    DateTime StartedAt,
    DateTime? CompletedAt,
    int TotalMessages,
    int ImportedMessages,
    int FailedMessages,
    int ErrorCount,
    double ProgressPercent,
    bool IsCompleted,
    bool HasErrors
);