using MailArchive.Application.Abstractions;
using MailArchive.Application.Audit;
using MailArchive.Application.Common;
using MailArchive.Application.Contracts.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MailArchive.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/dashboard")]
public class AdminDashboardRecentActivityController : ControllerBase
{
    private readonly IMailArchiveDbContext _db;
    private readonly IAuditLogService _auditLogService;

    public AdminDashboardRecentActivityController(
        IMailArchiveDbContext db,
        IAuditLogService auditLogService)
    {
        _db = db;
        _auditLogService = auditLogService;
    }

    [HttpGet("recent-activity")]
    public async Task<IActionResult> GetRecentActivity([FromQuery] int take = 10)
    {
        take = Math.Clamp(take, 1, 50);

        var recentImports = await _db.ImportBatches
            .AsNoTracking()
            .Include(x => x.Mailbox)
            .ThenInclude(x => x.OwnerUser)
            .OrderByDescending(x => x.StartedAt)
            .ThenByDescending(x => x.Id)
            .Take(take)
            .Select(x => new AdminDashboardRecentImportResponse(
                x.Id,
                x.PstFilename,
                x.MailboxId,
                x.Mailbox.DisplayName,
                x.Mailbox.OwnerUser.Email,
                x.Status.ToString(),
                x.StartedAt,
                x.CompletedAt,
                x.TotalMessages,
                x.ImportedMessages,
                x.FailedMessages
            ))
            .ToListAsync();

        var recentAuditLogs = await _db.AuditLogs
            .AsNoTracking()
            .Include(x => x.User)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(take)
            .Select(x => new AdminDashboardRecentAuditLogResponse(
                x.Id,
                x.UserId,
                x.User == null ? null : x.User.Email,
                x.Action,
                x.EntityType,
                x.EntityId,
                x.IpAddress,
                x.CreatedAt
            ))
            .ToListAsync();

        var response = new AdminDashboardRecentActivityResponse(
            RecentImports: recentImports,
            RecentAuditLogs: recentAuditLogs,
            GeneratedAtUtc: DateTime.UtcNow
        );

        await _auditLogService.LogAsync(
            action: "AdminDashboardRecentActivityViewed",
            entityType: "Dashboard");

        return Ok(ApiResponse<AdminDashboardRecentActivityResponse>.Ok(response));
    }
}