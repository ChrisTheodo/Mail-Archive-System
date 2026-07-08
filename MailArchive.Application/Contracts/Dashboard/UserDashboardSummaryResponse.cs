namespace MailArchive.Application.Contracts.Dashboard;

public record UserDashboardSummaryResponse(
    Guid CurrentUserId,
    string? CurrentUserEmail,
    string? CurrentUserRole,
    int TotalMailboxes,
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
    DateTime? LatestImportStartedAt,
    DateTime GeneratedAtUtc
);