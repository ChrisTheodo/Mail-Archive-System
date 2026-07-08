using MailArchive.Application.Abstractions;
using MailArchive.Application.Audit;
using MailArchive.Application.Common;
using MailArchive.Application.Contracts.Admin;
using MailArchive.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MailArchive.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/dashboard")]
public class AdminDashboardController : ControllerBase
{
    private readonly IMailArchiveDbContext _db;
    private readonly IAuditLogService _auditLogService;

    public AdminDashboardController(
        IMailArchiveDbContext db,
        IAuditLogService auditLogService)
    {
        _db = db;
        _auditLogService = auditLogService;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var totalUsers = await _db.Users
            .AsNoTracking()
            .CountAsync();

        var activeUsers = await _db.Users
            .AsNoTracking()
            .CountAsync(x => x.IsActive);

        var inactiveUsers = totalUsers - activeUsers;

        var totalMailboxes = await _db.Mailboxes
            .AsNoTracking()
            .CountAsync();

        var totalEmails = await _db.Emails
            .AsNoTracking()
            .CountAsync();

        var emailsWithAttachments = await _db.Emails
            .AsNoTracking()
            .CountAsync(x => x.HasAttachments);

        var totalAttachments = await _db.Attachments
            .AsNoTracking()
            .CountAsync();

        var totalImportBatches = await _db.ImportBatches
            .AsNoTracking()
            .CountAsync();

        var totalImportErrors = await _db.ImportErrors
            .AsNoTracking()
            .CountAsync();

        var importStatusRows = await _db.ImportBatches
            .AsNoTracking()
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

        var importStatusCounts = Enum
            .GetValues<ImportBatchStatus>()
            .Select(status => new ImportStatusCountResponse(
                Status: status.ToString(),
                Count: GetStatusCount(statusCounts, status)))
            .OrderBy(x => x.Status)
            .ToList();

        var response = new AdminDashboardSummaryResponse(
            TotalUsers: totalUsers,
            ActiveUsers: activeUsers,
            InactiveUsers: inactiveUsers,
            TotalMailboxes: totalMailboxes,
            TotalEmails: totalEmails,
            EmailsWithAttachments: emailsWithAttachments,
            TotalAttachments: totalAttachments,
            TotalImportBatches: totalImportBatches,
            QueuedImports: GetStatusCount(statusCounts, ImportBatchStatus.Queued),
            RunningImports: GetStatusCount(statusCounts, ImportBatchStatus.Running),
            CompletedImports: GetStatusCount(statusCounts, ImportBatchStatus.Completed),
            CompletedWithErrorsImports: GetStatusCount(statusCounts, ImportBatchStatus.CompletedWithErrors),
            FailedImports: GetStatusCount(statusCounts, ImportBatchStatus.Failed),
            CancelledImports: GetStatusCount(statusCounts, ImportBatchStatus.Cancelled),
            TotalImportErrors: totalImportErrors,
            ImportStatusCounts: importStatusCounts,
            GeneratedAtUtc: DateTime.UtcNow
        );

        await _auditLogService.LogAsync(
            action: "AdminDashboardSummaryViewed",
            entityType: "Dashboard");

        return Ok(ApiResponse<AdminDashboardSummaryResponse>.Ok(response));
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