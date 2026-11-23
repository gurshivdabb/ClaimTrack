using ClaimTrack.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimTrack.Api.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        // BR-11: emails are unique. SQL Server's default collation compares text
        // case-insensitively, so "Jane@x.com" and "jane@x.com" count as the same email.
        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256);
        builder.HasIndex(u => u.Email)
            .IsUnique();

        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(u => u.FullName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(u => u.Role)
            .HasConversion<string>()
            .HasMaxLength(20);

        // Restrict on both: users are never deleted while policies or claims point to them.
        builder.HasMany(u => u.Policies)
            .WithOne(p => p.Holder)
            .HasForeignKey(p => p.HolderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(u => u.AssignedClaims)
            .WithOne(c => c.AssignedAdjuster)
            .HasForeignKey(c => c.AssignedAdjusterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
