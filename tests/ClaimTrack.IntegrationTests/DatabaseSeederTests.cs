using System.Net;
using AwesomeAssertions;
using ClaimTrack.Api.Common;
using ClaimTrack.Api.Data;
using ClaimTrack.Api.Data.Seed;
using ClaimTrack.Api.Dtos.Auth;
using ClaimTrack.Api.Dtos.Claims;
using ClaimTrack.Api.Entities;
using ClaimTrack.Api.Enums;
using ClaimTrack.Api.Services;
using ClaimTrack.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ClaimTrack.IntegrationTests;

// The seeder only runs automatically in Development, so these tests call it directly.
[Collection(ApiCollection.Name)]
public class DatabaseSeederTests(ClaimTrackApiFactory factory)
{
    private const string DemoPassword = "Seeded-Staff-Pass-1";

    private static readonly List<string> StaffEmails =
        DatabaseSeeder.StaffAccounts.Select(s => s.Email).ToList();

    private async Task SeedAsync(string staffPassword)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        var seeder = new DatabaseSeeder(
            services.GetRequiredService<AppDbContext>(),
            services.GetRequiredService<IPasswordHasher<User>>(),
            services.GetRequiredService<INumberGenerator>(),
            Options.Create(new SeedOptions { DemoPassword = staffPassword }),
            services.GetRequiredService<TimeProvider>(),
            NullLogger<DatabaseSeeder>.Instance);

        await seeder.SeedAsync();
    }

    private async Task<List<User>> GetStaffAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users.AsNoTracking().Where(u => StaffEmails.Contains(u.Email)).ToListAsync();
    }

    [Fact]
    public async Task SeedAsync_WithPassword_CreatesOneSupervisorAndTwoAdjusters()
    {
        await SeedAsync(DemoPassword);

        var staff = await GetStaffAsync();
        staff.Select(u => u.Role).Should().BeEquivalentTo([Role.Supervisor, Role.Adjuster, Role.Adjuster]);
    }

    [Fact]
    public async Task SeedAsync_RunTwice_DoesNotDuplicate()
    {
        await SeedAsync(DemoPassword);
        await SeedAsync(DemoPassword);

        (await GetStaffAsync()).Should().HaveCount(3);
    }

    [Fact]
    public async Task SeedAsync_SeededSupervisor_CanLogInAsSupervisor()
    {
        await SeedAsync(DemoPassword);
        var client = factory.CreateClient();

        var response = await client.PostJsonAsync(
            "/api/auth/login", new LoginRequest("supervisor@claimtrack.test", DemoPassword));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var login = await response.ReadAsync<LoginResponse>();
        new JsonWebTokenHandler().ReadJsonWebToken(login.AccessToken)
            .GetClaim("role").Value.Should().Be("Supervisor");
    }

    // ---- Demo policyholders, policies, and claims (requirements §9) ----

    private sealed record SeededData(List<User> Users, List<Policy> Policies, List<Claim> Claims, List<AuditLogEntry> AuditLog);

    private async Task<SeededData> GetSeededDataAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var holderEmails = DatabaseSeeder.PolicyholderAccounts.Select(a => a.Email).ToList();

        var users = await db.Users.AsNoTracking().ToListAsync();
        var holderIds = users.Where(u => holderEmails.Contains(u.Email)).Select(u => u.Id).ToList();
        var policies = await db.Policies.AsNoTracking().Where(p => holderIds.Contains(p.HolderId)).ToListAsync();
        var policyIds = policies.Select(p => p.Id).ToList();
        var claims = await db.Claims.AsNoTracking().Where(c => policyIds.Contains(c.PolicyId)).ToListAsync();
        var claimIds = claims.Select(c => c.Id).ToList();
        var auditLog = await db.AuditLog.AsNoTracking().Where(a => claimIds.Contains(a.ClaimId)).ToListAsync();

        return new SeededData(users, policies, claims, auditLog);
    }

    private async Task<HttpClient> LogInAsync(string email)
    {
        var client = factory.CreateClient();
        var response = await client.PostJsonAsync("/api/auth/login", new LoginRequest(email, DemoPassword));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var login = await response.ReadAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new("Bearer", login.AccessToken);
        return client;
    }

    [Fact]
    public async Task SeedAsync_DemoData_HasThreeHoldersFivePoliciesAndTenClaimsInEveryStatus()
    {
        await SeedAsync(DemoPassword);

        var seeded = await GetSeededDataAsync();
        var today = DateOnly.FromDateTime(ClaimTrackApiFactory.Now.UtcDateTime);

        seeded.Policies.Select(p => p.HolderId).Distinct().Should().HaveCount(3);
        seeded.Policies.Should().HaveCount(5);
        seeded.Policies.Select(p => p.CoverageType).Distinct()
            .Should().BeEquivalentTo([CoverageType.Auto, CoverageType.Home, CoverageType.Travel]);
        seeded.Policies.Where(p => p.EndDate < today).Should().ContainSingle("one policy is expired");
        seeded.Claims.Should().HaveCount(10);
        seeded.Claims.Select(c => c.Status).Distinct().Should().BeEquivalentTo(Enum.GetValues<ClaimStatus>());
    }

    [Fact]
    public async Task SeedAsync_RunTwice_DoesNotDuplicateDemoData()
    {
        await SeedAsync(DemoPassword);
        await SeedAsync(DemoPassword);

        var seeded = await GetSeededDataAsync();
        seeded.Policies.Should().HaveCount(5);
        seeded.Claims.Should().HaveCount(10);
    }

    // Replays every seeded audit trail against the API's own rules.
    [Fact]
    public async Task SeedAsync_EverySeededHistory_FollowsTheLifecycleRules()
    {
        await SeedAsync(DemoPassword);
        var seeded = await GetSeededDataAsync();
        var roleOf = seeded.Users.ToDictionary(u => u.Id, u => u.Role);

        foreach (var claim in seeded.Claims)
        {
            var policy = seeded.Policies.Single(p => p.Id == claim.PolicyId);
            var history = seeded.AuditLog.Where(a => a.ClaimId == claim.Id).OrderBy(a => a.Timestamp).ToList();

            history[0].Action.Should().Be(AuditAction.Submitted, claim.ClaimNumber);
            history[0].ActorUserId.Should().Be(policy.HolderId);
            history.Select(a => a.Timestamp).Should().BeInAscendingOrder()
                .And.OnlyContain(t => t <= ClaimTrackApiFactory.Now);

            ClaimStatus? status = null;
            foreach (var entry in history)
            {
                if (entry.ToStatus is not null)
                {
                    entry.FromStatus.Should().Be(status, $"{claim.ClaimNumber} {entry.Action}");
                    if (status is not null)
                    {
                        ClaimLifecycle.CanTransition(status.Value, entry.ToStatus.Value)
                            .Should().BeTrue($"{claim.ClaimNumber}: {status} → {entry.ToStatus} must be allowed (BR-05)");
                    }
                    status = entry.ToStatus;
                }

                var expectedRole = entry.Action switch
                {
                    AuditAction.Submitted or AuditAction.Updated or AuditAction.Withdrawn => Role.Policyholder,
                    AuditAction.ReviewStarted or AuditAction.Recommended => Role.Adjuster,
                    _ => Role.Supervisor // Assigned, Approved, Denied
                };
                roleOf[entry.ActorUserId].Should().Be(expectedRole, $"{claim.ClaimNumber} {entry.Action}");
            }

            status.Should().Be(claim.Status, $"{claim.ClaimNumber}'s history must end in its current status");
        }
    }

    [Fact]
    public async Task SeedAsync_EverySeededClaim_FitsItsPolicy()
    {
        await SeedAsync(DemoPassword);
        var seeded = await GetSeededDataAsync();

        foreach (var claim in seeded.Claims)
        {
            var policy = seeded.Policies.Single(p => p.Id == claim.PolicyId);
            var details = new UpdateClaimRequest(claim.IncidentDate, claim.Description, claim.ClaimedAmount);

            ClaimPolicyRules.Check(details, policy).Should().BeNull(claim.ClaimNumber); // BR-01, BR-02
            claim.IncidentDate.Should().BeOnOrBefore(DateOnly.FromDateTime(claim.SubmittedAt.UtcDateTime));
        }
    }

    [Theory]
    [InlineData("alex.martin@claimtrack.test", 4)]
    [InlineData("priya.singh@claimtrack.test", 5)]
    [InlineData("tom.wilson@claimtrack.test", 1)]
    [InlineData("adjuster1@claimtrack.test", 3)]  // claim 9 was reassigned to adjuster 2
    [InlineData("adjuster2@claimtrack.test", 4)]
    public async Task SeedAsync_DemoUser_SeesOnlyTheirOwnOrAssignedClaims(string email, int expectedClaims)
    {
        await SeedAsync(DemoPassword);
        var client = await LogInAsync(email);

        var claims = (await (await client.GetAsync("/api/claims")).ReadAsync<PagedResult<ClaimResponse>>()).Items;

        claims.Should().HaveCount(expectedClaims); // BR-09
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234567")] // BR-11: shorter than 8 characters
    public async Task SeedAsync_MissingOrShortPassword_CreatesNothingAndDoesNotThrow(string staffPassword)
    {
        var before = await GetStaffAsync();

        await SeedAsync(staffPassword);

        // Other tests in this run may already have seeded staff, so compare before and after.
        (await GetStaffAsync()).Should().HaveCount(before.Count);
    }
}
