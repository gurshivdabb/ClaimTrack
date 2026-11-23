using ClaimTrack.Api.Enums;

namespace ClaimTrack.Api.Services;

// The logged-in user making the current request. Services use this instead of
// reading HTTP or the token themselves, so they stay free of web concerns.
// Only use it behind [Authorize]: without a logged-in user, both properties throw.
public interface ICurrentUser
{
    Guid Id { get; }
    Role Role { get; }
}
