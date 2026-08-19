using EMS.Application.Attendance;
using EMS.Domain.Enums;
using Shouldly;

namespace EMS.UnitTests.Attendance;

/// <summary>
/// Covers spec section 3.3.7. The order is the rule, so most of these tests assert precedence
/// rather than a single condition: an employee on approved leave over a public holiday resolves to
/// Holiday, and nobody is ever Absent on a Saturday.
/// </summary>
public class AttendanceStateRulesTests
{
    private static readonly DateOnly Hired = new(2026, 1, 1);
    private static readonly DateOnly Wednesday = new(2026, 8, 19);
    private static readonly DateOnly Saturday = new(2026, 8, 22);
    private static readonly TimeOnly WorkDayStart = new(8, 0);
    private static readonly TimeOnly WorkDayEnd = new(16, 0);

    private static AttendanceState Resolve(
        DateOnly date,
        DateOnly? lastEmployedDate = null,
        bool isPublicHoliday = false,
        bool isOnApprovedLeave = false,
        TimeOnly? clockInSct = null) =>
        AttendanceStateRules.Resolve(
            date,
            Hired,
            lastEmployedDate,
            isPublicHoliday,
            isOnApprovedLeave,
            clockInSct,
            WorkDayStart);

    [Fact]
    public void Resolve_BeforeTheHireDate_IsNotEmployed()
    {
        Resolve(new DateOnly(2025, 12, 31)).ShouldBe(AttendanceState.NotEmployed);
    }

    [Fact]
    public void Resolve_AfterDeactivation_IsNotEmployed()
    {
        Resolve(Wednesday, lastEmployedDate: new DateOnly(2026, 8, 18))
            .ShouldBe(AttendanceState.NotEmployed);
    }

    [Fact]
    public void Resolve_OnTheDeactivationDateItself_IsStillEmployed()
    {
        Resolve(Wednesday, lastEmployedDate: Wednesday).ShouldBe(AttendanceState.Absent);
    }

    [Fact]
    public void Resolve_ASaturday_IsWeekend()
    {
        Resolve(Saturday).ShouldBe(AttendanceState.Weekend);
    }

    [Fact]
    public void Resolve_APublicHolidayOnAWeekday_IsHoliday()
    {
        Resolve(Wednesday, isPublicHoliday: true).ShouldBe(AttendanceState.Holiday);
    }

    [Fact]
    public void Resolve_ApprovedLeave_IsOnLeave()
    {
        Resolve(Wednesday, isOnApprovedLeave: true).ShouldBe(AttendanceState.OnLeave);
    }

    [Fact]
    public void Resolve_AClockInAfterTheStartOfTheDay_IsLate()
    {
        Resolve(Wednesday, clockInSct: new TimeOnly(8, 1)).ShouldBe(AttendanceState.Late);
    }

    [Fact]
    public void Resolve_AClockInExactlyAtTheStartOfTheDay_IsPresent()
    {
        Resolve(Wednesday, clockInSct: WorkDayStart).ShouldBe(AttendanceState.Present);
    }

    [Fact]
    public void Resolve_AnEarlyClockIn_IsPresent()
    {
        Resolve(Wednesday, clockInSct: new TimeOnly(7, 30)).ShouldBe(AttendanceState.Present);
    }

    [Fact]
    public void Resolve_AWorkingDayWithNoRecord_IsAbsent()
    {
        Resolve(Wednesday).ShouldBe(AttendanceState.Absent);
    }

    [Fact]
    public void Resolve_NotEmployedBeatsEveryOtherCondition()
    {
        AttendanceStateRules.Resolve(
            new DateOnly(2025, 6, 1),
            Hired,
            lastEmployedDate: null,
            isPublicHoliday: true,
            isOnApprovedLeave: true,
            clockInSct: new TimeOnly(9, 0),
            WorkDayStart).ShouldBe(AttendanceState.NotEmployed);
    }

    [Fact]
    public void Resolve_WeekendBeatsAPublicHolidayAndLeave()
    {
        Resolve(Saturday, isPublicHoliday: true, isOnApprovedLeave: true)
            .ShouldBe(AttendanceState.Weekend);
    }

    [Fact]
    public void Resolve_AHolidayBeatsLeave()
    {
        Resolve(Wednesday, isPublicHoliday: true, isOnApprovedLeave: true)
            .ShouldBe(AttendanceState.Holiday);
    }

    [Fact]
    public void Resolve_LeaveBeatsAClockIn()
    {
        // Somebody who clocked in while on approved leave still reads as OnLeave; the stored event
        // is kept, but it does not change the day's meaning.
        Resolve(Wednesday, isOnApprovedLeave: true, clockInSct: new TimeOnly(9, 30))
            .ShouldBe(AttendanceState.OnLeave);
    }

    [Theory]
    [InlineData(AttendanceState.Present, true)]
    [InlineData(AttendanceState.Late, true)]
    [InlineData(AttendanceState.Absent, true)]
    [InlineData(AttendanceState.Weekend, false)]
    [InlineData(AttendanceState.Holiday, false)]
    [InlineData(AttendanceState.OnLeave, false)]
    [InlineData(AttendanceState.NotEmployed, false)]
    public void AllowsClocking_IsFalseOnEveryNonWorkingState(AttendanceState state, bool expected)
    {
        AttendanceStateRules.AllowsClocking(state).ShouldBe(expected);
    }

    [Fact]
    public void IsEarlyDeparture_IsTrueOnlyBeforeTheEndOfTheDay()
    {
        AttendanceStateRules.IsEarlyDeparture(new TimeOnly(15, 59), WorkDayEnd).ShouldBeTrue();
        AttendanceStateRules.IsEarlyDeparture(WorkDayEnd, WorkDayEnd).ShouldBeFalse();
        AttendanceStateRules.IsEarlyDeparture(new TimeOnly(17, 0), WorkDayEnd).ShouldBeFalse();
    }

    [Fact]
    public void IsEarlyDeparture_IsFalseWhenNobodyClockedOut()
    {
        AttendanceStateRules.IsEarlyDeparture(null, WorkDayEnd).ShouldBeFalse();
    }
}
