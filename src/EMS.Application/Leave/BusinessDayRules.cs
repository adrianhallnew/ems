namespace EMS.Application.Leave;

/// <summary>Counts business days over an already-loaded holiday set.</summary>
/// <remarks>
/// Split out from <see cref="BusinessDayCalculator"/> so the counting rule can be tested without a
/// database. The calculator supplies the holidays; this decides what they mean.
/// </remarks>
public static class BusinessDayRules
{
    /// <summary>Counts the weekdays in an inclusive range that are not public holidays.</summary>
    /// <param name="startDate">The first date.</param>
    /// <param name="endDate">The last date.</param>
    /// <param name="holidays">The public holidays falling inside the range.</param>
    /// <returns>The count, which is zero when the range is inverted or holds no working day.</returns>
    public static int Count(DateOnly startDate, DateOnly endDate, IReadOnlySet<DateOnly> holidays)
    {
        ArgumentNullException.ThrowIfNull(holidays);

        if (endDate < startDate)
        {
            return 0;
        }

        var count = 0;

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            if (IsWorkingDay(date, holidays))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Reports whether one date is a working day.</summary>
    /// <param name="date">The date.</param>
    /// <param name="holidays">The public holidays to consider.</param>
    /// <returns><c>true</c> when the date is a weekday and not a holiday.</returns>
    public static bool IsWorkingDay(DateOnly date, IReadOnlySet<DateOnly> holidays)
    {
        ArgumentNullException.ThrowIfNull(holidays);

        if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            return false;
        }

        return !holidays.Contains(date);
    }
}
