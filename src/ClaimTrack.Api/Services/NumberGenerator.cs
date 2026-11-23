using System.Globalization;
using ClaimTrack.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace ClaimTrack.Api.Services;

// BR-10: builds unique numbers like POL-2026-00001 from SQL Server sequences.
// The counter does not restart each year; uniqueness comes from the sequence.
public class NumberGenerator(AppDbContext db, TimeProvider timeProvider) : INumberGenerator
{
    public Task<string> NextPolicyNumberAsync(CancellationToken cancellationToken)
    {
        return NextAsync("POL", Sequences.PolicyNumbers, cancellationToken);
    }

    public Task<string> NextClaimNumberAsync(CancellationToken cancellationToken)
    {
        return NextAsync("CLM", Sequences.ClaimNumbers, cancellationToken);
    }

    private async Task<string> NextAsync(string prefix, string sequenceName, CancellationToken cancellationToken)
    {
        // ToListAsync runs the SQL as written; SQL Server doesn't allow NEXT VALUE FOR in a subquery.
        // EF1002 warns about building SQL from a variable. It's safe here: sequenceName is always one
        // of the Sequences constants, never user input, and SQL Server can't take a sequence name as
        // a parameter.
#pragma warning disable EF1002
        var values = await db.Database
            .SqlQueryRaw<int>($"SELECT NEXT VALUE FOR [{sequenceName}] AS [Value]")
            .ToListAsync(cancellationToken);
#pragma warning restore EF1002

        return Format(prefix, timeProvider.GetUtcNow().Year, values.Single());
    }

    // The readable part of BR-10, kept pure so it can be unit-tested: POL-2026-00042.
    // At least five digits; past 99999 the number simply gets longer instead of wrapping.
    public static string Format(string prefix, int year, int sequenceValue) =>
        string.Create(CultureInfo.InvariantCulture, $"{prefix}-{year}-{sequenceValue:D5}");
}
