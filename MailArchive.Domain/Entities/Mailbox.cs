using MailArchive.Domain.Enums;

namespace MailArchive.Domain.Entities;

public class Mailbox
{
    public Guid Id { get; set; }
    public User OwnerUser { get; set; } = null!;
    public Guid? OwnerUserId { get; set; }
    public string DisplayName { get; set; } = null!;
    public MailboxSourceType MailboxSource { get; set; } = MailboxSourceType.PST;
    
    public bool IsAssigned { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public ICollection<Email> Emails { get; set; } = new List<Email>();
}