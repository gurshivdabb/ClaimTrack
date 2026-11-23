using AwesomeAssertions;
using ClaimTrack.Api.Dtos.Auth;
using ClaimTrack.Api.Entities;
using ClaimTrack.Api.Enums;

namespace ClaimTrack.UnitTests.Dtos;

public class UserMappingsTests
{
    [Fact]
    public void ToResponse_User_CopiesEveryResponseField()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "jane@example.com",
            PasswordHash = "hash",
            FullName = "Jane Doe",
            Role = Role.Adjuster,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero)
        };

        var response = user.ToResponse();

        // Entity-only members (PasswordHash, the navigation lists) are ignored.
        response.Should().BeEquivalentTo(user, options => options.ExcludingMissingMembers());
    }

    [Fact]
    public void UserResponse_HasNoPasswordProperty()
    {
        var propertyNames = typeof(UserResponse).GetProperties().Select(p => p.Name);

        // BR-11: a password or hash must never be returned to a client.
        propertyNames.Should().NotContain(name => name.Contains("Password"));
    }

    [Fact]
    public void RegisterRequestToString_LeavesOutThePassword()
    {
        var request = new RegisterRequest("jane@example.com", "SuperSecret1!", "Jane Doe");

        var text = request.ToString();

        text.Should().Contain("jane@example.com").And.NotContain("SuperSecret1!");
    }
}
