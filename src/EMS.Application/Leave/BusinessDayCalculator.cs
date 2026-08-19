using EMS.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EMS.Application.Leave;

/// <summary>Counts business days, loading the public holidays the range needs.</summary>
/// <param name="factory">Creates one short-lived context per call.</param>
public sealed class BusinessDayCalculator(IApplicationDbContextFactory factory) : IBusinessDayCalculator
{
    /// <inheritdoc/>
    public async Task<int> CountBusinessDaysAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct)
    {
        if (endDate < startDate)
        {
            return 0;
        }

        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        // A set, not a list: the range is probed once per day it contains.
        var holidays = (await db.PublicHolidays
            .Where(h => h.Date >= startDate && h.Date <= endDate)
            .Select(h => h.Date)
            .ToListAsync(ct)
            .ConfigureAwait(false))
            .ToHashSet();

        return BusinessDayRules.Count(startDate, endDate, holidays);
    }
}
