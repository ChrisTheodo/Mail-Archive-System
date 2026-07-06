using MailArchive.Application.Common;
using MailArchive.Application.Contracts.Auth;

namespace MailArchive.Application.Auth;

public interface IAuthService
{
    Task<Result<LoginResponse>> LoginAsync(LoginRequest request);

    Task<Result<LoginResponse>> RefreshAsync(RefreshTokenRequest request);

    Task<Result<string>> RevokeRefreshTokenAsync(RefreshTokenRequest request);
}