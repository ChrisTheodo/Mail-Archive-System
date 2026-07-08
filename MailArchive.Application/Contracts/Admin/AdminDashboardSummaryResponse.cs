namespace MailArchive.Application.Contracts.Admin;

public record AdminDashboardSummaryResponse(
    int TotalUsers,
    int ActiveUsers,
    int InactiveUsers,
    int TotalMailboxes,
    int TotalEmails,
    int EmailsWithAttachments,
    int TotalAttachments,
    int TotalImportBatches,
    int QueuedImports,
    int RunningImports,
    int CompletedImports,
    int CompletedWithErrorsImports,
    int FailedImports,
    int CancelledImports,
    int TotalImportErrors,
    IReadOnlyCollection<ImportStatusCountResponse> ImportStatusCounts,
    DateTime GeneratedAtUtc
);