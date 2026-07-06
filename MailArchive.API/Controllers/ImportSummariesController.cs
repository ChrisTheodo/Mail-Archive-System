using MailArchive.Application.Abstractions;
using MailArchive.Application.Audit;
using MailArchive.Application.Common;
using MailArchive.Application.Contracts.Imports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MailArchive.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/imports")]
public class ImportSummariesController : ControllerBase
{
    private readonly IMailArchiveDbContext _db;
    private readonly IAuditLogService _auditLogService;

    public ImportSummariesController(
        IMailArchiveDbContext db,
        IAuditLogService auditLogService)
    {
        _db = db;
        _auditLogService = auditLogService;
    }

    [HttpGet("{id:guid}/summary")]
    public async Task<IActionResult> GetImportSummary(Guid id)
    {
        var importBatch = await _db.ImportBatches
            .AsNoTracking()
            .Include(x => x.Mailbox)
                .ThenInclude(x => x.OwnerUser)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (importBatch == null)
            return NotFound(ApiResponse<string>.Fail("ImportBatchNotFound"));

        var emailsInDatabase = await _db.Emails
            .AsNoTracking()
            .CountAsync(x => x.ImportBatchId == id);

        var emailsWithAttachments = await _db.Emails
            .AsNoTracking()
            .CountAsync(x =>
                x.ImportBatchId == id &&
                x.HasAttachments);

        var attachmentsInDatabase = await _db.Attachments
            .AsNoTracking()
            .CountAsync(x => x.Email.ImportBatchId == id);

        var errorCount = await _db.ImportErrors
            .AsNoTracking()
            .CountAsync(x => x.ImportBatchId == id);

        await _auditLogService.LogAsync(
            action: "ImportSummaryViewed",
            entityType: "ImportBatch",
            entityId: id);

        var response = new ImportBatchSummaryResponse(
            importBatch.Id,
            importBatch.PstFilename,
            importBatch.PstHash,
            importBatch.PstStoragePath,
            importBatch.MailboxId,
            importBatch.Mailbox?.DisplayName,
            importBatch.Mailbox?.OwnerUser?.Email,
            importBatch.Status,
            importBatch.StartedAt,
            importBatch.CompletedAt,
            importBatch.TotalMessages,
            importBatch.ImportedMessages,
            importBatch.FailedMessages,
            emailsInDatabase,
            emailsWithAttachments,
            attachmentsInDatabase,
            errorCount
        );

        return Ok(ApiResponse<ImportBatchSummaryResponse>.Ok(response));
    }
}