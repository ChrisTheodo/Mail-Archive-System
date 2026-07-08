namespace MailArchive.Application.Contracts.Admin;

public record AdminDashboardMailboxUsageItemResponse(
    Guid MailboxId,
    string MailboxDisplayName,
    Guid? OwnerUserId,
    string OwnerEmail,
    bool OwnerIsActive,
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