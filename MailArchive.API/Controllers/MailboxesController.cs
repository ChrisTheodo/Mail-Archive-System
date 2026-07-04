using MailArchive.Application.Audit;
using MailArchive.Application.Common;
using MailArchive.Application.Contracts.Mailboxes;
using MailArchive.Application.Mailboxes;
using MailArchive.Application.Mailboxes.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MailArchive.API.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/mailboxes")]
public class MailboxesController : ControllerBase
{
    private readonly IMailboxService _service;
    private readonly IAuditLogService _auditLogService;

    public MailboxesController(
        IMailboxService service,
        IAuditLogService auditLogService)
    {
        _service = service;
        _auditLogService = auditLogService;
    }

    // GET: api/mailboxes?page=1&pageSize=20&search=abc
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] MailboxQueryParameters query)
    {
        var result = await _service.GetPagedAsync(query);

        await _auditLogService.LogAsync(
            action: "MailboxListViewed",
            entityType: "Mailbox");

        var mapped = new PagedResult<MailboxResponse>
        {
            Items = result.Items.Select(x => new MailboxResponse(
                x.Id,
                x.DisplayName,
                x.OwnerUserId,
                x.OwnerUser?.Email
            )).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };

        return Ok(ApiResponse<PagedResult<MailboxResponse>>.Ok(mapped));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _service.GetByIdAsync(id);

        if (!result.IsSuccess)
            return NotFound(ApiResponse<MailboxResponse>.Fail(result.Error!));

        await _auditLogService.LogAsync(
            action: "MailboxViewed",
            entityType: "Mailbox",
            entityId: id);

        var x = result.Value!;

        var response = new MailboxResponse(
            x.Id,
            x.DisplayName,
            x.OwnerUserId,
            x.OwnerUser?.Email
        );

        return Ok(ApiResponse<MailboxResponse>.Ok(response));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateMailboxRequest request)
    {
        var result = await _service.CreateAsync(request);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<string>.Fail(result.Error!));

        var x = result.Value!;

        await _auditLogService.LogAsync(
            action: "MailboxCreated",
            entityType: "Mailbox",
            entityId: x.Id);

        var response = new MailboxResponse(
            x.Id,
            x.DisplayName,
            x.OwnerUserId,
            x.OwnerUser?.Email
        );

        return Ok(ApiResponse<MailboxResponse>.Ok(response));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateMailboxRequest request)
    {
        var result = await _service.UpdateAsync(id, request);

        if (!result.IsSuccess)
            return NotFound(ApiResponse<string>.Fail(result.Error!));

        var x = result.Value!;

        await _auditLogService.LogAsync(
            action: "MailboxUpdated",
            entityType: "Mailbox",
            entityId: x.Id);

        var response = new MailboxResponse(
            x.Id,
            x.DisplayName,
            x.OwnerUserId,
            x.OwnerUser?.Email
        );

        return Ok(ApiResponse<MailboxResponse>.Ok(response));
    }
}