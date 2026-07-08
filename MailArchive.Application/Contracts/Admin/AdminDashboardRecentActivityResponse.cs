namespace MailArchive.Application.Contracts.Admin;

public record AdminDashboardRecentActivityResponse(
    IReadOnlyCollection<AdminDashboardRecentImportResponse> RecentImports,
    IReadOnlyCollection<AdminDashboardRecentAuditLogResponse> RecentAuditLogs,
    DateTime GeneratedAtUtc
);