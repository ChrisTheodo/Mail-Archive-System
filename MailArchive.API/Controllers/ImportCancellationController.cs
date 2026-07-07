using MailArchive.Application.Abstractions;
using MailArchive.Application.Audit;
using MailArchive.Application.Common;
using MailArchive.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MailArchive.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/imports")]
public class ImportCancellationController : ControllerBase
{
    private readonly IMailArchiveDbContext _db;
    private readonly IAuditLogService _auditLogService;

    public ImportCancellationController(
        IMailArchiveDbContext db,
        IAuditLogService auditLogService)
    {
        _db = db;
        _auditLogService = auditLogService;
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelImport(Guid id)
    {
        var importBatch = await _db.ImportBatches
            .FirstOrDefaultAsync(x => x.Id == id);

        if (importBatch == null)
            return NotFound(ApiResponse<string>.Fail("ImportBatchNotFound"));

        if (importBatch.Status == ImportBatchStatus.Completed ||
            importBatch.Status == ImportBatchStatus.CompletedWithErrors)
        {
            return BadRequest(ApiResponse<string>.Fail("ImportBatchAlreadyCompleted"));
        }

        if (importBatch.Status == ImportBatchStatus.Failed)
            return BadRequest(ApiResponse<string>.Fail("ImportBatchAlreadyFailed"));

        if (importBatch.Status == ImportBatchStatus.Cancelled)
            return BadRequest(ApiResponse<string>.Fail("ImportBatchAlreadyCancelled"));

        importBatch.Status = ImportBatchStatus.Cancelled;
        importBatch.CompletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await _auditLogService.LogAsync(
            action: "ImportCancelled",
            entityType: "ImportBatch",
            entityId: id);

        var response = new
        {
            importBatchId = id,
            status = importBatch.Status.ToString()
        };

        return Ok(ApiResponse<object>.Ok(response));
    }
}