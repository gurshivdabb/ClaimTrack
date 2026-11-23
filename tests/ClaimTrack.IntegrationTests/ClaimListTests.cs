using System.Net;
using AwesomeAssertions;
using ClaimTrack.Api.Common;
using ClaimTrack.Api.Dtos.Claims;
using ClaimTrack.Api.Enums;
using ClaimTrack.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using static ClaimTrack.IntegrationTests.Infrastructure.ApiClientExtensions;

namespace ClaimTrack.IntegrationTests;

// GET /api/claims?status=&page=&pageSize=&sort= (requirements §6, Phase 3).
// Each test uses a brand-new policyholder, so totals are exact and other tests' claims never interfere.
[Collection(ApiCollection.Name)]
public class ClaimListTests(ClaimTrackApiFactory factory)
{
    private readonly HttpClient _supervisor = factory.CreateSupervisorClient();

    private sealed record Holder(HttpClient Client, List<ClaimResponse> Claims);

    // A new policyholder with one claim per amount, submitted in the order given.
    private async Task<Holder> HolderWithClaimsAsync(params decimal[] amounts)
    {
        var id = await factory.CreateUserAsync(Role.Policyholder);
        var client = factory.CreateClientAs(id, Role.Policyholder);
        var policy = await _supervisor.CreatePolicyAsync(ValidPolicyRequest() with { HolderId = id });

        var claims = new List<ClaimResponse>();
        foreach (var amount in amounts)
        {
            var response = await client.PostJsonAsync("/api/claims", ValidClaimRequest(policy.Id) with { ClaimedAmount = amount });
            claims.Add(await response.ReadAsync<ClaimResponse>());
        }
        return new Holder(client, claims);
    }

    private static async Task<PagedResult<ClaimResponse>> ListAsync(HttpClient client, string query = "")
    {
        var response = await client.GetAsync($"/api/claims{query}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.ReadAsync<PagedResult<ClaimResponse>>();
    }

    [Fact]
    public async Task List_FiveClaimsInPagesOfTwo_ReturnsTwoTwoOneThenEmpty()
    {
        var holder = await HolderWithClaimsAsync(100m, 200m, 300m, 400m, 500m);

        var page1 = await ListAsync(holder.Client, "?page=1&pageSize=2");
        var page2 = await ListAsync(holder.Client, "?page=2&pageSize=2");
        var page3 = await ListAsync(holder.Client, "?page=3&pageSize=2");
        var page4 = await ListAsync(holder.Client, "?page=4&pageSize=2");

        (page1.Page, page1.PageSize, page1.TotalCount, page1.TotalPages).Should().Be((1, 2, 5, 3));
        page1.Items.Should().HaveCount(2);
        page2.Items.Should().HaveCount(2);
        page3.Items.Should().HaveCount(1);
        page4.Items.Should().BeEmpty(); // past the end is an empty page, not 404

        // Every claim appears exactly once across the pages (the stable tie-breaker at work).
        page1.Items.Concat(page2.Items).Concat(page3.Items).Select(c => c.Id)
            .Should().BeEquivalentTo(holder.Claims.Select(c => c.Id));
    }

    [Fact]
    public async Task List_NoParameters_IsNewestFirst()
    {
        var holder = await HolderWithClaimsAsync(100m, 200m, 300m);

        var result = await ListAsync(holder.Client);

        result.Items.Select(c => c.Id).Should().Equal(holder.Claims.Select(c => c.Id).Reverse());
        (result.Page, result.PageSize).Should().Be((1, ClaimListQuery.DefaultPageSize));
    }

    [Fact]
    public async Task List_FilterByStatus_ReturnsOnlyThatStatusAndCountsOnlyThose()
    {
        var holder = await HolderWithClaimsAsync(100m, 200m, 300m);
        await holder.Client.WithdrawAsync(holder.Claims[1].Id);

        var result = await ListAsync(holder.Client, "?status=Withdrawn");

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle().Which.Id.Should().Be(holder.Claims[1].Id);
    }

    [Theory]
    [InlineData("claimedAmount", new[] { 100, 200, 300 })]
    [InlineData("-claimedAmount", new[] { 300, 200, 100 })]
    public async Task List_SortByAmount_OrdersAscendingOrDescending(string sort, int[] expectedAmounts)
    {
        var holder = await HolderWithClaimsAsync(200m, 300m, 100m);

        var result = await ListAsync(holder.Client, $"?sort={sort}");

        result.Items.Select(c => c.ClaimedAmount).Should().Equal(expectedAmounts.Select(a => (decimal)a));
    }

    [Fact]
    public async Task List_TotalCount_OnlyCountsClaimsTheUserCanSee()
    {
        var alice = await HolderWithClaimsAsync(100m, 200m);
        await HolderWithClaimsAsync(300m, 400m, 500m); // someone else's claims

        var result = await ListAsync(alice.Client);

        result.TotalCount.Should().Be(2); // BR-09: other people's claims don't leak through the count
    }

    [Theory]
    [InlineData("?page=0", "page")]
    [InlineData("?pageSize=0", "pageSize")]
    [InlineData("?pageSize=101", "pageSize")]
    [InlineData("?sort=description", "sort")]
    [InlineData("?status=Pending", "status")]
    [InlineData("?page=2147483647", "page")]             // (page - 1) × pageSize would overflow an int
    [InlineData("?page=99999999999", "page")]            // not even an int
    public async Task List_InvalidParameter_Returns400NamingIt(string query, string field)
    {
        var response = await _supervisor.GetAsync($"/api/claims{query}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadAsync<ValidationProblemDetails>()).Errors.Keys.Should().Contain(field);
    }
}
