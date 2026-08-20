using EMS.Application.Attendance;
using EMS.Domain.Enums;
using Shouldly;

namespace EMS.UnitTests.Attendance;

/// <summary>
/// Covers the sort allow-list for resolved attendance days. An unrecognised column name must fall
/// back to the default order rather than being honoured, which is the polarity architecture.md §5.4
/// requires of every sort.
/// </summary>
public class AttendanceDaySortTests
{
    private static readonly AttendanceDayDto Early = Day("Adrienne", new DateOnly(2026, 8, 17), AttendanceState.Present, 480, flagged: false);
    private static readonly AttendanceDayDto Middle = Day("Belmont", new DateOnly(2026, 8, 18), AttendanceState.Late, 300, flagged: true);
    private static readonly AttendanceDayDto Recent = Day("Charlot", new DateOnly(2026, 8, 19), AttendanceState.Absent, null, flagged: false);

    private static readonly AttendanceDayDto[] Days = [Middle, Recent, Early];

    [Fact]
    public void Apply_WithNoSort_PutsTheMostRecentFirst()
    {
        AttendanceDaySort.Apply(Days, null, descending: false)
            .Select(d => d.Date)
            .ShouldBe([Recent.Date, Middle.Date, Early.Date]);
    }

    [Fact]
    public void Apply_WithAnUnrecognisedColumn_FallsBackToTheDefault()
    {
        // The name a grid could send if someone renamed a column, or if a caller guessed.
        AttendanceDaySort.Apply(Days, "salary", descending: false)
            .Select(d => d.Date)
            .ShouldBe([Recent.Date, Middle.Date, Early.Date]);
    }

    [Fact]
    public void Apply_ByDateAscending_ReversesTheDefault()
    {
        AttendanceDaySort.Apply(Days, "date", descending: false)
            .Select(d => d.Date)
            .ShouldBe([Early.Date, Middle.Date, Recent.Date]);
    }

    [Fact]
    public void Apply_ByEmployeeName_OrdersAlphabetically()
    {
        AttendanceDaySort.Apply(Days, "employeename", descending: false)
            .Select(d => d.EmployeeName)
            .ShouldBe(["Adrienne", "Belmont", "Charlot"]);
    }

    [Fact]
    public void Apply_IsCaseInsensitive()
    {
        AttendanceDaySort.Apply(Days, "EmployeeName", descending: true)
            .Select(d => d.EmployeeName)
            .ShouldBe(["Charlot", "Belmont", "Adrienne"]);
    }

    [Fact]
    public void Apply_ByWorkedMinutes_PutsTheUnworkedDayFirst()
    {
        // A null is not zero: an absence has no worked minutes at all.
        AttendanceDaySort.Apply(Days, "workedminutes", descending: false)
            .Select(d => d.WorkedMinutes)
            .ShouldBe([null, 300, 480]);
    }

    [Fact]
    public void Apply_ByFlaggedDescending_SurfacesTheFlaggedDay()
    {
        AttendanceDaySort.Apply(Days, "isflagged", descending: true)
            .First()
            .IsFlagged
            .ShouldBeTrue();
    }

    [Fact]
    public void Apply_ByState_OrdersByTheEnumsOwnOrder()
    {
        AttendanceDaySort.Apply(Days, "state", descending: false)
            .Select(d => d.State)
            .ShouldBe([AttendanceState.Present, AttendanceState.Late, AttendanceState.Absent]);
    }

    private static AttendanceDayDto Day(
        string name,
        DateOnly date,
        AttendanceState state,
        int? workedMinutes,
        bool flagged) =>
        new(
            Guid.NewGuid(),
            name,
            date,
            state,
            workedMinutes is null ? null : date.ToDateTime(new TimeOnly(4, 0)),
            workedMinutes is null ? null : date.ToDateTime(new TimeOnly(12, 0)),
            workedMinutes,
            flagged,
            IsEarlyDeparture: false,
            CorrectionNote: null,
            RecordId: workedMinutes is null ? null : Guid.NewGuid());
}
