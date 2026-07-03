using MailArchive.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MailArchive.Persistence.Configurations;

public class MailboxConfiguration : IEntityTypeConfiguration<Mailbox>
{
    public void Configure(EntityTypeBuilder<Mailbox> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.MailboxSource)
            .IsRequired();

        builder.Property(x => x.IsAssigned)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasIndex(x => x.OwnerUserId);

        builder.HasIndex(x => new { x.OwnerUserId, x.DisplayName })
            .IsUnique();

        builder
            .HasMany(x => x.Emails)
            .WithOne(x => x.Mailbox)
            .HasForeignKey(x => x.MailboxId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}