using MailArchive.Application.Abstractions;
using MailArchive.Application.Audit.Queries;
using MailArchive.Application.Common;
using MailArchive.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MailArchive.Application.Audit;

public class AuditLogService : IAuditLogService
{
    private readonly IMailArchiveDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public AuditLogService(
        IMailArchiveDbContext db,
        ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task LogAsync(
        string action,
        string entityType,
        Guid? entityId = null,
        Guid? userIdOverride = null)
    {
        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userIdOverride ?? _currentUser.UserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            IpAddress = _currentUser.IpAddress,
            CreatedAt = DateTime.UtcNow
        };

        _db.AuditLogs.Add(auditLog);

        await _db.SaveChangesAsync();
    }

    public async Task<PagedResult<AuditLog>> GetPagedAsync(AuditLogQueryParameters query)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 20 : query.PageSize;
        pageSize = pageSize > 100 ? 100 : pageSize;

        var baseQuery = _db.AuditLogs
            .Include(x => x.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();

            baseQuery = baseQuery.Where(x =>
                x.Action.ToLower().Contains(search) ||
                x.EntityType.ToLower().Contains(search) ||
                (x.IpAddress != null && x.IpAddress.ToLower().Contains(search)) ||
                (x.User != null && x.User.Email.ToLower().Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            var action = query.Action.Trim().ToLowerInvariant();

            baseQuery = baseQuery.Where(x =>
                x.Action.ToLower().Contains(action));
        }

        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            var entityType = query.EntityType.Trim().ToLowerInvariant();

            baseQuery = baseQuery.Where(x =>
                x.EntityType.ToLower().Contains(entityType));
        }

        if (query.UserId.HasValue)
        {
            baseQuery = baseQuery.Where(x =>
                x.UserId == query.UserId.Value);
        }

        if (query.FromDate.HasValue)
        {
            baseQuery = baseQuery.Where(x =>
                x.CreatedAt >= query.FromDate.Value);
        }

        if (query.ToDate.HasValue)
        {
            baseQuery = baseQuery.Where(x =>
                x.CreatedAt <= query.ToDate.Value);
        }

        var total = await baseQuery.CountAsync();

        var items = await baseQuery
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<AuditLog>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }
}