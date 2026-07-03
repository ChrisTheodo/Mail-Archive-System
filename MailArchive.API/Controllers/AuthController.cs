using MailArchive.Application.Auth;
using MailArchive.Application.Common;
using MailArchive.Application.Contracts.Auth;
using Microsoft.AspNetCore.Mvc;

namespace MailArchive.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _service;

    public AuthController(IAuthService service)
    {
        _service = service;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var result = await _service.LoginAsync(request);

        if (!result.IsSuccess)
            return Unauthorized(ApiResponse<string>.Fail(result.Error!));

        return Ok(ApiResponse<LoginResponse>.Ok(result.Value!));
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        return Ok(ApiResponse<string>.Ok("LoggedOut"));
    }
}