namespace EMS.Application.Reports;

/// <summary>
/// Turns a preset into a date range.
/// </summary>
/// <remarks>
/// Pure, and takes today as a parameter, so the boundaries can be tested without a clock. Every
/// boundary is an SCT calendar date, consistent with spec §3.3.3 and required by spec §3.6.
/// </remarks>
public static class ReportRangeRules
{
    /// <summary>Resolves the range a report covers.</summary>
    /// <param name="period">The preset the caller chose.</param>
    /// <param name="from">The custom start, used only when the period is Custom.</param>
    /// <param name="to">The custom end, used only when the period is Custom.</param>
    /// <param name="today">The current SCT date.</param>
    /// <returns>The inclusive range.</returns>
    /// <remarks>
    /// A Custom period with nothing supplied falls back to this month. The alternative — an
    /// unbounded range — is a table scan with a report attached.
    /// </remarks>
    public static (DateOnly From, DateOnly To) Resolve(
        ReportPeriod period,
        DateOnly? from,
        DateOnly? to,
        DateOnly today) =>
        period switch
        {
            ReportPeriod.ThisMonth => MonthOf(today),
            ReportPeriod.LastMonth => MonthOf(today.AddMonths(-1)),
            ReportPeriod.ThisYear => YearOf(today.Year),
            ReportPeriod.LastYear => YearOf(today.Year - 1),
            _ => (from ?? MonthOf(today).From, to ?? MonthOf(today).To),
        };

    private static (DateOnly From, DateOnly To) MonthOf(DateOnly date)
    {
        var start = new DateOnly(date.Year, date.Month, 1);

        return (start, start.AddMonths(1).AddDays(-1));
    }

    private static (DateOnly From, DateOnly To) YearOf(int year) =>
        (new DateOnly(year, 1, 1), new DateOnly(year, 12, 31));
}
