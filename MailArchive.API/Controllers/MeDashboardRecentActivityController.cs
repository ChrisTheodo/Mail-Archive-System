using System.Security.Claims;
using MailArchive.Application.Abstractions;
using MailArchive.Application.Audit;
using MailArchive.Application.Common;
using MailArchive.Application.Contracts.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MailArchive.API.Controllers;

[ApiController]
[Authorize]
[Route("api/me/dashboard")]
public class MeDashboardRecentActivityController : ControllerBase
{
    private readonly IMailArchiveDbContext _db;
    private readonly IAuditLogService _auditLogService;

    public MeDashboardRecentActivityController(
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

        var recentEmails = await _db.Emails
            .AsNoTracking()
            .Where(x => mailboxIds.Contains(x.MailboxId))
            .OrderByDescending(x => x.ReceivedAt)
            .ThenByDescending(x => x.CreatedAt)
            .Take(take)
            .Select(x => new UserDashboardRecentEmailResponse(
                x.Id,
                x.MailboxId,
                x.Mailbox.DisplayName,
                x.InternetMessageId,
                x.FolderPath,
                x.SenderEmail ?? string.Empty,
                x.SenderName,
                x.Subject,
                x.SentAt,
                x.ReceivedAt,
                x.HasAttachments,
                x.Attachments
                    .Select(attachment => attachment.FileName)
                    .OrderBy(fileName => fileName)
                    .ToList(),
                CreateBodyPreview(x.BodyText ?? x.BodyHtml)
            ))
            .ToListAsync();

        var recentImports = await _db.ImportBatches
            .AsNoTracking()
            .Where(x => mailboxIds.Contains(x.MailboxId))
            .OrderByDescending(x => x.StartedAt)
            .ThenByDescending(x => x.Id)
            .Take(take)
            .Select(x => new UserDashboardRecentImportResponse(
                x.Id,
                x.PstFilename,
                x.MailboxId,
                x.Mailbox.DisplayName,
                x.Status.ToString(),
                x.StartedAt,
                x.CompletedAt,
                x.TotalMessages,
                x.ImportedMessages,
                x.FailedMessages
            ))
            .ToListAsync();

        var response = new UserDashboardRecentActivityResponse(
            CurrentUserId: currentUserId,
            CurrentUserEmail: currentUserEmail,
            CurrentUserRole: currentUserRole,
            RecentEmails: recentEmails,
            RecentImports: recentImports,
            GeneratedAtUtc: DateTime.UtcNow
        );

        await _auditLogService.LogAsync(
            action: "UserDashboardRecentActivityViewed",
            entityType: "Dashboard");

        return Ok(ApiResponse<UserDashboardRecentActivityResponse>.Ok(response));
    }

    private static string? CreateBodyPreview(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        var normalized = string.Join(
            " ",
            body.Split(
                new[] { ' ', '\r', '\n', '\t' },
                StringSplitOptions.RemoveEmptyEntries));

        if (normalized.Length <= 220)
            return normalized;

        return normalized[..220] + "...";
    }
}