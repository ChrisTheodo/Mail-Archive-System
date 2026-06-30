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

        var exists = await _db.Mailboxes
            .AnyAsync(x =>
                x.OwnerUserId == request.OwnerUserId &&
                x.DisplayName == request.DisplayName);

        if (exists)
            return Result<Mailbox>.Failure("MailboxAlreadyExists");

        var mailbox = new Mailbox
        {
            Id = Guid.NewGuid(),
            OwnerUserId = request.OwnerUserId,
            DisplayName = request.DisplayName,
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

        var exists = await _db.Mailboxes
            .AnyAsync(x =>
                x.OwnerUserId == mailbox.OwnerUserId &&
                x.DisplayName == request.DisplayName &&
                x.Id != id);

        if (exists)
            return Result<Mailbox>.Failure("MailboxAlreadyExists");

        mailbox.DisplayName = request.DisplayName;

        await _db.SaveChangesAsync();

        return Result<Mailbox>.Success(mailbox);
    }
    
    public async Task<PagedResult<Mailbox>> GetPagedAsync(MailboxQueryParameters query)
    {
        var baseQuery = _db.Mailboxes
            .Include(x => x.OwnerUser)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            baseQuery = baseQuery.Where(x =>
                x.DisplayName.Contains(query.Search));
        }

        var total = await baseQuery.CountAsync();

        var items = await baseQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return new PagedResult<Mailbox>
        {
            Items = items,
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }
}