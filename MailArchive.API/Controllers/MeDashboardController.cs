using System.Security.Claims;
using MailArchive.Application.Abstractions;
using MailArchive.Application.Audit;
using MailArchive.Application.Common;
using MailArchive.Application.Contracts.Dashboard;
using MailArchive.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MailArchive.API.Controllers;

[ApiController]
[Authorize]
[Route("api/me/dashboard")]
public class MeDashboardController : ControllerBase
{
    private readonly IMailArchiveDbContext _db;
    private readonly IAuditLogService _auditLogService;

    public MeDashboardController(
        IMailArchiveDbContext db,
        IAuditLogService auditLogService)
    {
        _db = db;
        _auditLogService = auditLogService;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var userIdText = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdText, out var currentUserId))
            return Unauthorized(ApiResponse<string>.Fail("CurrentUserNotFound"));

        var currentUserEmail = User.FindFirstValue(ClaimTypes.Email);
        var currentUserRole = User.FindFirstValue(ClaimTypes.Role);

        var mailboxIds = await _db.Mailboxes
            .AsNoTracking()
            .Where(x => x.OwnerUserId == currentUserId)
            .Select(x => x.Id)
            .ToListAsync();

        var totalMailboxes = mailboxIds.Count;

        var totalEmails = await _db.Emails
            .AsNoTracking()
            .CountAsync(x => mailboxIds.Contains(x.MailboxId));

        var emailsWithAttachments = await _db.Emails
            .AsNoTracking()
            .CountAsync(x =>
                mailboxIds.Contains(x.MailboxId) &&
                x.HasAttachments);

        var totalAttachments = await _db.Attachments
            .AsNoTracking()
            .CountAsync(x => mailboxIds.Contains(x.Email.MailboxId));

        var totalImportBatches = await _db.ImportBatches
            .AsNoTracking()
            .CountAsync(x => mailboxIds.Contains(x.MailboxId));

        var importStatusRows = await _db.ImportBatches
            .AsNoTracking()
            .Where(x => mailboxIds.Contains(x.MailboxId))
            .GroupBy(x => x.Status)
            .Select(x => new
            {
                Status = x.Key,
                Count = x.Count()
            })
            .ToListAsync();

        var statusCounts = importStatusRows.ToDictionary(
            x => x.Status,
            x => x.Count);

        var totalImportErrors = await _db.ImportErrors
            .AsNoTracking()
            .CountAsync(x => mailboxIds.Contains(x.ImportBatch.MailboxId));

        var latestEmailReceivedAt = await _db.Emails
            .AsNoTracking()
            .Where(x => mailboxIds.Contains(x.MailboxId))
            .MaxAsync(x => (DateTime?)x.ReceivedAt);

        var latestImportStartedAt = await _db.ImportBatches
            .AsNoTracking()
            .Where(x => mailboxIds.Contains(x.MailboxId))
            .MaxAsync(x => (DateTime?)x.StartedAt);

        var response = new UserDashboardSummaryResponse(
            CurrentUserId: currentUserId,
            CurrentUserEmail: currentUserEmail,
            CurrentUserRole: currentUserRole,
            TotalMailboxes: totalMailboxes,
            TotalEmails: totalEmails,
            EmailsWithAttachments: emailsWithAttachments,
            TotalAttachments: totalAttachments,
            TotalImportBatches: totalImportBatches,
            PendingImports: GetStatusCount(statusCounts, ImportBatchStatus.Pending),
            QueuedImports: GetStatusCount(statusCounts, ImportBatchStatus.Queued),
            RunningImports: GetStatusCount(statusCounts, ImportBatchStatus.Running),
            CompletedImports: GetStatusCount(statusCounts, ImportBatchStatus.Completed),
            CompletedWithErrorsImports: GetStatusCount(statusCounts, ImportBatchStatus.CompletedWithErrors),
            FailedImports: GetStatusCount(statusCounts, ImportBatchStatus.Failed),
            CancelledImports: GetStatusCount(statusCounts, ImportBatchStatus.Cancelled),
            TotalImportErrors: totalImportErrors,
            LatestEmailReceivedAt: latestEmailReceivedAt,
            LatestImportStartedAt: latestImportStartedAt,
            GeneratedAtUtc: DateTime.UtcNow
        );

        await _auditLogService.LogAsync(
            action: "UserDashboardSummaryViewed",
            entityType: "Dashboard");

        return Ok(ApiResponse<UserDashboardSummaryResponse>.Ok(response));
    }

    private static int GetStatusCount(
        IReadOnlyDictionary<ImportBatchStatus, int> statusCounts,
        ImportBatchStatus status)
    {
        return statusCounts.TryGetValue(status, out var count)
            ? count
            : 0;
    }
}