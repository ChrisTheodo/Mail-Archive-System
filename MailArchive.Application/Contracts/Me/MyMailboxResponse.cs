namespace MailArchive.Application.Contracts.Me;

public record MyMailboxResponse(
    Guid MailboxId,
    string DisplayName,
    Guid? OwnerUserId,
    string? OwnerEmail,
    int TotalEmails,
    int EmailsWithAttachments,
    int TotalAttachments,
    int TotalImportBatches,
    int PendingImports,
    int QueuedImports,
    int RunningImports,
    int CompletedImports,
    int CompletedWithErrorsImports,
    int FailedImports,
    int CancelledImports,
    int TotalImportErrors,
    DateTime? LatestEmailReceivedAt,
    DateTime? LatestImportStartedAt
);