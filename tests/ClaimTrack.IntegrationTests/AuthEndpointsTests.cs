using System.Net;
using AwesomeAssertions;
using ClaimTrack.Api.Data;
using ClaimTrack.Api.Dtos.Auth;
using ClaimTrack.Api.Entities;
using ClaimTrack.Api.Enums;
using ClaimTrack.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ClaimTrack.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AuthEndpointsTests(ClaimTrackApiFactory factory)
{
    private const string Password = "Correct-Horse-1";

    private readonly HttpClient _client = factory.CreateClient();

    // Every test uses its own email, so tests never clash on the unique index.
    private static RegisterRequest ValidRequest() =>
        new($"{Guid.NewGuid():N}@example.com", Password, "Jane Doe");

    [Fact]
    public async Task Register_ValidRequest_Returns201AsPolicyholder()
    {
        var request = ValidRequest() with { Email = $"  Jane.{Guid.NewGuid():N}@Example.COM " };

        var response = await _client.PostJsonAsync("/api/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var user = await response.ReadAsync<UserResponse>();
        user.Role.Should().Be(Role.Policyholder);
        user.Email.Should().Be(request.Email.Trim().ToLowerInvariant());
        user.CreatedAt.Should().Be(ClaimTrackApiFactory.Now);

        // BR-11: neither the password nor its hash is ever sent back.
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContainEquivalentOf("password").And.NotContain(Password);
    }

    [Fact]
    public async Task Register_ValidRequest_StoresOnlyASaltedHash()
    {
        var first = await RegisterAsync(ValidRequest());
        var second = await RegisterAsync(ValidRequest()); // same password, different user

        var firstUser = await FindUserAsync(first.Id);
        var secondUser = await FindUserAsync(second.Id);

        // BR-11: the plain password isn't stored, and the hash checks out.
        firstUser.PasswordHash.Should().NotContain(Password);
        new PasswordHasher<User>().VerifyHashedPassword(firstUser, firstUser.PasswordHash, Password)
            .Should().Be(PasswordVerificationResult.Success);

        // Each hash has its own random salt, so the same password gives different hashes.
        firstUser.PasswordHash.Should().NotBe(secondUser.PasswordHash);
    }

    [Fact]
    public async Task Register_EmailAlreadyUsedWithDifferentCase_Returns409()
    {
        var request = ValidRequest();
        await RegisterAsync(request);

        var response = await _client.PostJsonAsync(
            "/api/auth/register", request with { Email = request.Email.ToUpperInvariant() });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict); // BR-11
        var problem = await response.ReadAsync<ProblemDetails>();
        problem.Detail.Should().Be("An account with this email already exists.");
    }

    [Fact]
    public async Task Register_InvalidInput_Returns400WithCamelCaseFieldNames()
    {
        var request = new RegisterRequest("not-an-email", "short", "");

        var response = await _client.PostJsonAsync("/api/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.ReadAsync<ValidationProblemDetails>();
        problem.Errors.Keys.Should().BeEquivalentTo("email", "password", "fullName");
    }

    [Fact]
    public async Task Register_WithRoleInBody_StillCreatesPolicyholder()
    {
        // A "mass assignment" attempt: the client adds a field it shouldn't control.
        var request = new
        {
            email = $"{Guid.NewGuid():N}@example.com",
            password = Password,
            fullName = "Mallory",
            role = "Supervisor"
        };

        var response = await _client.PostJsonAsync("/api/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.ReadAsync<UserResponse>()).Role.Should().Be(Role.Policyholder);
    }

    [Fact]
    public async Task Login_CorrectCredentials_Returns200WithTokenForThatUser()
    {
        var user = await RegisterAsync(ValidRequest());

        var response = await _client.PostJsonAsync("/api/auth/login", new LoginRequest(user.Email, Password));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var login = await response.ReadAsync<LoginResponse>();
        login.ExpiresAt.Should().Be(ClaimTrackApiFactory.Now.AddMinutes(60)); // requirements §7

        var token = new JsonWebTokenHandler().ReadJsonWebToken(login.AccessToken);
        token.GetClaim("sub").Value.Should().Be(user.Id.ToString());
        token.GetClaim("role").Value.Should().Be("Policyholder");
    }

    [Fact]
    public async Task Login_EmailWithDifferentCaseAndSpaces_Returns200()
    {
        var user = await RegisterAsync(ValidRequest());

        var response = await _client.PostJsonAsync(
            "/api/auth/login", new LoginRequest($"  {user.Email.ToUpperInvariant()} ", Password));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var user = await RegisterAsync(ValidRequest());

        var response = await _client.PostJsonAsync("/api/auth/login", new LoginRequest(user.Email, "Wrong-Password-1"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await response.ReadAsync<ProblemDetails>();
        problem.Detail.Should().Be("Invalid email or password.");
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401WithTheSameMessage()
    {
        var response = await _client.PostJsonAsync(
            "/api/auth/login", new LoginRequest($"{Guid.NewGuid():N}@example.com", Password));

        // Same status and message as a wrong password, so attackers can't tell which emails exist.
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await response.ReadAsync<ProblemDetails>();
        problem.Detail.Should().Be("Invalid email or password.");
    }

    [Fact]
    public async Task Login_EmptyFields_Returns400()
    {
        var response = await _client.PostJsonAsync("/api/auth/login", new LoginRequest("", ""));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.ReadAsync<ValidationProblemDetails>();
        problem.Errors.Keys.Should().BeEquivalentTo("email", "password");
    }

    // When ASP.NET's hashing settings get stronger, old hashes still verify, and login quietly
    // replaces them with a hash in the current format while it has the plain password.
    [Fact]
    public async Task Login_UserWithOldFormatHash_UpgradesTheStoredHash()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        var userId = await factory.CreateUserAsync(Role.Policyholder, email);
        var oldHasher = new PasswordHasher<User>(Options.Create(new PasswordHasherOptions
        {
            CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV2
        }));
        await SetPasswordHashAsync(userId, oldHasher.HashPassword(new User(), Password));

        var response = await _client.PostJsonAsync("/api/auth/login", new LoginRequest(email, Password));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await FindUserAsync(userId);
        new PasswordHasher<User>().VerifyHashedPassword(user, user.PasswordHash, Password)
            .Should().Be(PasswordVerificationResult.Success, "the hash is now in the current format");
    }

    [Fact]
    public void Startup_WithoutSigningKey_FailsImmediately()
    {
        // Added after the factory's settings, so this empty key wins.
        using var api = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:SigningKey"] = "" })));

        var start = () => api.CreateClient();

        start.Should().Throw<OptionsValidationException>()
            .WithMessage("*Jwt:SigningKey*");
    }

    private async Task<UserResponse> RegisterAsync(RegisterRequest request)
    {
        var response = await _client.PostJsonAsync("/api/auth/register", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await response.ReadAsync<UserResponse>();
    }

    private async Task SetPasswordHashAsync(Guid userId, string passwordHash)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.PasswordHash, passwordHash));
    }

    private async Task<User> FindUserAsync(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users.AsNoTracking().SingleAsync(u => u.Id == id);
    }
}
