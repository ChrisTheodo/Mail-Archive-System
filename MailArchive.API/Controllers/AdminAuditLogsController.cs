using MailArchive.Application.Audit;
using MailArchive.Application.Audit.Queries;
using MailArchive.Application.Common;
using MailArchive.Application.Contracts.Audit;
using MailArchive.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MailArchive.API.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/audit-logs")]
public class AdminAuditLogsController : ControllerBase
{
    private readonly IAuditLogService _service;

    public AdminAuditLogsController(IAuditLogService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] AuditLogQueryParameters query)
    {
        var result = await _service.GetPagedAsync(query);

        var mapped = new PagedResult<AuditLogResponse>
        {
            Items = result.Items.Select(MapToResponse).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };

        return Ok(ApiResponse<PagedResult<AuditLogResponse>>.Ok(mapped));
    }

    private static AuditLogResponse MapToResponse(AuditLog auditLog)
    {
        return new AuditLogResponse(
            auditLog.Id,
            auditLog.UserId,
            auditLog.User?.Email,
            auditLog.Action,
            auditLog.EntityType,
            auditLog.EntityId,
            auditLog.IpAddress,
            auditLog.CreatedAt
        );
    }
}