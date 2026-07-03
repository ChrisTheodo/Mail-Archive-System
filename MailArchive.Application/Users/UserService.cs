using MailArchive.Application.Abstractions;
using MailArchive.Application.Common;
using MailArchive.Application.Contracts.Users;
using MailArchive.Application.Users.Queries;
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

    public async Task<PagedResult<User>> GetPagedAsync(UserQueryParameters query)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 20 : query.PageSize;
        pageSize = pageSize > 100 ? 100 : pageSize;

        var baseQuery = _db.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();

            baseQuery = baseQuery.Where(x =>
                x.Email.ToLower().Contains(search) ||
                x.DisplayName.ToLower().Contains(search));
        }

        var total = await baseQuery.CountAsync();

        var users = await baseQuery
            .OrderBy(x => x.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<User>
        {
            Items = users,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<Result<User>> GetByIdAsync(Guid id)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(x => x.Id == id);

        if (user == null)
            return Result<User>.Failure("UserNotFound");

        return Result<User>.Success(user);
    }

    public async Task<Result<User>> CreateAsync(CreateUserRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var displayName = request.DisplayName.Trim();

        var emailExists = await _db.Users
            .AnyAsync(x => x.Email.ToLower() == normalizedEmail);

        if (emailExists)
            return Result<User>.Failure("UserAlreadyExists");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            DisplayName = displayName,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Result<User>.Success(user);
    }

    public async Task<Result<User>> UpdateAsync(Guid id, UpdateUserRequest request)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(x => x.Id == id);

        if (user == null)
            return Result<User>.Failure("UserNotFound");

        user.DisplayName = request.DisplayName.Trim();
        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Result<User>.Success(user);
    }
}