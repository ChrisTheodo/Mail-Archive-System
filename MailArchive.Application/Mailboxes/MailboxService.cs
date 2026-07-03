using MailArchive.Application.Common;
using MailArchive.Domain.Entities;
using MailArchive.Application.Contracts.Mailboxes;
using MailArchive.Application.Abstractions;
using MailArchive.Application.Mailboxes.Queries;
using Microsoft.EntityFrameworkCore;

namespace MailArchive.Application.Mailboxes;

public class MailboxService : IMailboxService
{
    private readonly IMailArchiveDbContext _db;

    public MailboxService(IMailArchiveDbContext db)
    {
        _db = db;
    }

    public async Task<Result<List<Mailbox>>> GetAllAsync()
    {
        var mailboxes = await _db.Mailboxes
            .Include(x => x.OwnerUser)
            .OrderBy(x => x.DisplayName)
            .ToListAsync();

        return Result<List<Mailbox>>.Success(mailboxes);
    }

    public async Task<Result<Mailbox>> GetByIdAsync(Guid id)
    {
        var mailbox = await _db.Mailboxes
            .Include(x => x.OwnerUser)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (mailbox == null)
            return Result<Mailbox>.Failure("MailboxNotFound");

        return Result<Mailbox>.Success(mailbox);
    }

    public async Task<Result<Mailbox>> CreateAsync(CreateMailboxRequest request)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(x => x.Id == request.OwnerUserId);

        if (user == null)
            return Result<Mailbox>.Failure("UserNotFound");

        if (!user.IsActive)
            return Result<Mailbox>.Failure("UserInactive");

        var displayName = request.DisplayName.Trim();

        var exists = await _db.Mailboxes
            .AnyAsync(x =>
                x.OwnerUserId == request.OwnerUserId &&
                x.DisplayName.ToLower() == displayName.ToLower());

        if (exists)
            return Result<Mailbox>.Failure("MailboxAlreadyExists");

        var mailbox = new Mailbox
        {
            Id = Guid.NewGuid(),
            OwnerUserId = request.OwnerUserId,
            DisplayName = displayName,
            CreatedAt = DateTime.UtcNow,
            IsAssigned = true
        };

        _db.Mailboxes.Add(mailbox);
        await _db.SaveChangesAsync();

        return Result<Mailbox>.Success(mailbox);
    }

    public async Task<Result<Mailbox>> UpdateAsync(Guid id, UpdateMailboxRequest request)
    {
        var mailbox = await _db.Mailboxes
            .FirstOrDefaultAsync(x => x.Id == id);

        if (mailbox == null)
            return Result<Mailbox>.Failure("MailboxNotFound");

        var displayName = request.DisplayName.Trim();

        var exists = await _db.Mailboxes
            .AnyAsync(x =>
                x.OwnerUserId == mailbox.OwnerUserId &&
                x.DisplayName.ToLower() == displayName.ToLower() &&
                x.Id != id);

        if (exists)
            return Result<Mailbox>.Failure("MailboxAlreadyExists");

        mailbox.DisplayName = displayName;

        await _db.SaveChangesAsync();

        return Result<Mailbox>.Success(mailbox);
    }

    public async Task<PagedResult<Mailbox>> GetPagedAsync(MailboxQueryParameters query)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 20 : query.PageSize;
        pageSize = pageSize > 100 ? 100 : pageSize;

        var baseQuery = _db.Mailboxes
            .Include(x => x.OwnerUser)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();

            baseQuery = baseQuery.Where(x =>
                x.DisplayName.ToLower().Contains(search) ||
                x.OwnerUser.Email.ToLower().Contains(search));
        }

        var total = await baseQuery.CountAsync();

        var items = await baseQuery
            .OrderBy(x => x.DisplayName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<Mailbox>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }
}