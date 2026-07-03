using MailArchive.Domain.Entities;

namespace MailArchive.Application.Auth;

public interface IJwtTokenGenerator
{
    JwtTokenResult GenerateToken(User user);
}