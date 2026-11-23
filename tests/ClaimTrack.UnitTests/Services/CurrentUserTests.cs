using System.Security.Claims;
using AwesomeAssertions;
using ClaimTrack.Api.Enums;
using ClaimTrack.Api.Services;
using Microsoft.AspNetCore.Http;

namespace ClaimTrack.UnitTests.Services;

public class CurrentUserTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    // Builds what the JWT middleware produces for a logged-in user: a ClaimsPrincipal
    // whose identity has an authentication type (that's what makes IsAuthenticated true).
    private static CurrentUser CreateCurrentUser(params Claim[] claims) =>
        CreateCurrentUser(new ClaimsIdentity(claims, authenticationType: "Bearer"));

    private static CurrentUser CreateCurrentUser(ClaimsIdentity identity)
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return new CurrentUser(accessor);
    }

    [Fact]
    public void IdAndRole_ValidClaims_AreReadFromTheToken()
    {
        var currentUser = CreateCurrentUser(new Claim("sub", UserId.ToString()), new Claim("role", "Adjuster"));

        currentUser.Id.Should().Be(UserId);
        currentUser.Role.Should().Be(Role.Adjuster);
    }

    [Fact]
    public void Id_NobodyLoggedIn_Throws()
    {
        // An identity without an authentication type is what ASP.NET Core uses for anonymous requests.
        var currentUser = CreateCurrentUser(new ClaimsIdentity());

        var read = () => currentUser.Id;

        read.Should().Throw<InvalidOperationException>().WithMessage("*missing [Authorize]*");
    }

    [Fact]
    public void Id_NoHttpContext_Throws()
    {
        var currentUser = new CurrentUser(new HttpContextAccessor());

        var read = () => currentUser.Id;

        read.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Id_MissingSubClaim_Throws()
    {
        var currentUser = CreateCurrentUser(new Claim("role", "Adjuster"));

        var read = () => currentUser.Id;

        read.Should().Throw<InvalidOperationException>().WithMessage("*'sub'*");
    }

    [Fact]
    public void Id_SubClaimNotAGuid_Throws()
    {
        var currentUser = CreateCurrentUser(new Claim("sub", "12345"), new Claim("role", "Adjuster"));

        var read = () => currentUser.Id;

        read.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("Administrator")]
    [InlineData("7")] // Enum.TryParse accepts numbers, so this checks the IsDefined guard
    public void Role_UnknownRole_Throws(string role)
    {
        var currentUser = CreateCurrentUser(new Claim("sub", UserId.ToString()), new Claim("role", role));

        var read = () => currentUser.Role;

        read.Should().Throw<InvalidOperationException>();
    }
}
