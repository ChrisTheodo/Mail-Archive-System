namespace MailArchive.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; set; }

    // Who did it
    public Guid? UserId { get; set; }
    public User? User { get; set; }

    // What happened
    public string Action { get; set; } = null!;

    // What was affected
    public string EntityType { get; set; } = null!;
    public Guid? EntityId { get; set; }

    // Context
    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}