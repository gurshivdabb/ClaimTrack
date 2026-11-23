using System.Net;
using AwesomeAssertions;
using ClaimTrack.Api.Dtos.Claims;
using ClaimTrack.Api.Enums;
using ClaimTrack.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace ClaimTrack.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AssignClaimTests(ClaimTrackApiFactory factory)
{
    private readonly HttpClient _supervisor = factory.CreateSupervisorClient();

    private Task<ClaimResponse> SubmitClaimAsync() => factory.SubmitClaimAsync();

    private Task<HttpResponseMessage> AssignAsync(Guid claimId, Guid adjusterId) =>
        _supervisor.AssignAsync(claimId, adjusterId);

    [Fact]
    public async Task Assign_ToAdjuster_Returns200AndAdjusterCanSeeTheClaim()
    {
        var claim = await SubmitClaimAsync();
        var adjusterId = await factory.CreateUserAsync(Role.Adjuster);

        var response = await AssignAsync(claim.Id, adjusterId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var assigned = await response.ReadAsync<ClaimResponse>();
        assigned.AssignedAdjusterId.Should().Be(adjusterId);
        assigned.Status.Should().Be(ClaimStatus.Submitted); // §4: assignment doesn't change status

        var adjuster = factory.CreateClientAs(adjusterId, Role.Adjuster);
        (await adjuster.GetAsync($"/api/claims/{claim.Id}")).StatusCode.Should().Be(HttpStatusCode.OK); // BR-09
    }

    [Fact]
    public async Task Assign_ToAnotherAdjuster_ReplacesTheFirst()
    {
        var claim = await SubmitClaimAsync();
        var firstId = await factory.CreateUserAsync(Role.Adjuster);
        var secondId = await factory.CreateUserAsync(Role.Adjuster);
        await AssignAsync(claim.Id, firstId);

        var response = await AssignAsync(claim.Id, secondId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var first = factory.CreateClientAs(firstId, Role.Adjuster);
        var second = factory.CreateClientAs(secondId, Role.Adjuster);
        (await first.GetAsync($"/api/claims/{claim.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await second.GetAsync($"/api/claims/{claim.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Assign_ClaimUnderReview_Returns200()
    {
        var claim = await SubmitClaimAsync();
        await factory.SetClaimStatusAsync(claim.Id, ClaimStatus.UnderReview);

        var response = await AssignAsync(claim.Id, await factory.CreateUserAsync(Role.Adjuster));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.ReadAsync<ClaimResponse>()).Status.Should().Be(ClaimStatus.UnderReview);
    }

    [Theory]
    [InlineData(ClaimStatus.Approved)]
    [InlineData(ClaimStatus.Denied)]
    [InlineData(ClaimStatus.Withdrawn)]
    public async Task Assign_FinalClaim_Returns409(ClaimStatus status)
    {
        var claim = await SubmitClaimAsync();
        await factory.SetClaimStatusAsync(claim.Id, status);

        var response = await AssignAsync(claim.Id, await factory.CreateUserAsync(Role.Adjuster));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.ReadAsync<ProblemDetails>()).Detail.Should().Be(
            $"Claims can only be assigned while Submitted or UnderReview. This claim is {status}.");
    }

    [Theory]
    [InlineData(Role.Policyholder)]
    [InlineData(Role.Supervisor)] // separation of duties: supervisors can't assign claims to themselves
    public async Task Assign_ToUserWhoIsNotAnAdjuster_Returns422(Role role)
    {
        var claim = await SubmitClaimAsync();

        var response = await AssignAsync(claim.Id, await factory.CreateUserAsync(role));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity); // BR-04
        (await response.ReadAsync<ProblemDetails>()).Detail.Should().Be("Assignee must be an existing adjuster.");
    }

    [Fact]
    public async Task Assign_UnknownAdjuster_Returns422()
    {
        var claim = await SubmitClaimAsync();

        var response = await AssignAsync(claim.Id, Guid.NewGuid());

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Assign_UnknownClaim_Returns404()
    {
        var response = await AssignAsync(Guid.NewGuid(), await factory.CreateUserAsync(Role.Adjuster));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Assign_EmptyAdjusterId_Returns400()
    {
        var claim = await SubmitClaimAsync();

        var response = await AssignAsync(claim.Id, Guid.Empty);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadAsync<ValidationProblemDetails>()).Errors.Keys.Should().BeEquivalentTo("adjusterId");
    }
}
