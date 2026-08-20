using EMS.Application.Reports;
using Shouldly;

namespace EMS.UnitTests.Reports;

/// <summary>
/// Covers the report presets. Every boundary is an SCT calendar date (spec §3.6, §3.3.3), so these
/// cases pin the arithmetic rather than the time zone the server happens to run in.
/// </summary>
public class ReportRangeRulesTests
{
    private static readonly DateOnly Today = new(2026, 8, 20);

    [Fact]
    public void Resolve_ThisMonth_CoversTheWholeCalendarMonth()
    {
        var (from, to) = ReportRangeRules.Resolve(ReportPeriod.ThisMonth, null, null, Today);

        from.ShouldBe(new DateOnly(2026, 8, 1));
        to.ShouldBe(new DateOnly(2026, 8, 31));
    }

    [Fact]
    public void Resolve_LastMonth_CoversTheWholePreviousMonth()
    {
        var (from, to) = ReportRangeRules.Resolve(ReportPeriod.LastMonth, null, null, Today);

        from.ShouldBe(new DateOnly(2026, 7, 1));
        to.ShouldBe(new DateOnly(2026, 7, 31));
    }

    [Fact]
    public void Resolve_LastMonth_InJanuary_CrossesTheYearBoundary()
    {
        var (from, to) = ReportRangeRules.Resolve(
            ReportPeriod.LastMonth,
            null,
            null,
            new DateOnly(2026, 1, 15));

        from.ShouldBe(new DateOnly(2025, 12, 1));
        to.ShouldBe(new DateOnly(2025, 12, 31));
    }

    [Fact]
    public void Resolve_LastMonth_FromA31st_LandsOnAShorterMonth()
    {
        // 31 March minus a month is 28 February, and the range must still be the whole of February.
        var (from, to) = ReportRangeRules.Resolve(
            ReportPeriod.LastMonth,
            null,
            null,
            new DateOnly(2026, 3, 31));

        from.ShouldBe(new DateOnly(2026, 2, 1));
        to.ShouldBe(new DateOnly(2026, 2, 28));
    }

    [Fact]
    public void Resolve_ThisMonth_InALeapFebruary_EndsOnThe29th()
    {
        var (_, to) = ReportRangeRules.Resolve(
            ReportPeriod.ThisMonth,
            null,
            null,
            new DateOnly(2028, 2, 10));

        to.ShouldBe(new DateOnly(2028, 2, 29));
    }

    [Fact]
    public void Resolve_ThisYear_CoversJanuaryToDecember()
    {
        var (from, to) = ReportRangeRules.Resolve(ReportPeriod.ThisYear, null, null, Today);

        from.ShouldBe(new DateOnly(2026, 1, 1));
        to.ShouldBe(new DateOnly(2026, 12, 31));
    }

    [Fact]
    public void Resolve_LastYear_CoversThePreviousYear()
    {
        var (from, to) = ReportRangeRules.Resolve(ReportPeriod.LastYear, null, null, Today);

        from.ShouldBe(new DateOnly(2025, 1, 1));
        to.ShouldBe(new DateOnly(2025, 12, 31));
    }

    [Fact]
    public void Resolve_Custom_UsesTheSuppliedDates()
    {
        var (from, to) = ReportRangeRules.Resolve(
            ReportPeriod.Custom,
            new DateOnly(2026, 3, 4),
            new DateOnly(2026, 5, 6),
            Today);

        from.ShouldBe(new DateOnly(2026, 3, 4));
        to.ShouldBe(new DateOnly(2026, 5, 6));
    }

    [Fact]
    public void Resolve_Custom_WithNothingSupplied_FallsBackToThisMonth()
    {
        // Not an unbounded range: that is a table scan with a report attached.
        var (from, to) = ReportRangeRules.Resolve(ReportPeriod.Custom, null, null, Today);

        from.ShouldBe(new DateOnly(2026, 8, 1));
        to.ShouldBe(new DateOnly(2026, 8, 31));
    }
}
