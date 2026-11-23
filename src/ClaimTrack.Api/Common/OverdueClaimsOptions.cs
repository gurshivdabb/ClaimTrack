namespace ClaimTrack.Api.Common;

// Settings from the "OverdueClaims" section of appsettings.json.
public class OverdueClaimsOptions
{
    public const string SectionName = "OverdueClaims";

    // Tests switch the job off so it can't change shared test data at random moments.
    public bool Enabled { get; set; } = true;

    // A claim UnderReview for at least this many days (since its review started) is overdue.
    public int ThresholdDays { get; set; } = 14;

    // How often the job checks. It also checks once when the API starts.
    public int CheckIntervalMinutes { get; set; } = 60;

    public bool IsValid => ThresholdDays > 0 && CheckIntervalMinutes > 0;
}
