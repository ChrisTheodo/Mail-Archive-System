using System.Security.Cryptography;
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
    private readonly IWebHostEnvironment _environment;

    public ImportsController(
        IImportService service,
        IAuditLogService auditLogService,
        IWebHostEnvironment environment)
    {
        _service = service;
        _auditLogService = auditLogService;
        _environment = environment;
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

    [HttpPost("pst/upload")]
    [RequestSizeLimit(1024L * 1024L * 1024L)]
    public async Task<IActionResult> UploadPstImport(
        [FromForm] Guid mailboxId,
        [FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<string>.Fail("PstFileRequired"));

        var originalFileName = Path.GetFileName(file.FileName);

        if (string.IsNullOrWhiteSpace(originalFileName))
            return BadRequest(ApiResponse<string>.Fail("PstFilenameRequired"));

        if (!originalFileName.EndsWith(".pst", StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse<string>.Fail("OnlyPstFilesAreAllowed"));

        var pstHash = await CalculateSha256Async(file);

        var storageRoot = Path.Combine(
            _environment.ContentRootPath,
            "storage",
            "imports");

        Directory.CreateDirectory(storageRoot);

        var storedFileName = $"{Guid.NewGuid():N}_{SanitizeFileName(originalFileName)}";
        var fullPath = Path.Combine(storageRoot, storedFileName);

        await using (var stream = System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(stream);
        }

        var relativeStoragePath = Path.Combine("storage", "imports", storedFileName)
            .Replace("\\", "/");

        var request = new CreatePstImportRequest(
            mailboxId,
            originalFileName,
            pstHash,
            relativeStoragePath);

        var result = await _service.CreatePstImportAsync(request);

        if (!result.IsSuccess)
        {
            if (System.IO.File.Exists(fullPath))
                System.IO.File.Delete(fullPath);

            return BadRequest(ApiResponse<string>.Fail(result.Error!));
        }

        var importBatch = result.Value!;

        await _auditLogService.LogAsync(
            action: "PstImportUploaded",
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
        {
            await _auditLogService.LogAsync(
                action: "ImportProcessFailed",
                entityType: "ImportBatch",
                entityId: id);

            return BadRequest(ApiResponse<string>.Fail(result.Error!));
        }

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
        var importResult = await _service.GetByIdAsync(id);

        if (!importResult.IsSuccess)
            return NotFound(ApiResponse<IReadOnlyCollection<ImportErrorResponse>>.Fail(importResult.Error!));

        var errors = await _service.GetErrorsAsync(id);

        await _auditLogService.LogAsync(
            action: "ImportErrorsViewed",
            entityType: "ImportBatch",
            entityId: id);

        var response = errors
            .Select(MapToErrorResponse)
            .ToList();

        return Ok(ApiResponse<IReadOnlyCollection<ImportErrorResponse>>.Ok(response));
    }

    private static ImportBatchResponse MapToResponse(ImportBatch importBatch)
    {
        return new ImportBatchResponse(
            importBatch.Id,
            importBatch.PstFilename,
            importBatch.PstHash,
            importBatch.PstStoragePath,
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

    private static ImportErrorResponse MapToErrorResponse(ImportError error)
    {
        return new ImportErrorResponse(
            error.Id,
            error.ImportBatchId,
            error.Message,
            error.CreatedAt
        );
    }

    private static async Task<string> CalculateSha256Async(IFormFile file)
    {
        await using var stream = file.OpenReadStream();

        using var sha256 = SHA256.Create();

        var hashBytes = await sha256.ComputeHashAsync(stream);

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();

        var sanitized = new string(
            fileName.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());

        return string.IsNullOrWhiteSpace(sanitized)
            ? "uploaded.pst"
            : sanitized;
    }
}