using MailArchive.Application.Audit;
using MailArchive.Application.Common;
using MailArchive.Application.Contracts.Imports;
using MailArchive.Application.Imports;
using MailArchive.Application.Imports.Queries;
using MailArchive.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MailArchive.API.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/imports")]
public class ImportsController : ControllerBase
{
    private readonly IImportService _service;
    private readonly IAuditLogService _auditLogService;

    public ImportsController(
        IImportService service,
        IAuditLogService auditLogService)
    {
        _service = service;
        _auditLogService = auditLogService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] ImportBatchQueryParameters query)
    {
        var result = await _service.GetPagedAsync(query);

        await _auditLogService.LogAsync(
            action: "ImportListViewed",
            entityType: "ImportBatch");

        var mapped = new PagedResult<ImportBatchResponse>
        {
            Items = result.Items.Select(MapToResponse).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };

        return Ok(ApiResponse<PagedResult<ImportBatchResponse>>.Ok(mapped));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _service.GetByIdAsync(id);

        if (!result.IsSuccess)
            return NotFound(ApiResponse<ImportBatchResponse>.Fail(result.Error!));

        await _auditLogService.LogAsync(
            action: "ImportViewed",
            entityType: "ImportBatch",
            entityId: id);

        return Ok(ApiResponse<ImportBatchResponse>.Ok(MapToResponse(result.Value!)));
    }

    [HttpPost("pst")]
    public async Task<IActionResult> CreatePstImport(CreatePstImportRequest request)
    {
        var result = await _service.CreatePstImportAsync(request);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<string>.Fail(result.Error!));

        var importBatch = result.Value!;

        await _auditLogService.LogAsync(
            action: "PstImportCreated",
            entityType: "ImportBatch",
            entityId: importBatch.Id);

        return Ok(ApiResponse<ImportBatchResponse>.Ok(MapToResponse(importBatch)));
    }

    [HttpPost("{id:guid}/start")]
    public async Task<IActionResult> Start(Guid id)
    {
        var result = await _service.StartAsync(id);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<string>.Fail(result.Error!));

        await _auditLogService.LogAsync(
            action: "ImportStarted",
            entityType: "ImportBatch",
            entityId: id);

        return Ok(ApiResponse<ImportBatchResponse>.Ok(MapToResponse(result.Value!)));
    }

    [HttpPost("{id:guid}/process")]
    public async Task<IActionResult> Process(Guid id)
    {
        var result = await _service.ProcessAsync(id);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<string>.Fail(result.Error!));

        await _auditLogService.LogAsync(
            action: "ImportProcessed",
            entityType: "ImportBatch",
            entityId: id);

        return Ok(ApiResponse<ImportBatchResponse>.Ok(MapToResponse(result.Value!)));
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, CompleteImportRequest request)
    {
        var result = await _service.CompleteAsync(id, request);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<string>.Fail(result.Error!));

        await _auditLogService.LogAsync(
            action: "ImportCompleted",
            entityType: "ImportBatch",
            entityId: id);

        return Ok(ApiResponse<ImportBatchResponse>.Ok(MapToResponse(result.Value!)));
    }

    [HttpPost("{id:guid}/fail")]
    public async Task<IActionResult> Fail(Guid id, FailImportRequest request)
    {
        var result = await _service.FailAsync(id, request);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<string>.Fail(result.Error!));

        await _auditLogService.LogAsync(
            action: "ImportFailed",
            entityType: "ImportBatch",
            entityId: id);

        return Ok(ApiResponse<ImportBatchResponse>.Ok(MapToResponse(result.Value!)));
    }

    [HttpGet("{id:guid}/errors")]
    public async Task<IActionResult> GetErrors(Guid id)
    {
        var result = await _service.GetByIdAsync(id);

        if (!result.IsSuccess)
            return NotFound(ApiResponse<IReadOnlyCollection<ImportErrorResponse>>.Fail(result.Error!));

        await _auditLogService.LogAsync(
            action: "ImportErrorsViewed",
            entityType: "ImportBatch",
            entityId: id);

        IReadOnlyCollection<ImportErrorResponse> errors = new List<ImportErrorResponse>();

        return Ok(ApiResponse<IReadOnlyCollection<ImportErrorResponse>>.Ok(errors));
    }

    private static ImportBatchResponse MapToResponse(ImportBatch importBatch)
    {
        return new ImportBatchResponse(
            importBatch.Id,
            importBatch.PstFilename,
            importBatch.PstHash,
            importBatch.MailboxId,
            importBatch.Mailbox?.DisplayName,
            importBatch.Mailbox?.OwnerUser?.Email,
            importBatch.Status.ToString(),
            importBatch.StartedAt,
            importBatch.CompletedAt,
            importBatch.TotalMessages,
            importBatch.ImportedMessages,
            importBatch.FailedMessages
        );
    }
}