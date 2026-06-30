using MailArchive.Application.Contracts.Users;
using MailArchive.Domain.Entities;

namespace MailArchive.Application.Users;

public interface IUserService
{
    Task<List<User>> GetAllAsync();
    Task<User?> GetByIdAsync(Guid id);
    Task<User> CreateAsync(CreateUserRequest request);
    Task<User?> UpdateAsync(Guid id, UpdateUserRequest request);
}