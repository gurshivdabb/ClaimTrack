namespace ClaimTrack.Api.Data.Seed;

// Settings from the "Seed" configuration section. DemoPassword is a secret,
// so it comes from user-secrets locally, never from appsettings.json.
public class SeedOptions
{
    public const string SectionName = "Seed";

    // The password for every seeded demo account, staff and policyholders.
    public string DemoPassword { get; set; } = string.Empty;
}
