using ClaimTrack.Api.Enums;

namespace ClaimTrack.Api.Dtos.Auth;

// Never includes the password or its hash.
public record UserResponse(
    Guid Id,
    string Email,
    string FullName,
    Role Role,
    DateTimeOffset CreatedAt);
