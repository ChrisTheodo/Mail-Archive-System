namespace MailArchive.Application.Abstractions;

public interface ICurrentUserService
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }

    string? Email { get; }

    string? Role { get; }

    string? IpAddress { get; }

    bool IsAdmin { get; }
}