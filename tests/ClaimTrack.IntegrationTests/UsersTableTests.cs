using AwesomeAssertions;
using ClaimTrack.Api.Enums;
using ClaimTrack.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ClaimTrack.IntegrationTests;

// Checks rules the database itself enforces on the Users table.
// There are no user endpoints yet, so these talk to the database directly.
[Collection(ApiCollection.Name)]
public class UsersTableTests(ClaimTrackApiFactory factory)
{
    [Fact]
    public async Task CreateUser_EmailDifferingOnlyInCase_IsRejected()
    {
        var email = $"{Guid.NewGuid()}@test.local";
        await factory.CreateUserAsync(Role.Policyholder, email);

        var duplicate = () => factory.CreateUserAsync(Role.Policyholder, email.ToUpperInvariant());

        await duplicate.Should().ThrowAsync<DbUpdateException>(); // BR-11
    }
}
