using ClaimTrack.Api.Enums;

namespace ClaimTrack.Api.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;

    // BR-11: only the hash is stored, never the plain password.
    public string PasswordHash { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;
    public Role Role { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Policies this user holds (Policyholders only).
    public List<Policy> Policies { get; set; } = [];

    // Claims assigned to this user for review (Adjusters only).
    public List<Claim> AssignedClaims { get; set; } = [];
}
