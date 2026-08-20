namespace EMS.Application.Attendance;

/// <summary>
/// The sort allow-list for resolved attendance days.
/// </summary>
/// <remarks>
/// The <c>IQueryable</c> allow-lists in <c>Common/Security/SortAllowLists.cs</c> cannot serve this
/// grid: an absence has no row, so the population is projected in memory rather than queried
/// (ADR-0004). The polarity of architecture.md §5.4 still holds — a name that is not recognised
/// falls back to the default order instead of being honoured.
/// </remarks>
public static class AttendanceDaySort
{
    /// <summary>Orders resolved days by an allow-listed column.</summary>
    /// <param name="days">The resolved days.</param>
    /// <param name="sortBy">The requested column, or null.</param>
    /// <param name="descending">Whether to reverse the order.</param>
    /// <returns>The days in order. Most recent first, then by employee, when nothing matches.</returns>
    public static IOrderedEnumerable<AttendanceDayDto> Apply(
        IEnumerable<AttendanceDayDto> days,
        string? sortBy,
        bool descending)
    {
        ArgumentNullException.ThrowIfNull(days);

        return sortBy?.ToLowerInvariant() switch
        {
            "employeename" => descending
                ? days.OrderByDescending(d => d.EmployeeName)
                : days.OrderBy(d => d.EmployeeName),
            "state" => descending
                ? days.OrderByDescending(d => d.State)
                : days.OrderBy(d => d.State),
            "clockin" => descending
                ? days.OrderByDescending(d => d.ClockIn)
                : days.OrderBy(d => d.ClockIn),
            "workedminutes" => descending
                ? days.OrderByDescending(d => d.WorkedMinutes)
                : days.OrderBy(d => d.WorkedMinutes),
            "isflagged" => descending
                ? days.OrderByDescending(d => d.IsFlagged)
                : days.OrderBy(d => d.IsFlagged),
            "date" => descending
                ? days.OrderByDescending(d => d.Date).ThenBy(d => d.EmployeeName)
                : days.OrderBy(d => d.Date).ThenBy(d => d.EmployeeName),
            _ => days.OrderByDescending(d => d.Date).ThenBy(d => d.EmployeeName),
        };
    }
}
