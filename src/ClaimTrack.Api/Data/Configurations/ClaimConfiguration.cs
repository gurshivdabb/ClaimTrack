using ClaimTrack.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimTrack.Api.Data.Configurations;

public class ClaimConfiguration : IEntityTypeConfiguration<Claim>
{
    public void Configure(EntityTypeBuilder<Claim> builder)
    {
        builder.HasKey(c => c.Id);

        // BR-10: claim numbers must be unique.
        builder.Property(c => c.ClaimNumber)
            .IsRequired()
            .HasMaxLength(20);
        builder.HasIndex(c => c.ClaimNumber)
            .IsUnique();

        builder.Property(c => c.Description)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(c => c.ClaimedAmount)
            .HasPrecision(18, 2);

        builder.Property(c => c.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(c => c.Recommendation)
            .HasConversion<string>()
            .HasMaxLength(10);

        // The Policy relationship is configured in PolicyConfiguration,
        // and the AssignedAdjuster relationship in UserConfiguration.
    }
}
