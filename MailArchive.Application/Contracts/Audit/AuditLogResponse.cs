namespace MailArchive.Application.Contracts.Audit;

public record AuditLogResponse(
    Guid Id,
    Guid? UserId,
    string? UserEmail,
    string Action,
    string EntityType,
    Guid? EntityId,
    string? IpAddress,
    DateTime CreatedAt
);