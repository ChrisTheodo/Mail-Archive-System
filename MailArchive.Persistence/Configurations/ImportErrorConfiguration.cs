using MailArchive.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MailArchive.Persistence.Configurations;

public class ImportErrorConfiguration : IEntityTypeConfiguration<ImportError>
{
    public void Configure(EntityTypeBuilder<ImportError> builder)
    {
        builder.ToTable("import_errors");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Message)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasOne(x => x.ImportBatch)
            .WithMany(x => x.ImportErrors)
            .HasForeignKey(x => x.ImportBatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.ImportBatchId);

        builder.HasIndex(x => x.CreatedAt);
    }
}