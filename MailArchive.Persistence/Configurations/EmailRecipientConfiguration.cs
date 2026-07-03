using MailArchive.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MailArchive.Persistence.Configurations;

public class EmailRecipientConfiguration : IEntityTypeConfiguration<EmailRecipient>
{
    public void Configure(EntityTypeBuilder<EmailRecipient> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RecipientType)
            .IsRequired();

        builder.Property(x => x.RecipientEmail)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.RecipientName)
            .HasMaxLength(200);

        builder.HasIndex(x => x.EmailId);

        builder.HasIndex(x => x.RecipientEmail);

        builder.HasIndex(x => new { x.EmailId, x.RecipientType });

        builder.HasIndex(x => new { x.RecipientEmail, x.RecipientType });
    }
}