namespace ClaimTrack.Api.Dtos.Auth;

// The client sends AccessToken back in the "Authorization: Bearer <token>" header.
public record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt);
