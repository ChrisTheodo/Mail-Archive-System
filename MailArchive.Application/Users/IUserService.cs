using MailArchive.Application.Common;
using MailArchive.Application.Contracts.Users;
using MailArchive.Application.Users.Queries;
using MailArchive.Domain.Entities;

namespace MailArchive.Application.Users;

public interface IUserService
{
    Task<PagedResult<User>> GetPagedAsync(UserQueryParameters query);

    Task<Result<User>> GetByIdAsync(Guid id);

    Task<Result<User>> CreateAsync(CreateUserRequest request);

    Task<Result<User>> UpdateAsync(Guid id, UpdateUserRequest request);
}