using MailArchive.Application.Audit;
using MailArchive.Application.Common;
using MailArchive.Application.Contracts.Users;
using MailArchive.Application.Users;
using MailArchive.Application.Users.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MailArchive.API.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IUserService _service;
    private readonly IAuditLogService _auditLogService;

    public UsersController(
        IUserService service,
        IAuditLogService auditLogService)
    {
        _service = service;
        _auditLogService = auditLogService;
    }

    // GET: api/users?page=1&pageSize=20&search=abc
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] UserQueryParameters query)
    {
        var result = await _service.GetPagedAsync(query);

        await _auditLogService.LogAsync(
            action: "UserListViewed",
            entityType: "User");

        var mapped = new PagedResult<UserResponse>
        {
            Items = result.Items.Select(x => new UserResponse(
                x.Id,
                x.Email,
                x.DisplayName,
                x.IsActive
            )).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };

        return Ok(ApiResponse<PagedResult<UserResponse>>.Ok(mapped));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _service.GetByIdAsync(id);

        if (!result.IsSuccess)
            return NotFound(ApiResponse<UserResponse>.Fail(result.Error!));

        await _auditLogService.LogAsync(
            action: "UserViewed",
            entityType: "User",
            entityId: id);

        var x = result.Value!;

        var response = new UserResponse(
            x.Id,
            x.Email,
            x.DisplayName,
            x.IsActive
        );

        return Ok(ApiResponse<UserResponse>.Ok(response));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserRequest request)
    {
        var result = await _service.CreateAsync(request);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<string>.Fail(result.Error!));

        var x = result.Value!;

        await _auditLogService.LogAsync(
            action: "UserCreated",
            entityType: "User",
            entityId: x.Id);

        var response = new UserResponse(
            x.Id,
            x.Email,
            x.DisplayName,
            x.IsActive
        );

        return Ok(ApiResponse<UserResponse>.Ok(response));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateUserRequest request)
    {
        var result = await _service.UpdateAsync(id, request);

        if (!result.IsSuccess)
            return NotFound(ApiResponse<string>.Fail(result.Error!));

        var x = result.Value!;

        await _auditLogService.LogAsync(
            action: "UserUpdated",
            entityType: "User",
            entityId: x.Id);

        var response = new UserResponse(
            x.Id,
            x.Email,
            x.DisplayName,
            x.IsActive
        );

        return Ok(ApiResponse<UserResponse>.Ok(response));
    }
}