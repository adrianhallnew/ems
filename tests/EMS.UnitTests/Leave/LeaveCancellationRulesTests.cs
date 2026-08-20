using EMS.Application.Leave;
using Shouldly;

namespace EMS.UnitTests.Leave;

/// <summary>
/// Covers how much balance a cancellation gives back. v2.0 restored the full amount whenever leave
/// was cancelled, which credited employees for days they had already taken (spec §3.4.5).
/// </summary>
public class LeaveCancellationRulesTests
{
    private static readonly IReadOnlySet<DateOnly> NoHolidays = new HashSet<DateOnly>();

    // Monday 17 August 2026 to Friday 21 August 2026: five business days.
    private static readonly DateOnly Start = new(2026, 8, 17);
    private static readonly DateOnly End = new(2026, 8, 21);

    [Fact]
    public void RestorableDays_CancelledBeforeItStarts_RestoresEverything()
    {
        LeaveCancellationRules
            .RestorableDays(Start, End, bookedDays: 5, today: new DateOnly(2026, 8, 14), NoHolidays)
            .ShouldBe(5);
    }

    [Fact]
    public void RestorableDays_CancelledOnTheFirstDay_RestoresEverything()
    {
        // The boundary is inclusive: nothing has been consumed on the morning it starts.
        LeaveCancellationRules
            .RestorableDays(Start, End, bookedDays: 5, today: Start, NoHolidays)
            .ShouldBe(5);
    }

    [Fact]
    public void RestorableDays_CancelledMidLeave_RestoresOnlyTheRemainingDays()
    {
        // Wednesday: Wednesday, Thursday and Friday are still to come.
        LeaveCancellationRules
            .RestorableDays(Start, End, bookedDays: 5, today: new DateOnly(2026, 8, 19), NoHolidays)
            .ShouldBe(3);
    }

    [Fact]
    public void RestorableDays_CancelledOnTheLastDay_RestoresThatDay()
    {
        LeaveCancellationRules
            .RestorableDays(Start, End, bookedDays: 5, today: End, NoHolidays)
            .ShouldBe(1);
    }

    [Fact]
    public void RestorableDays_CancelledAfterItEnds_RestoresNothing()
    {
        LeaveCancellationRules
            .RestorableDays(Start, End, bookedDays: 5, today: new DateOnly(2026, 8, 24), NoHolidays)
            .ShouldBe(0);
    }

    [Fact]
    public void RestorableDays_ExcludesWeekendsFromTheRemainingWindow()
    {
        // Friday 21 August to Tuesday 25 August, cancelled on the Monday: Monday and Tuesday only.
        LeaveCancellationRules
            .RestorableDays(
                new DateOnly(2026, 8, 21),
                new DateOnly(2026, 8, 25),
                bookedDays: 3,
                today: new DateOnly(2026, 8, 24),
                NoHolidays)
            .ShouldBe(2);
    }

    [Fact]
    public void RestorableDays_ExcludesHolidaysFromTheRemainingWindow()
    {
        var holidays = new HashSet<DateOnly> { new(2026, 8, 20) };   // a Thursday

        LeaveCancellationRules
            .RestorableDays(Start, End, bookedDays: 4, today: new DateOnly(2026, 8, 19), holidays)
            .ShouldBe(2);
    }

    [Fact]
    public void RestorableDays_NeverExceedsWhatWasDeducted()
    {
        // A holiday deleted since approval makes the remaining window look longer than the booked
        // count. The employee still gets back only what was taken from them.
        LeaveCancellationRules
            .RestorableDays(Start, End, bookedDays: 2, today: Start, NoHolidays)
            .ShouldBe(2);
    }
}
