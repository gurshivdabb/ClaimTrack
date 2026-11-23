using ClaimTrack.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimTrack.Api.Data.Configurations;

public class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Action)
            .HasConversion<string>()
            .HasMaxLength(20);
        builder.Property(a => a.FromStatus)
            .HasConversion<string>()
            .HasMaxLength(20);
        builder.Property(a => a.ToStatus)
            .HasConversion<string>()
            .HasMaxLength(20);

        // Room for the longest detail: a denial reason (up to 2000 characters).
        builder.Property(a => a.Details)
            .HasMaxLength(2000);

        // Restrict: neither a claim nor a user can be deleted while their history exists.
        builder.HasOne(a => a.Claim)
            .WithMany()
            .HasForeignKey(a => a.ClaimId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Actor)
            .WithMany()
            .HasForeignKey(a => a.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // A claim's history is always read in time order, so index exactly that.
        builder.HasIndex(a => new { a.ClaimId, a.Timestamp });
    }
}
