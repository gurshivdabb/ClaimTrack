using ClaimTrack.Api.Common;
using ClaimTrack.Api.Dtos.Auth;
using ClaimTrack.Api.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ClaimTrack.Api.Services;

public class TokenService(IOptions<JwtOptions> options, TimeProvider timeProvider) : ITokenService
{
    // Requirements §7
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(60);

    public LoginResponse CreateToken(User user)
    {
        var jwt = options.Value;
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.Add(Lifetime);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Claims = new Dictionary<string, object>
            {
                [JwtOptions.UserIdClaim] = user.Id.ToString(),
                [JwtOptions.EmailClaim] = user.Email,
                [JwtOptions.RoleClaim] = user.Role.ToString()
            },
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(jwt.CreateSigningKey(), SecurityAlgorithms.HmacSha256)
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);

        return new LoginResponse(token, expiresAt);
    }
}
