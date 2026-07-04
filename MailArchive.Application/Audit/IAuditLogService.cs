using MailArchive.Application.Audit.Queries;
using MailArchive.Application.Common;
using MailArchive.Domain.Entities;

namespace MailArchive.Application.Audit;

public interface IAuditLogService
{
    Task LogAsync(
        string action,
        string entityType,
        Guid? entityId = null,
        Guid? userIdOverride = null);

    Task<PagedResult<AuditLog>> GetPagedAsync(AuditLogQueryParameters query);
}