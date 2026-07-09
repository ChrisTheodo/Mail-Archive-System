namespace MailArchive.Application.Contracts.Me;

public record MyImportDetailsResponse(
    Guid Id,
    string PstFilename,
    string PstHash,
    string? PstStoragePath,
    Guid MailboxId,
    string? MailboxDisplayName,
    string? MailboxOwnerEmail,
    string Status,
    DateTime StartedAt,
    DateTime? CompletedAt,
    int TotalMessages,
    int ImportedMessages,
    int FailedMessages,
    int EmailsInDatabase,
    int EmailsWithAttachments,
    int AttachmentsInDatabase,
    int ErrorCount,
    double ProgressPercent,
    bool IsCompleted,
    bool HasErrors,
    IReadOnlyCollection<MyImportErrorResponse> Errors,
    DateTime GeneratedAtUtc
);