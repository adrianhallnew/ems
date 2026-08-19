using EMS.Application.Leave;
using Shouldly;

namespace EMS.UnitTests.Leave;

/// <summary>
/// Covers business-day counting. The counts here decide how many days a leave request costs, so an
/// off-by-one is a balance error rather than a display error.
/// </summary>
public class BusinessDayRulesTests
{
    private static readonly IReadOnlySet<DateOnly> NoHolidays = new HashSet<DateOnly>();

    [Fact]
    public void Count_AFullWorkingWeek_IsFiveDays()
    {
        // Monday 17 August 2026 to Friday 21 August 2026.
        BusinessDayRules.Count(new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 21), NoHolidays)
            .ShouldBe(5);
    }

    [Fact]
    public void Count_ARangeSpanningAWeekend_ExcludesSaturdayAndSunday()
    {
        // Friday to the following Monday: two working days.
        BusinessDayRules.Count(new DateOnly(2026, 8, 21), new DateOnly(2026, 8, 24), NoHolidays)
            .ShouldBe(2);
    }

    [Fact]
    public void Count_AWeekendOnlyRange_IsZero()
    {
        BusinessDayRules.Count(new DateOnly(2026, 8, 22), new DateOnly(2026, 8, 23), NoHolidays)
            .ShouldBe(0);
    }

    [Fact]
    public void Count_ExcludesPublicHolidaysThatFallOnAWeekday()
    {
        var holidays = new HashSet<DateOnly> { new(2026, 8, 19) };   // a Wednesday

        BusinessDayRules.Count(new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 21), holidays)
            .ShouldBe(4);
    }

    [Fact]
    public void Count_DoesNotDoubleCountAHolidayFallingOnAWeekend()
    {
        var holidays = new HashSet<DateOnly> { new(2026, 8, 22) };   // a Saturday

        BusinessDayRules.Count(new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 23), holidays)
            .ShouldBe(5);
    }

    [Fact]
    public void Count_ASingleWorkingDay_IsOne()
    {
        BusinessDayRules.Count(new DateOnly(2026, 8, 19), new DateOnly(2026, 8, 19), NoHolidays)
            .ShouldBe(1);
    }

    [Fact]
    public void Count_AnInvertedRange_IsZero()
    {
        BusinessDayRules.Count(new DateOnly(2026, 8, 21), new DateOnly(2026, 8, 17), NoHolidays)
            .ShouldBe(0);
    }

    [Theory]
    [InlineData(2026, 8, 22, false)]    // Saturday
    [InlineData(2026, 8, 23, false)]    // Sunday
    [InlineData(2026, 8, 24, true)]     // Monday
    public void IsWorkingDay_RejectsWeekends(int year, int month, int day, bool expected)
    {
        BusinessDayRules.IsWorkingDay(new DateOnly(year, month, day), NoHolidays).ShouldBe(expected);
    }
}
