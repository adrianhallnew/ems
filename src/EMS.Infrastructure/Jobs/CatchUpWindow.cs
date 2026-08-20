namespace EMS.Infrastructure.Jobs;

/// <summary>
/// Which dates a catch-up pass owes.
/// </summary>
/// <remarks>
/// Pure, so the watermark arithmetic can be tested without a database or a host. The rule is
/// architecture.md §4.10: process every outstanding date, not merely yesterday, because the
/// application is not assumed to have been running.
/// </remarks>
public static class CatchUpWindow
{
    /// <summary>Resolves the dates one pass must process.</summary>
    /// <param name="watermark">The last date processed successfully, or null on a first run.</param>
    /// <param name="today">The current SCT date.</param>
    /// <param name="initialCatchUpDays">How far back a first run reaches.</param>
    /// <returns>
    /// The first date to process, the last one, and whether there is anything to do. When there is
    /// not, <c>From</c> is later than <c>To</c>.
    /// </returns>
    /// <remarks>
    /// Only fully elapsed SCT dates are eligible, so the last processable date is always yesterday
    /// (spec §3.3.5). A watermark already at yesterday yields an empty window rather than a re-run.
    /// </remarks>
    public static (DateOnly From, DateOnly To, bool HasWork) Resolve(
        DateOnly? watermark,
        DateOnly today,
        int initialCatchUpDays)
    {
        var lastProcessable = today.AddDays(-1);

        var from = watermark is { } processed
            ? processed.AddDays(1)
            : lastProcessable.AddDays(-initialCatchUpDays);

        return (from, lastProcessable, from <= lastProcessable);
    }
}
