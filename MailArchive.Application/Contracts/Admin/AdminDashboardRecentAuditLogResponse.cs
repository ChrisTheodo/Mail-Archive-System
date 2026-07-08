namespace MailArchive.Application.Contracts.Admin;

public record AdminDashboardRecentAuditLogResponse(
    Guid Id,
    Guid? UserId,
    string? UserEmail,
    string Action,
    string EntityType,
    Guid? EntityId,
    string? IpAddress,
    DateTime CreatedAt
);