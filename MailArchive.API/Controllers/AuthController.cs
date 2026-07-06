using MailArchive.Application.Audit;
using MailArchive.Application.Auth;
using MailArchive.Application.Common;
using MailArchive.Application.Contracts.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MailArchive.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _service;
    private readonly IAuditLogService _auditLogService;

    public AuthController(
        IAuthService service,
        IAuditLogService auditLogService)
    {
        _service = service;
        _auditLogService = auditLogService;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var result = await _service.LoginAsync(request);

        if (!result.IsSuccess)
            return Unauthorized(ApiResponse<string>.Fail(result.Error!));

        return Ok(ApiResponse<LoginResponse>.Ok(result.Value!));
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshTokenRequest request)
    {
        var result = await _service.RefreshAsync(request);

        if (!result.IsSuccess)
            return Unauthorized(ApiResponse<string>.Fail(result.Error!));

        return Ok(ApiResponse<LoginResponse>.Ok(result.Value!));
    }

    [AllowAnonymous]
    [HttpPost("refresh/revoke")]
    public async Task<IActionResult> RevokeRefreshToken(RefreshTokenRequest request)
    {
        var result = await _service.RevokeRefreshTokenAsync(request);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<string>.Fail(result.Error!));

        return Ok(ApiResponse<string>.Ok(result.Value!));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _auditLogService.LogAsync(
            action: "Logout",
            entityType: "Auth");

        return Ok(ApiResponse<string>.Ok("LoggedOut"));
    }
}