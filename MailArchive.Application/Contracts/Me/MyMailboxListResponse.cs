namespace MailArchive.Application.Contracts.Me;

public record MyMailboxListResponse(
    Guid CurrentUserId,
    string? CurrentUserEmail,
    string? CurrentUserRole,
    int TotalMailboxes,
    IReadOnlyCollection<MyMailboxResponse> Mailboxes,
    DateTime GeneratedAtUtc
);