namespace MailArchive.Application.Contracts.Me;

public record MyImportListResponse(
    Guid CurrentUserId,
    string? CurrentUserEmail,
    string? CurrentUserRole,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    IReadOnlyCollection<MyImportResponse> Items,
    DateTime GeneratedAtUtc
);