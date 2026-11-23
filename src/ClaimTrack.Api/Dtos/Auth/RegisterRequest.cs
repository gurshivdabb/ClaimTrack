namespace ClaimTrack.Api.Dtos.Auth;

// No Role property on purpose: public registration always creates a Policyholder,
// so a client can't make itself a Supervisor by adding "role" to the JSON.
public record RegisterRequest(string Email, string Password, string FullName)
{
    // A record's generated ToString() prints every property. This one leaves the password out,
    // so it can never end up in a log by accident.
    public override string ToString() => $"RegisterRequest {{ Email = {Email}, FullName = {FullName} }}";
}
