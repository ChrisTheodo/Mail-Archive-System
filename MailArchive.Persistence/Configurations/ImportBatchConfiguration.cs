using MailArchive.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MailArchive.Persistence.Configurations;

public class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
{
    public void Configure(EntityTypeBuilder<ImportBatch> builder)
    {
        builder.ToTable("import_batches");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PstFilename)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.PstHash)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(x => x.PstStoragePath)
            .HasMaxLength(1000);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(x => x.StartedAt)
            .IsRequired();

        builder.Property(x => x.TotalMessages)
            .IsRequired();

        builder.Property(x => x.ImportedMessages)
            .IsRequired();

        builder.Property(x => x.FailedMessages)
            .IsRequired();

        builder.HasOne(x => x.Mailbox)
            .WithMany()
            .HasForeignKey(x => x.MailboxId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Emails)
            .WithOne(x => x.ImportBatch)
            .HasForeignKey(x => x.ImportBatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.ImportErrors)
            .WithOne(x => x.ImportBatch)
            .HasForeignKey(x => x.ImportBatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.MailboxId);

        builder.HasIndex(x => x.Status);

        builder.HasIndex(x => x.StartedAt);

        builder.HasIndex(x => x.CompletedAt);

        builder.HasIndex(x => x.PstHash);

        builder.HasIndex(x => new { x.MailboxId, x.PstHash })
            .IsUnique();
    }
}