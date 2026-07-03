using MailArchive.Application.Abstractions;
using MailArchive.Application.Common;
using MailArchive.Application.Contracts.Auth;
using MailArchive.Application.Contracts.Users;
using Microsoft.EntityFrameworkCore;

namespace MailArchive.Application.Auth;

public class AuthService : IAuthService
{
    private readonly IMailArchiveDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public AuthService(
        IMailArchiveDbContext db,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<Result<LoginResponse>> LoginAsync(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _db.Users
            .FirstOrDefaultAsync(x => x.Email == email);

        if (user == null)
            return Result<LoginResponse>.Failure("InvalidCredentials");

        if (!user.IsActive)
            return Result<LoginResponse>.Failure("UserInactive");

        if (string.IsNullOrWhiteSpace(user.PasswordHash))
            return Result<LoginResponse>.Failure("PasswordNotConfigured");

        var passwordValid = _passwordHasher.Verify(request.Password, user.PasswordHash);

        if (!passwordValid)
            return Result<LoginResponse>.Failure("InvalidCredentials");

        var token = _jwtTokenGenerator.GenerateToken(user);

        var response = new LoginResponse(
            token.AccessToken,
            "Bearer",
            token.ExpiresAtUtc,
            new UserResponse(
                user.Id,
                user.Email,
                user.DisplayName,
                user.IsActive
            )
        );

        return Result<LoginResponse>.Success(response);
    }
}