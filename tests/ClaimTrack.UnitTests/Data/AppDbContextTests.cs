using AwesomeAssertions;
using ClaimTrack.Api.Data;
using ClaimTrack.Api.Entities;
using ClaimTrack.Api.Enums;
using Microsoft.EntityFrameworkCore;

namespace ClaimTrack.UnitTests.Data;

// BR-08: the append-only guard. This is the real AppDbContext, not a mock or the in-memory
// provider. It never connects: the guard throws before anything is sent to the database.
public class AppDbContextTests
{
    private static AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=never-connected;Database=none")
            .Options);

    private static AuditLogEntry TrackExistingEntry(AppDbContext db)
    {
        var entry = new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            ClaimId = Guid.NewGuid(),
            ActorUserId = Guid.NewGuid(),
            Action = AuditAction.Submitted,
            ToStatus = ClaimStatus.Submitted
        };
        db.AuditLog.Attach(entry); // tracked as an existing row, as if it had been loaded
        return entry;
    }

    [Fact]
    public void SaveChanges_ModifiedAuditEntry_Throws()
    {
        using var db = CreateContext();
        TrackExistingEntry(db).Details = "Rewriting history";

        var save = () => db.SaveChanges();

        save.Should().Throw<InvalidOperationException>().WithMessage("*append-only*");
    }

    [Fact]
    public void SaveChanges_DeletedAuditEntry_Throws()
    {
        using var db = CreateContext();
        db.AuditLog.Remove(TrackExistingEntry(db));

        var save = () => db.SaveChanges();

        save.Should().Throw<InvalidOperationException>().WithMessage("*append-only*");
    }

    [Fact]
    public async Task SaveChangesAsync_ModifiedAuditEntry_Throws()
    {
        await using var db = CreateContext();
        TrackExistingEntry(db).Details = "Rewriting history";

        var save = () => db.SaveChangesAsync();

        await save.Should().ThrowAsync<InvalidOperationException>().WithMessage("*append-only*");
    }

    [Fact]
    public async Task SaveChangesAsync_DeletedAuditEntry_Throws()
    {
        await using var db = CreateContext();
        db.AuditLog.Remove(TrackExistingEntry(db));

        var save = () => db.SaveChangesAsync();

        await save.Should().ThrowAsync<InvalidOperationException>().WithMessage("*append-only*");
    }
}
