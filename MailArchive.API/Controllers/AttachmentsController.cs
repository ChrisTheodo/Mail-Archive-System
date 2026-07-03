using MailArchive.Application.Attachments;
using MailArchive.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MailArchive.API.Controllers;

[Authorize]
[ApiController]
[Route("api/attachments")]
public class AttachmentsController : ControllerBase
{
    private readonly IAttachmentService _service;
    private readonly IWebHostEnvironment _environment;

    public AttachmentsController(
        IAttachmentService service,
        IWebHostEnvironment environment)
    {
        _service = service;
        _environment = environment;
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id)
    {
        var result = await _service.GetByIdAsync(id);

        if (!result.IsSuccess)
            return NotFound(ApiResponse<string>.Fail(result.Error!));

        var attachment = result.Value!;

        if (string.IsNullOrWhiteSpace(attachment.StoragePath))
            return NotFound(ApiResponse<string>.Fail("AttachmentStoragePathMissing"));

        var filePath = ResolveStoragePath(attachment.StoragePath);

        if (!System.IO.File.Exists(filePath))
            return NotFound(ApiResponse<string>.Fail("AttachmentFileNotFound"));

        var contentType = string.IsNullOrWhiteSpace(attachment.ContentType)
            ? "application/octet-stream"
            : attachment.ContentType;

        return PhysicalFile(filePath, contentType, attachment.FileName);
    }

    private string ResolveStoragePath(string storagePath)
    {
        if (Path.IsPathRooted(storagePath))
            return storagePath;

        return Path.GetFullPath(Path.Combine(_environment.ContentRootPath, storagePath));
    }
}