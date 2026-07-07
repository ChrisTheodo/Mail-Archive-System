namespace MailArchive.Application.Contracts.Imports;

public record ImportProgressResponse(
    Guid Id,
    string PstFilename,
    Guid MailboxId,
    string? MailboxDisplayName,
    string Status,
    int ProgressPercent,
    int TotalMessages,
    int ImportedMessages,
    int FailedMessages,
    int ErrorCount,
    bool IsCompleted,
    bool HasErrors,
    DateTime StartedAt,
    DateTime? CompletedAt
);