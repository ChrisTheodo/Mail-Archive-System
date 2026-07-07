using MailArchive.API.Background;
using MailArchive.Application.Abstractions;
using MailArchive.Application.Audit;
using MailArchive.Application.Common;
using MailArchive.Application.Imports;
using MailArchive.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


namespace MailArchive.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/imports")]
public class ImportBackgroundProcessingController : ControllerBase
{
    private readonly IMailArchiveDbContext _db;
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly IAuditLogService _auditLogService;

    public ImportBackgroundProcessingController(
        IMailArchiveDbContext db,
        IBackgroundTaskQueue taskQueue,
        IAuditLogService auditLogService)
    {
        _db = db;
        _taskQueue = taskQueue;
        _auditLogService = auditLogService;
    }

    [HttpPost("{id:guid}/process/background")]
    public async Task<IActionResult> ProcessInBackground(Guid id)
    {
        var importBatch = await _db.ImportBatches
            .FirstOrDefaultAsync(x => x.Id == id);

        if (importBatch == null)
            return NotFound(ApiResponse<string>.Fail("ImportBatchNotFound"));

        if (importBatch.Status == ImportBatchStatus.Running)
            return BadRequest(ApiResponse<string>.Fail("ImportBatchAlreadyRunning"));

        if (importBatch.Status == ImportBatchStatus.Completed ||
            importBatch.Status == ImportBatchStatus.CompletedWithErrors)
        {
            return BadRequest(ApiResponse<string>.Fail("ImportBatchAlreadyCompleted"));
        }

        if (importBatch.Status == ImportBatchStatus.Failed)
            return BadRequest(ApiResponse<string>.Fail("ImportBatchAlreadyFailed"));

        importBatch.Status = ImportBatchStatus.Running;
        importBatch.StartedAt = DateTime.UtcNow;
        importBatch.CompletedAt = null;

        await _db.SaveChangesAsync();

        await _auditLogService.LogAsync(
            action: "ImportProcessingQueued",
            entityType: "ImportBatch",
            entityId: id);

        await _taskQueue.QueueBackgroundWorkItemAsync(async (services, cancellationToken) =>
        {
            using var scope = services.CreateScope();

            var processor = scope.ServiceProvider.GetRequiredService<IPstImportProcessor>();

            await processor.ProcessAsync(id);
        });

        var response = new
        {
            importBatchId = id,
            status = "Queued"
        };

        return Accepted(ApiResponse<object>.Ok(response));
    }
}