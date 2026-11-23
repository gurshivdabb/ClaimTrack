using AwesomeAssertions;
using ClaimTrack.Api.Common;
using ClaimTrack.Api.Entities;
using ClaimTrack.Api.Enums;
using ClaimTrack.Api.Services;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using static ClaimTrack.UnitTests.TestClock;

namespace ClaimTrack.UnitTests.Services;

public class TokenServiceTests
{
    private static readonly JwtOptions Jwt = new()
    {
        Issuer = "ClaimTrack",
        Audience = "ClaimTrack",
        SigningKey = "unit-test-signing-key-at-least-32-bytes-long"
    };

    private readonly FakeTimeProvider _time = CreateTimeProvider();

    private static readonly User User = new()
    {
        Id = Guid.NewGuid(),
        Email = "adjuster@example.com",
        Role = Role.Adjuster
    };

    private string CreateToken() => new TokenService(Options.Create(Jwt), _time).CreateToken(User).AccessToken;

    // Checks the token with the same rules Program.cs gives the JWT middleware.
    private Task<Microsoft.IdentityModel.Tokens.TokenValidationResult> ValidateAsync(string token, JwtOptions jwt) =>
        new JsonWebTokenHandler().ValidateTokenAsync(token, jwt.CreateValidationParameters(_time));

    [Fact]
    public void CreateToken_User_ContainsIdEmailAndRole()
    {
        var token = new JsonWebTokenHandler().ReadJsonWebToken(CreateToken());

        token.GetClaim("sub").Value.Should().Be(User.Id.ToString());
        token.GetClaim("email").Value.Should().Be("adjuster@example.com");
        token.GetClaim("role").Value.Should().Be("Adjuster");
        token.Issuer.Should().Be("ClaimTrack");
    }

    [Fact]
    public void CreateToken_User_ExpiresSixtyMinutesAfterIssue()
    {
        var response = new TokenService(Options.Create(Jwt), _time).CreateToken(User);

        var token = new JsonWebTokenHandler().ReadJsonWebToken(response.AccessToken);
        response.ExpiresAt.Should().Be(Now.AddMinutes(60));
        token.ValidTo.Should().Be(Now.AddMinutes(60).UtcDateTime);
    }

    [Fact]
    public async Task Validate_FreshToken_IsValid()
    {
        var result = await ValidateAsync(CreateToken(), Jwt);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_TokenSignedWithAnotherKey_IsRejected()
    {
        var otherKey = new JwtOptions
        {
            Issuer = Jwt.Issuer,
            Audience = Jwt.Audience,
            SigningKey = "a-completely-different-key-also-32-bytes-long"
        };

        var result = await ValidateAsync(CreateToken(), otherKey);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_OneSecondBeforeExpiry_IsValid()
    {
        var token = CreateToken();
        _time.Advance(TimeSpan.FromMinutes(60) - TimeSpan.FromSeconds(1));

        var result = await ValidateAsync(token, Jwt);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_AtExpiry_IsRejectedWithNoGracePeriod()
    {
        var token = CreateToken();
        _time.Advance(TimeSpan.FromMinutes(60));

        var result = await ValidateAsync(token, Jwt);

        result.IsValid.Should().BeFalse(); // requirements §7, ClockSkew = 0
    }

    [Fact]
    public async Task Validate_TokenNotValidYet_IsRejected()
    {
        // A token issued one minute in the future, as if it came from a server with a fast clock.
        var futureClock = new FakeTimeProvider(Now.AddMinutes(1));
        var token = new TokenService(Options.Create(Jwt), futureClock).CreateToken(User).AccessToken;

        var result = await ValidateAsync(token, Jwt);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_WrongAudience_IsRejected()
    {
        var otherAudience = new JwtOptions { Issuer = Jwt.Issuer, Audience = "SomeOtherApi", SigningKey = Jwt.SigningKey };

        var result = await ValidateAsync(CreateToken(), otherAudience);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("", "ClaimTrack", "unit-test-signing-key-at-least-32-bytes-long")]
    [InlineData("ClaimTrack", "", "unit-test-signing-key-at-least-32-bytes-long")]
    [InlineData("ClaimTrack", "ClaimTrack", "only-31-bytes-long-signing-key!")]
    public void IsValid_MissingSettingOrShortKey_IsFalse(string issuer, string audience, string key)
    {
        var options = new JwtOptions { Issuer = issuer, Audience = audience, SigningKey = key };

        options.IsValid.Should().BeFalse();
    }

    [Fact]
    public void IsValid_KeyOfExactly32Bytes_IsTrue()
    {
        var options = new JwtOptions { Issuer = "ClaimTrack", Audience = "ClaimTrack", SigningKey = new string('k', 32) };

        options.IsValid.Should().BeTrue();
    }
}
