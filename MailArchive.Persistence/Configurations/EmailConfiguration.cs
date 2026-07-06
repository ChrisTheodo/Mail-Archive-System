using MailArchive.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MailArchive.Persistence.Configurations;

public class EmailConfiguration : IEntityTypeConfiguration<Email>
{
    public void Configure(EntityTypeBuilder<Email> builder)
    {
        builder.ToTable("emails");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.InternetMessageId)
            .HasMaxLength(500);

        builder.Property(x => x.MessageHash)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(x => x.FolderPath)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.SenderEmail)
            .IsRequired()
            .HasMaxLength(320);

        builder.Property(x => x.SenderName)
            .HasMaxLength(300);

        builder.Property(x => x.Subject)
            .HasMaxLength(1000);

        builder.Property(x => x.BodyText);

        builder.Property(x => x.BodyHtml);

        builder.Property(x => x.HasAttachments)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasOne(x => x.Mailbox)
            .WithMany(x => x.Emails)
            .HasForeignKey(x => x.MailboxId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ImportBatch)
            .WithMany(x => x.Emails)
            .HasForeignKey(x => x.ImportBatchId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Recipients)
            .WithOne(x => x.Email)
            .HasForeignKey(x => x.EmailId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Attachments)
            .WithOne(x => x.Email)
            .HasForeignKey(x => x.EmailId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.MailboxId);

        builder.HasIndex(x => x.ImportBatchId);

        builder.HasIndex(x => x.SenderEmail);

        builder.HasIndex(x => x.SentAt);

        builder.HasIndex(x => x.ReceivedAt);

        builder.HasIndex(x => x.Subject);

        builder.HasIndex(x => x.InternetMessageId);

        builder.HasIndex(x => x.MessageHash);

        builder.HasIndex(x => new { x.MailboxId, x.MessageHash })
            .IsUnique();

        builder.HasIndex(x => new { x.MailboxId, x.ReceivedAt });

        builder.HasIndex(x => new
            {
                x.Subject,
                x.BodyText,
                x.BodyHtml,
                x.SenderEmail,
                x.SenderName,
                x.InternetMessageId,
                x.FolderPath
            })
            .HasMethod("GIN")
            .IsTsVectorExpressionIndex("english");
    }
}