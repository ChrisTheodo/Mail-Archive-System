using MailArchive.Application.Abstractions;
using MailArchive.Application.Contracts.Users;
using MailArchive.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MailArchive.Application.Users;

public class UserService : IUserService
{
    private readonly IMailArchiveDbContext _db;

    public UserService(IMailArchiveDbContext db)
    {
        _db = db;
    }

    public async Task<List<User>> GetAllAsync()
    {
        return await _db.Users.ToListAsync();
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        return await _db.Users.FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<User> CreateAsync(CreateUserRequest request)
    {
        var emailExists = await _db.Users
            .AnyAsync(x => x.Email == request.Email);

        if (emailExists)
        {
            throw new InvalidOperationException("Email already exists");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            DisplayName = request.DisplayName,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return user;
    }

    public async Task<User?> UpdateAsync(Guid id, UpdateUserRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (user == null) return null;

        user.DisplayName = request.DisplayName;
        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return user;
    }
}