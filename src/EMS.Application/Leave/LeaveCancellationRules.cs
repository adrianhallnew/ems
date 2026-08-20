namespace EMS.Application.Leave;

/// <summary>
/// How many days a cancellation gives back.
/// </summary>
/// <remarks>
/// Pure, and takes today and the holiday set as parameters, so every boundary is testable without a
/// database. The rule is spec §3.4.5: cancelling before the leave starts restores all of it,
/// cancelling once it has started restores only the days from today forward. v2.0 restored the full
/// amount either way, crediting employees for leave they had already taken.
/// </remarks>
public static class LeaveCancellationRules
{
    /// <summary>Calculates the restorable days for one cancellation.</summary>
    /// <param name="startDate">The first day of the leave.</param>
    /// <param name="endDate">The last day of the leave.</param>
    /// <param name="bookedDays">The business days deducted when the request was approved.</param>
    /// <param name="today">The current SCT date.</param>
    /// <param name="holidays">Public holidays inside the remaining window.</param>
    /// <returns>Days to add back to the balance. Never more than were deducted.</returns>
    public static int RestorableDays(
        DateOnly startDate,
        DateOnly endDate,
        int bookedDays,
        DateOnly today,
        IReadOnlySet<DateOnly> holidays)
    {
        // Not started yet: everything comes back.
        if (today <= startDate)
        {
            return bookedDays;
        }

        // Already finished: the leave was taken, so nothing does.
        if (today > endDate)
        {
            return 0;
        }

        var remaining = BusinessDayRules.Count(today, endDate, holidays);

        // The booked count is the ceiling. A holiday deleted since approval would otherwise make
        // the remaining window look longer than what was deducted.
        return Math.Min(remaining, bookedDays);
    }
}
