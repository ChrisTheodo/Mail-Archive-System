using MailArchive.Application.Common;
using MailArchive.Application.Contracts.Emails;
using MailArchive.Application.Emails;
using MailArchive.Application.Emails.Queries;
using MailArchive.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MailArchive.API.Controllers;

[Authorize]
[ApiController]
[Route("api/emails")]
public class EmailsController : ControllerBase
{
    private readonly IEmailService _service;

    public EmailsController(IEmailService service)
    {
        _service = service;
    }

    // GET: api/emails?page=1&pageSize=20&search=invoice
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] EmailQueryParameters query)
    {
        var result = await _service.GetPagedAsync(query);

        var mapped = new PagedResult<EmailListResponse>
        {
            Items = result.Items.Select(MapToListResponse).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };

        return Ok(ApiResponse<PagedResult<EmailListResponse>>.Ok(mapped));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _service.GetByIdAsync(id);

        if (!result.IsSuccess)
            return NotFound(ApiResponse<EmailDetailsResponse>.Fail(result.Error!));

        var response = MapToDetailsResponse(result.Value!);

        return Ok(ApiResponse<EmailDetailsResponse>.Ok(response));
    }

    [HttpGet("{id:guid}/attachments")]
    public async Task<IActionResult> GetAttachments(Guid id)
    {
        var result = await _service.GetAttachmentsByEmailIdAsync(id);

        if (!result.IsSuccess)
            return NotFound(ApiResponse<IReadOnlyCollection<EmailAttachmentResponse>>.Fail(result.Error!));

        var response = result.Value!
            .Select(MapToAttachmentResponse)
            .ToList();

        return Ok(ApiResponse<IReadOnlyCollection<EmailAttachmentResponse>>.Ok(response));
    }

    private static EmailListResponse MapToListResponse(Email email)
    {
        var recipientEmails = email.Recipients
            .Select(x => x.RecipientEmail)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        var attachmentFileNames = email.Attachments
            .Select(x => x.FileName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        return new EmailListResponse(
            email.Id,
            email.MailboxId,
            email.Mailbox?.DisplayName,
            email.InternetMessageId,
            email.FolderPath,
            email.SenderEmail,
            email.SenderName,
            email.Subject,
            email.SentAt,
            email.ReceivedAt,
            email.HasAttachments,
            recipientEmails,
            attachmentFileNames
        );
    }

    private static EmailDetailsResponse MapToDetailsResponse(Email email)
    {
        return new EmailDetailsResponse(
            email.Id,
            email.MailboxId,
            email.Mailbox?.DisplayName,
            email.ImportBatchId,
            email.InternetMessageId,
            email.MessageHash,
            email.FolderPath,
            email.SenderEmail,
            email.SenderName,
            email.Subject,
            email.BodyText,
            email.BodyHtml,
            email.SentAt,
            email.ReceivedAt,
            email.HasAttachments,
            email.CreatedAt,
            email.Recipients.Select(x => new EmailRecipientResponse(
                x.Id,
                x.RecipientType.ToString(),
                x.RecipientEmail,
                x.RecipientName
            )).ToList(),
            email.Attachments.Select(MapToAttachmentResponse).ToList()
        );
    }

    private static EmailAttachmentResponse MapToAttachmentResponse(Attachment attachment)
    {
        return new EmailAttachmentResponse(
            attachment.Id,
            attachment.FileName,
            attachment.ContentType,
            attachment.SizeBytes,
            attachment.StoragePath,
            attachment.ContentHash
        );
    }
}