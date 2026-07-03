using System.Security.Claims;
using MailArchive.Application.Abstractions;
using MailArchive.Domain.Enums;

namespace MailArchive.API.Security;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public bool IsAuthenticated
    {
        get
        {
            return _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;
        }
    }

    public Guid? UserId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (Guid.TryParse(value, out var userId))
                return userId;

            return null;
        }
    }

    public string? Email
    {
        get
        {
            return _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Email);
        }
    }

    public string? Role
    {
        get
        {
            return _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Role);
        }
    }

    public bool IsAdmin
    {
        get
        {
            return string.Equals(
                Role,
                UserRole.Admin.ToString(),
                StringComparison.OrdinalIgnoreCase);
        }
    }
}