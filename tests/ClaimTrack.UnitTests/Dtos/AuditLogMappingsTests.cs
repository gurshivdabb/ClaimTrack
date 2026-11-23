using AwesomeAssertions;
using ClaimTrack.Api.Dtos.Claims;
using ClaimTrack.Api.Entities;
using ClaimTrack.Api.Enums;

namespace ClaimTrack.UnitTests.Dtos;

public class AuditLogMappingsTests
{
    [Fact]
    public void ToResponse_Entry_CopiesFieldsAndActorNameAndRole()
    {
        var actor = new User { Id = Guid.NewGuid(), FullName = "Sarah Chen", Role = Role.Supervisor, Email = "s@x.test" };
        var entry = new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            ClaimId = Guid.NewGuid(),
            ActorUserId = actor.Id,
            Actor = actor,
            Action = AuditAction.Denied,
            FromStatus = ClaimStatus.UnderReview,
            ToStatus = ClaimStatus.Denied,
            Details = "Damage predates the policy.",
            Timestamp = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero)
        };

        // EF translates the expression into SQL; compiling it runs the same mapping in memory.
        var response = AuditLogMappings.ToResponse.Compile()(entry);

        response.Should().Be(new AuditLogEntryResponse(
            entry.Id, AuditAction.Denied, ClaimStatus.UnderReview, ClaimStatus.Denied,
            "Damage predates the policy.", entry.Timestamp, actor.Id, "Sarah Chen", Role.Supervisor));
    }
}
