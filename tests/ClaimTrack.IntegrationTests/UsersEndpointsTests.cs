using System.Net;
using AwesomeAssertions;
using ClaimTrack.Api.Dtos.Users;
using ClaimTrack.Api.Enums;
using ClaimTrack.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace ClaimTrack.IntegrationTests;

// GET /api/users?role=: supervisors pick an adjuster to assign or a policyholder for a new policy.
[Collection(ApiCollection.Name)]
public class UsersEndpointsTests(ClaimTrackApiFactory factory)
{
    private readonly HttpClient _supervisor = factory.CreateSupervisorClient();

    [Fact]
    public async Task List_AdjustersOnly_ReturnsAdjustersSortedByName()
    {
        // Names starting with "~" sort after every other test user, so these two are compared on their own.
        var zoe = await factory.CreateUserAsync(Role.Adjuster, fullName: "~ Zoe Adjuster");
        var amir = await factory.CreateUserAsync(Role.Adjuster, fullName: "~ Amir Adjuster");

        var response = await _supervisor.GetAsync("/api/users?role=Adjuster");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var users = await response.ReadAsync<List<UserSummaryResponse>>();
        users.Should().OnlyContain(u => u.Role == Role.Adjuster);
        users.Where(u => u.FullName.StartsWith('~')).Select(u => u.Id).Should().Equal(amir, zoe);
    }

    [Fact]
    public async Task List_NoRole_ReturnsEveryRole()
    {
        var users = await (await _supervisor.GetAsync("/api/users")).ReadAsync<List<UserSummaryResponse>>();

        users.Select(u => u.Role).Distinct().Should().BeEquivalentTo([Role.Policyholder, Role.Adjuster, Role.Supervisor]);
        users.Should().Contain(u => u.Id == ClaimTrackApiFactory.PolicyholderId);
    }

    [Fact]
    public async Task List_Response_HasNoEmailsOrPasswordData()
    {
        var body = await (await _supervisor.GetAsync("/api/users")).Content.ReadAsStringAsync();

        body.Should().NotContainEquivalentOf("email")
            .And.NotContainEquivalentOf("password")
            .And.NotContain("@test.local");
    }

    [Theory]
    [InlineData(Role.Policyholder)]
    [InlineData(Role.Adjuster)]
    public async Task List_NotASupervisor_Returns403(Role role)
    {
        var client = await factory.CreateClientForNewUserAsync(role);

        (await client.GetAsync("/api/users")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_NoToken_Returns401()
    {
        (await factory.CreateClient().GetAsync("/api/users")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_UnknownRole_Returns400NamingTheField()
    {
        var response = await _supervisor.GetAsync("/api/users?role=Manager");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadAsync<ValidationProblemDetails>()).Errors.Keys.Should().Contain("role");
    }
}
