using EMS.Application.Holidays;
using EMS.Domain.Enums;
using Shouldly;

namespace EMS.UnitTests.Holidays;

/// <summary>
/// Covers the holiday command rules. Spec §3.7.1 pairs <c>Rule</c> with <c>Easter Offset</c>: an
/// Easter-relative entry without one cannot be projected onto next year, and a fixed-date entry
/// carrying one describes two different dates at once.
/// </summary>
public class HolidayValidatorsTests
{
    private static readonly DateOnly SomeDate = new(2026, 6, 18);

    [Fact]
    public void Create_AFixedDateHoliday_IsValid()
    {
        Validate(new CreateHolidayCommand("National Day", SomeDate, HolidayRule.FixedDate, null))
            .ShouldBeTrue();
    }

    [Fact]
    public void Create_AnEasterRelativeHolidayWithAnOffset_IsValid()
    {
        Validate(new CreateHolidayCommand("Corpus Christi", SomeDate, HolidayRule.EasterRelative, 60))
            .ShouldBeTrue();
    }

    [Fact]
    public void Create_AnEasterRelativeHolidayWithoutAnOffset_IsRejected()
    {
        Validate(new CreateHolidayCommand("Corpus Christi", SomeDate, HolidayRule.EasterRelative, null))
            .ShouldBeFalse();
    }

    [Fact]
    public void Create_AFixedDateHolidayWithAnOffset_IsRejected()
    {
        Validate(new CreateHolidayCommand("National Day", SomeDate, HolidayRule.FixedDate, 60))
            .ShouldBeFalse();
    }

    [Fact]
    public void Create_ANegativeOffset_IsValid()
    {
        // Good Friday is Easter minus two. Offsets run in both directions.
        Validate(new CreateHolidayCommand("Good Friday", SomeDate, HolidayRule.EasterRelative, -2))
            .ShouldBeTrue();
    }

    [Fact]
    public void Create_WithoutAName_IsRejected()
    {
        Validate(new CreateHolidayCommand("   ", SomeDate, HolidayRule.FixedDate, null))
            .ShouldBeFalse();
    }

    [Fact]
    public void Update_AnEasterRelativeHolidayWithoutAnOffset_IsRejected()
    {
        new UpdateHolidayValidator()
            .Validate(new UpdateHolidayCommand(
                Guid.NewGuid(),
                "Corpus Christi",
                SomeDate,
                HolidayRule.EasterRelative,
                null))
            .IsValid
            .ShouldBeFalse();
    }

    [Fact]
    public void Update_WithoutAnIdentifier_IsRejected()
    {
        new UpdateHolidayValidator()
            .Validate(new UpdateHolidayCommand(
                Guid.Empty,
                "National Day",
                SomeDate,
                HolidayRule.FixedDate,
                null))
            .IsValid
            .ShouldBeFalse();
    }

    private static bool Validate(CreateHolidayCommand command) =>
        new CreateHolidayValidator().Validate(command).IsValid;
}
