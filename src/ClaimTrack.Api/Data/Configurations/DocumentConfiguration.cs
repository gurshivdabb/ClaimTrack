using ClaimTrack.Api.Entities;
using ClaimTrack.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimTrack.Api.Data.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.FileName)
            .IsRequired()
            .HasMaxLength(FileNames.MaxLength);

        builder.Property(d => d.ContentType)
            .IsRequired()
            .HasMaxLength(50);

        // A 32-character GUID key; unique, so two rows can never point at the same file.
        builder.Property(d => d.StoragePath)
            .IsRequired()
            .HasMaxLength(64);
        builder.HasIndex(d => d.StoragePath)
            .IsUnique();

        // Restrict: neither a claim nor a user can be deleted while documents point to them.
        builder.HasOne(d => d.Claim)
            .WithMany()
            .HasForeignKey(d => d.ClaimId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.UploadedBy)
            .WithMany()
            .HasForeignKey(d => d.UploadedById)
            .OnDelete(DeleteBehavior.Restrict);

        // Documents are always listed per claim, in upload order.
        builder.HasIndex(d => new { d.ClaimId, d.UploadedAt });
    }
}
