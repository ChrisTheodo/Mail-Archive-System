using MailArchive.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MailArchive.Persistence.Configurations;

public class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
{
    public void Configure(EntityTypeBuilder<ImportBatch> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PstFilename)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.PstHash)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.StartedAt)
            .IsRequired();

        builder.Property(x => x.TotalMessages)
            .IsRequired();

        builder.Property(x => x.ImportedMessages)
            .IsRequired();

        builder.Property(x => x.FailedMessages)
            .IsRequired();

        builder.HasIndex(x => x.MailboxId);

        builder.HasIndex(x => x.PstHash);

        builder.HasIndex(x => x.Status);

        builder.HasIndex(x => x.StartedAt);

        builder.HasIndex(x => new { x.MailboxId, x.PstHash });

        builder
            .HasMany(x => x.Emails)
            .WithOne(x => x.ImportBatch)
            .HasForeignKey(x => x.ImportBatchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}