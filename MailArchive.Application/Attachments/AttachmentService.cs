using MailArchive.Application.Abstractions;
using MailArchive.Application.Common;
using MailArchive.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MailArchive.Application.Attachments;

public class AttachmentService : IAttachmentService
{
    private readonly IMailArchiveDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public AttachmentService(
        IMailArchiveDbContext db,
        ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<Attachment>> GetByIdAsync(Guid id)
    {
        var query = _db.Attachments
            .Include(x => x.Email)
            .ThenInclude(x => x.Mailbox)
            .Where(x => x.Id == id)
            .AsQueryable();

        query = ApplyUserIsolation(query);

        var attachment = await query.FirstOrDefaultAsync();

        if (attachment == null)
            return Result<Attachment>.Failure("AttachmentNotFound");

        return Result<Attachment>.Success(attachment);
    }

    private IQueryable<Attachment> ApplyUserIsolation(IQueryable<Attachment> query)
    {
        if (_currentUser.IsAdmin)
            return query;

        var currentUserId = _currentUser.UserId;

        if (!currentUserId.HasValue)
            return query.Where(x => false);

        return query.Where(x => x.Email.Mailbox.OwnerUserId == currentUserId.Value);
    }
}