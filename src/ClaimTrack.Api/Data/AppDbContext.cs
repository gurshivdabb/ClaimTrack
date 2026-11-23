using ClaimTrack.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClaimTrack.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Policy> Policies => Set<Policy>();
    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<AuditLogEntry> AuditLog => Set<AuditLogEntry>();
    public DbSet<Document> Documents => Set<Document>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Picks up every IEntityTypeConfiguration class in Data/Configurations.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // BR-10: the database hands out each number once, even under concurrent requests.
        modelBuilder.HasSequence<int>(Sequences.PolicyNumbers);
        modelBuilder.HasSequence<int>(Sequences.ClaimNumbers);
    }

    // Every save goes through one of these two methods (the async overloads without the
    // bool parameter call this one), so the BR-08 guard can't be skipped by tracked changes.
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureAuditLogIsAppendOnly();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnsureAuditLogIsAppendOnly();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    // BR-08: audit entries can be added, never changed or removed. This is a safety net
    // against our own bugs; there are no endpoints that update or delete them.
    private void EnsureAuditLogIsAppendOnly()
    {
        var rewritesHistory = ChangeTracker.Entries<AuditLogEntry>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        if (rewritesHistory)
        {
            throw new InvalidOperationException("Audit log entries are append-only and cannot be changed or deleted (BR-08).");
        }
    }
}
