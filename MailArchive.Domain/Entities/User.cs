using MailArchive.Domain.Enums;

namespace MailArchive.Domain.Entities;

public class User
{
    public Guid Id { get; set; }

    public string Email { get; set; } = null!;
    public string DisplayName { get; set; } = null!;

    // authentication
    public string? PasswordHash { get; set; }
    
    public UserRole Role { get; set; } = UserRole.User;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    public ICollection<Mailbox> Mailboxes { get; set; } = new List<Mailbox>();
}
