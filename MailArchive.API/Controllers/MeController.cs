using System.Security.Claims;
using MailArchive.Application.Common;
using MailArchive.Application.Contracts.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MailArchive.API.Controllers;

[ApiController]
[Route("api/me")]
public class MeController : ControllerBase
{
    [Authorize]
    [HttpGet]
    public IActionResult Get()
    {
        var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = User.FindFirstValue(ClaimTypes.Email);
        var displayName = User.FindFirstValue(ClaimTypes.Name);
        var role = User.FindFirstValue(ClaimTypes.Role);

        if (!Guid.TryParse(idValue, out var userId))
            return Unauthorized(ApiResponse<string>.Fail("InvalidToken"));

        if (string.IsNullOrWhiteSpace(email))
            return Unauthorized(ApiResponse<string>.Fail("InvalidToken"));

        var response = new CurrentUserResponse(
            userId,
            email,
            displayName ?? string.Empty,
            role ?? string.Empty
        );

        return Ok(ApiResponse<CurrentUserResponse>.Ok(response));
    }
}