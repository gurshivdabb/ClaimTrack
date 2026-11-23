namespace ClaimTrack.Api.Data;

// Names of the SQL Server sequences that number policies and claims (BR-10).
// Used by AppDbContext to create them and by NumberGenerator to read from them.
public static class Sequences
{
    public const string PolicyNumbers = "PolicyNumbers";
    public const string ClaimNumbers = "ClaimNumbers";
}
