namespace ClaimTrack.Api.Dtos.Auth;

public record LoginRequest(string Email, string Password)
{
    // Keeps the password out of the generated ToString(), so it can't be logged by accident.
    public override string ToString() => $"LoginRequest {{ Email = {Email} }}";
}
