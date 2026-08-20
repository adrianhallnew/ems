using EMS.Infrastructure.Jobs;
using Shouldly;

namespace EMS.UnitTests.Jobs;

/// <summary>
/// Covers the watermark arithmetic behind both background jobs. A job that only ever processes
/// "yesterday" silently skips every day the container was stopped, which is the defect the
/// watermark exists to prevent (spec §3.3.5, architecture.md §4.10).
/// </summary>
public class CatchUpWindowTests
{
    private static readonly DateOnly Today = new(2026, 8, 20);

    [Fact]
    public void Resolve_WithNoWatermark_ReachesBackTheInitialWindow()
    {
        var (from, to, hasWork) = CatchUpWindow.Resolve(null, Today, initialCatchUpDays: 7);

        from.ShouldBe(new DateOnly(2026, 8, 12));
        to.ShouldBe(new DateOnly(2026, 8, 19));
        hasWork.ShouldBeTrue();
    }

    [Fact]
    public void Resolve_StopsAtYesterday_BecauseTodayHasNotElapsed()
    {
        var (_, to, _) = CatchUpWindow.Resolve(new DateOnly(2026, 8, 18), Today, 7);

        to.ShouldBe(new DateOnly(2026, 8, 19));
    }

    [Fact]
    public void Resolve_ResumesFromTheDayAfterTheWatermark()
    {
        var (from, _, _) = CatchUpWindow.Resolve(new DateOnly(2026, 8, 15), Today, 7);

        from.ShouldBe(new DateOnly(2026, 8, 16));
    }

    [Fact]
    public void Resolve_AfterALongOutage_CoversEveryMissedDate()
    {
        // Down since the end of July: every date from 1 August to yesterday is still owed.
        var (from, to, hasWork) = CatchUpWindow.Resolve(new DateOnly(2026, 7, 31), Today, 7);

        from.ShouldBe(new DateOnly(2026, 8, 1));
        to.ShouldBe(new DateOnly(2026, 8, 19));
        hasWork.ShouldBeTrue();

        var dates = 0;

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            dates++;
        }

        dates.ShouldBe(19);
    }

    [Fact]
    public void Resolve_WhenAlreadyCaughtUp_ReportsNoWork()
    {
        var (from, to, hasWork) = CatchUpWindow.Resolve(new DateOnly(2026, 8, 19), Today, 7);

        hasWork.ShouldBeFalse();
        from.ShouldBeGreaterThan(to);
    }

    [Fact]
    public void Resolve_WithAWatermarkAheadOfYesterday_ReportsNoWork()
    {
        // A clock moved backwards, or a watermark written by a future version. Either way the job
        // must not reprocess, and must not walk backwards.
        var (_, _, hasWork) = CatchUpWindow.Resolve(new DateOnly(2026, 8, 25), Today, 7);

        hasWork.ShouldBeFalse();
    }
}
