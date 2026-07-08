namespace MailArchive.Application.Contracts.Admin;

public record AdminDashboardMailboxUsageResponse(
    int TotalMailboxes,
    int ReturnedMailboxes,
    IReadOnlyCollection<AdminDashboardMailboxUsageItemResponse> Mailboxes,
    DateTime GeneratedAtUtc
);