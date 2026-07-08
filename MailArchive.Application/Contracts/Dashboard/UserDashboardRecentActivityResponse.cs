namespace MailArchive.Application.Contracts.Dashboard;

public record UserDashboardRecentActivityResponse(
    Guid CurrentUserId,
    string? CurrentUserEmail,
    string? CurrentUserRole,
    IReadOnlyCollection<UserDashboardRecentEmailResponse> RecentEmails,
    IReadOnlyCollection<UserDashboardRecentImportResponse> RecentImports,
    DateTime GeneratedAtUtc
);