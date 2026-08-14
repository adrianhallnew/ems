using EMS.Domain.Entities;
using EMS.Domain.Exceptions;
using Shouldly;

namespace EMS.UnitTests.Entities;

/// <summary>
/// Covers the two pure functions on <see cref="Employee"/>. Both take the current date as a
/// parameter, so no clock or test double is involved and the boundaries are exact.
/// </summary>
public class EmployeeTests
{
    private static Employee HiredOn(int year, int month, int day) =>
        new() { HireDate = new DateOnly(year, month, day) };

    [Theory]
    [InlineData(2026, 1, 1, true)]      // hire date itself
    [InlineData(2026, 3, 31, true)]     // one day before the boundary
    [InlineData(2026, 4, 1, false)]     // the boundary date — probation is over
    [InlineData(2026, 4, 2, false)]     // one day after
    public void IsInProbation_ReturnsTrueOnlyBeforeTheBoundary(int year, int month, int day, bool expected)
    {
        var employee = HiredOn(2026, 1, 1);

        employee.IsInProbation(new DateOnly(year, month, day), probationMonths: 3).ShouldBe(expected);
    }

    [Fact]
    public void IsInProbation_HonoursAShorterProbationPeriod()
    {
        var employee = HiredOn(2026, 1, 1);

        employee.IsInProbation(new DateOnly(2026, 1, 31), probationMonths: 1).ShouldBeTrue();
        employee.IsInProbation(new DateOnly(2026, 2, 1), probationMonths: 1).ShouldBeFalse();
    }

    [Fact]
    public void PeriodFor_TheHireDateItself_StartsTheFirstPeriod()
    {
        var employee = HiredOn(2026, 6, 15);

        var (start, end) = employee.PeriodFor(new DateOnly(2026, 6, 15));

        start.ShouldBe(new DateOnly(2026, 6, 15));
        end.ShouldBe(new DateOnly(2027, 6, 14));
    }

    [Fact]
    public void PeriodFor_ADateMidPeriod_ReturnsTheEnclosingWindow()
    {
        var employee = HiredOn(2024, 6, 15);

        var (start, end) = employee.PeriodFor(new DateOnly(2026, 1, 20));

        start.ShouldBe(new DateOnly(2025, 6, 15));
        end.ShouldBe(new DateOnly(2026, 6, 14));
    }

    [Fact]
    public void PeriodFor_TheDayBeforeAnAnniversary_StaysInTheOlderPeriod()
    {
        var employee = HiredOn(2024, 6, 15);

        var (start, end) = employee.PeriodFor(new DateOnly(2026, 6, 14));

        start.ShouldBe(new DateOnly(2025, 6, 15));
        end.ShouldBe(new DateOnly(2026, 6, 14));
    }

    [Fact]
    public void PeriodFor_AnAnniversary_StartsTheNewPeriod()
    {
        var employee = HiredOn(2024, 6, 15);

        var (start, end) = employee.PeriodFor(new DateOnly(2026, 6, 15));

        start.ShouldBe(new DateOnly(2026, 6, 15));
        end.ShouldBe(new DateOnly(2027, 6, 14));
    }

    [Fact]
    public void PeriodFor_AJanuaryHireDateReadInDecember_DoesNotRunAhead()
    {
        var employee = HiredOn(2025, 1, 10);

        var (start, end) = employee.PeriodFor(new DateOnly(2026, 12, 31));

        start.ShouldBe(new DateOnly(2026, 1, 10));
        end.ShouldBe(new DateOnly(2027, 1, 9));
    }

    [Fact]
    public void PeriodFor_ALeapDayHireDate_ResolvesToTheTwentyEighthInNonLeapYears()
    {
        var employee = HiredOn(2024, 2, 29);

        var (start, end) = employee.PeriodFor(new DateOnly(2025, 6, 1));

        start.ShouldBe(new DateOnly(2025, 2, 28));
        end.ShouldBe(new DateOnly(2026, 2, 27));
    }

    [Fact]
    public void PeriodFor_ALeapDayHireDate_ProducesContiguousPeriods()
    {
        var employee = HiredOn(2024, 2, 29);

        var (_, firstEnd) = employee.PeriodFor(new DateOnly(2025, 6, 1));
        var (secondStart, _) = employee.PeriodFor(new DateOnly(2026, 6, 1));

        secondStart.ShouldBe(firstEnd.AddDays(1));
    }

    [Fact]
    public void PeriodFor_ADateBeforeTheHireDate_Throws()
    {
        var employee = HiredOn(2026, 6, 15);

        Should.Throw<InvalidDateRangeException>(() => employee.PeriodFor(new DateOnly(2026, 6, 14)));
    }
}
