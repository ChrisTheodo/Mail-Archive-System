using MailArchive.Application.Audit;
using MailArchive.Application.Common;
using MailArchive.Application.Contracts.Emails;
using MailArchive.Application.Emails;
using MailArchive.Application.Emails.Queries;
using MailArchive.Application.Emails.Search;
using MailArchive.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MailArchive.API.Controllers;

[ApiController]
[Authorize]
[Route("api/emails")]
public class EmailsController : ControllerBase
{
    private readonly IEmailService _service;
    private readonly IAuditLogService _auditLogService;

    public EmailsController(
        IEmailService service,
        IAuditLogService auditLogService)
    {
        _service = service;
        _auditLogService = auditLogService;
    }

    [HttpGet]
    public async Task<IActionResult> GetEmails([FromQuery] EmailQueryParameters query)
    {
        var result = await _service.GetPagedAsync(query);

        await _auditLogService.LogAsync(
            action: "EmailSearch",
            entityType: "Email");

        var response = new PagedResult<EmailListResponse>
        {
            Items = result.Items
                .Select(email => MapToListResponse(email, query.Search))
                .ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };

        return Ok(ApiResponse<PagedResult<EmailListResponse>>.Ok(response));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetEmailById(Guid id)
    {
        var result = await _service.GetByIdAsync(id);

        if (!result.IsSuccess)
            return NotFound(ApiResponse<string>.Fail(result.Error!));

        await _auditLogService.LogAsync(
            action: "EmailViewed",
            entityType: "Email",
            entityId: id);

        return Ok(ApiResponse<EmailDetailsResponse>.Ok(
            MapToDetailsResponse(result.Value!)));
    }

    [HttpGet("{id:guid}/attachments")]
    public async Task<IActionResult> GetAttachments(Guid id)
    {
        var result = await _service.GetAttachmentsByEmailIdAsync(id);

        if (!result.IsSuccess)
            return NotFound(ApiResponse<string>.Fail(result.Error!));

        await _auditLogService.LogAsync(
            action: "EmailAttachmentsViewed",
            entityType: "Email",
            entityId: id);

        var response = result.Value!
            .Select(MapToAttachmentResponse)
            .ToList();

        return Ok(ApiResponse<List<EmailAttachmentResponse>>.Ok(response));
    }

    private static EmailListResponse MapToListResponse(
        Email email,
        string? search)
    {
        return new EmailListResponse(
            email.Id,
            email.MailboxId,
            email.Mailbox.DisplayName,
            email.InternetMessageId,
            email.FolderPath ?? string.Empty,
            email.SenderEmail ?? string.Empty,
            email.SenderName,
            email.Subject,
            email.SentAt,
            email.ReceivedAt,
            email.HasAttachments,
            email.Recipients
                .Select(x => x.RecipientEmail)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList(),
            email.Attachments
                .Select(x => x.FileName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList(),
            EmailSearchSnippet.Create(email, search)
        );
    }

    private static EmailDetailsResponse MapToDetailsResponse(Email email)
    {
        return new EmailDetailsResponse(
            email.Id,
            email.MailboxId,
            email.Mailbox.DisplayName,
            email.ImportBatchId,
            email.InternetMessageId,
            email.MessageHash ?? string.Empty,
            email.FolderPath ?? string.Empty,
            email.SenderEmail ?? string.Empty,
            email.SenderName,
            email.Subject,
            email.BodyText,
            email.BodyHtml,
            email.SentAt,
            email.ReceivedAt,
            email.HasAttachments,
            email.CreatedAt,
            email.Recipients
                .OrderBy(x => x.RecipientType)
                .ThenBy(x => x.RecipientEmail)
                .Select(MapToRecipientResponse)
                .ToList(),
            email.Attachments
                .OrderBy(x => x.FileName)
                .Select(MapToAttachmentResponse)
                .ToList()
        );
    }

    private static EmailRecipientResponse MapToRecipientResponse(
        EmailRecipient recipient)
    {
        return new EmailRecipientResponse(
            recipient.Id,
            recipient.RecipientType.ToString(),
            recipient.RecipientEmail,
            recipient.RecipientName
        );
    }

    private static EmailAttachmentResponse MapToAttachmentResponse(
        Attachment attachment)
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