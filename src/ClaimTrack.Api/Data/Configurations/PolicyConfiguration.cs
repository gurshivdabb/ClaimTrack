using ClaimTrack.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimTrack.Api.Data.Configurations;

public class PolicyConfiguration : IEntityTypeConfiguration<Policy>
{
    public void Configure(EntityTypeBuilder<Policy> builder)
    {
        builder.HasKey(p => p.Id);

        // BR-10: policy numbers must be unique.
        builder.Property(p => p.PolicyNumber)
            .IsRequired()
            .HasMaxLength(20);
        builder.HasIndex(p => p.PolicyNumber)
            .IsUnique();

        builder.Property(p => p.CoverageType)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(p => p.CoverageLimit)
            .HasPrecision(18, 2);
        builder.Property(p => p.Deductible)
            .HasPrecision(18, 2);

        // Restrict: the database refuses to delete a policy that still has claims.
        builder.HasMany(p => p.Claims)
            .WithOne(c => c.Policy)
            .HasForeignKey(c => c.PolicyId)
            .OnDelete(DeleteBehavior.Restrict);

        // The Holder relationship is configured in UserConfiguration.
    }
}
