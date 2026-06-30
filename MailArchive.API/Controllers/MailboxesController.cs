using MailArchive.Application.Contracts.Mailboxes;
using MailArchive.Application.Contracts;
using MailArchive.Domain.Entities;
using MailArchive.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MailArchive.API.Controllers;

[ApiController]
[Route("api/mailboxes")]
public class MailboxesController : ControllerBase
{
    private readonly MailArchiveDbContext _db;

    public MailboxesController(MailArchiveDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var mailboxes = await _db.Mailboxes
            .Include(m => m.OwnerUser)
            .ToListAsync();

        return Ok(mailboxes);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var mailbox = await _db.Mailboxes
            .Include(m => m.OwnerUser)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (mailbox == null) return NotFound();
        return Ok(mailbox);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateMailboxRequest request)
    {
        var user = await _db.Users.FindAsync(request.OwnerUserId);
        if (user == null) return BadRequest("User not found");

        var mailbox = new Mailbox
        {
            Id = Guid.NewGuid(),
            OwnerUserId = request.OwnerUserId,
            DisplayName = request.DisplayName,
            CreatedAt = DateTime.UtcNow
        };

        _db.Mailboxes.Add(mailbox);
        await _db.SaveChangesAsync();

        return Ok(mailbox);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateMailboxRequest request)
    {
        var mailbox = await _db.Mailboxes.FindAsync(id);
        if (mailbox == null) return NotFound();

        mailbox.DisplayName = request.DisplayName;

        await _db.SaveChangesAsync();
        return Ok(mailbox);
    }
}