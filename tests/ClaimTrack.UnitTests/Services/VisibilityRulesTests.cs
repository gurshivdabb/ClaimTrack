using AwesomeAssertions;
using ClaimTrack.Api.Entities;
using ClaimTrack.Api.Enums;
using ClaimTrack.Api.Services;

namespace ClaimTrack.UnitTests.Services;

// BR-09 rules run here on in-memory lists (LINQ to Objects). The integration tests
// run the same rules as SQL against a real database.
public class VisibilityRulesTests
{
    private static readonly Guid Alice = Guid.NewGuid();      // policyholder
    private static readonly Guid Bob = Guid.NewGuid();        // policyholder
    private static readonly Guid Adjuster1 = Guid.NewGuid();
    private static readonly Guid Adjuster2 = Guid.NewGuid();
    private static readonly Guid Supervisor = Guid.NewGuid();

    private readonly Policy _alicePolicy = new() { Id = Guid.NewGuid(), HolderId = Alice };
    private readonly Policy _bobPolicy = new() { Id = Guid.NewGuid(), HolderId = Bob };
    private readonly Claim _aliceClaimForAdjuster1;
    private readonly Claim _bobClaimUnassigned;

    public VisibilityRulesTests()
    {
        _aliceClaimForAdjuster1 = AddClaim(_alicePolicy, assignedAdjusterId: Adjuster1);
        _bobClaimUnassigned = AddClaim(_bobPolicy, assignedAdjusterId: null);
    }

    private static Claim AddClaim(Policy policy, Guid? assignedAdjusterId)
    {
        var claim = new Claim { Id = Guid.NewGuid(), Policy = policy, PolicyId = policy.Id, AssignedAdjusterId = assignedAdjusterId };
        policy.Claims.Add(claim);
        return claim;
    }

    private IQueryable<Policy> Policies => new[] { _alicePolicy, _bobPolicy }.AsQueryable();
    private IQueryable<Claim> Claims => new[] { _aliceClaimForAdjuster1, _bobClaimUnassigned }.AsQueryable();

    private sealed record FakeCurrentUser(Guid Id, Role Role) : ICurrentUser;

    [Fact]
    public void Policies_Supervisor_SeesAll()
    {
        Policies.VisibleTo(new FakeCurrentUser(Supervisor, Role.Supervisor))
            .Should().BeEquivalentTo([_alicePolicy, _bobPolicy]);
    }

    [Fact]
    public void Policies_Policyholder_SeesOnlyOwn()
    {
        Policies.VisibleTo(new FakeCurrentUser(Alice, Role.Policyholder))
            .Should().ContainSingle().Which.Should().BeSameAs(_alicePolicy);
    }

    [Fact]
    public void Policies_Adjuster_SeesOnlyPoliciesBehindAssignedClaims()
    {
        Policies.VisibleTo(new FakeCurrentUser(Adjuster1, Role.Adjuster))
            .Should().ContainSingle().Which.Should().BeSameAs(_alicePolicy);
    }

    [Fact]
    public void Policies_AdjusterWithNoAssignedClaims_SeesNone()
    {
        Policies.VisibleTo(new FakeCurrentUser(Adjuster2, Role.Adjuster)).Should().BeEmpty();
    }

    [Fact]
    public void Claims_Supervisor_SeesAll()
    {
        Claims.VisibleTo(new FakeCurrentUser(Supervisor, Role.Supervisor))
            .Should().BeEquivalentTo([_aliceClaimForAdjuster1, _bobClaimUnassigned]);
    }

    [Fact]
    public void Claims_Policyholder_SeesOnlyClaimsOnOwnPolicies()
    {
        Claims.VisibleTo(new FakeCurrentUser(Bob, Role.Policyholder))
            .Should().ContainSingle().Which.Should().BeSameAs(_bobClaimUnassigned);
    }

    [Fact]
    public void Claims_Adjuster_SeesOnlyAssignedClaims()
    {
        Claims.VisibleTo(new FakeCurrentUser(Adjuster1, Role.Adjuster))
            .Should().ContainSingle().Which.Should().BeSameAs(_aliceClaimForAdjuster1);
    }

    [Fact]
    public void Claims_OtherAdjuster_SeesNone()
    {
        Claims.VisibleTo(new FakeCurrentUser(Adjuster2, Role.Adjuster)).Should().BeEmpty();
    }

    [Fact]
    public void VisibleTo_UndefinedRole_Throws()
    {
        var user = new FakeCurrentUser(Alice, (Role)99);

        ((Action)(() => Claims.VisibleTo(user))).Should().Throw<InvalidOperationException>();
        ((Action)(() => Policies.VisibleTo(user))).Should().Throw<InvalidOperationException>();
    }
}
