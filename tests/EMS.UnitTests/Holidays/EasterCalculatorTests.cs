using EMS.Application.Holidays;
using EMS.Domain.Enums;
using Shouldly;

namespace EMS.UnitTests.Holidays;

/// <summary>
/// Checks the computus against published Easter dates. Eleven consecutive years, including the
/// leap year 2024 and both extremes of the possible range.
/// </summary>
public class EasterCalculatorTests
{
    [Theory]
    [InlineData(2020, 4, 12)]
    [InlineData(2021, 4, 4)]
    [InlineData(2022, 4, 17)]
    [InlineData(2023, 4, 9)]
    [InlineData(2024, 3, 31)]   // leap year
    [InlineData(2025, 4, 20)]
    [InlineData(2026, 4, 5)]
    [InlineData(2027, 3, 28)]
    [InlineData(2028, 4, 16)]   // leap year
    [InlineData(2029, 4, 1)]
    [InlineData(2030, 4, 21)]
    public void EasterSunday_MatchesThePublishedDate(int year, int month, int day)
    {
        EasterCalculator.EasterSunday(year).ShouldBe(new DateOnly(year, month, day));
    }

    [Theory]
    [InlineData(1818, 3, 22)]   // the earliest Easter can fall
    [InlineData(1943, 4, 25)]   // the latest Easter can fall
    public void EasterSunday_HandlesTheExtremesOfTheRange(int year, int month, int day)
    {
        EasterCalculator.EasterSunday(year).ShouldBe(new DateOnly(year, month, day));
    }

    [Fact]
    public void EasterSunday_IsAlwaysASunday()
    {
        for (var year = 2020; year <= 2050; year++)
        {
            EasterCalculator.EasterSunday(year).DayOfWeek.ShouldBe(DayOfWeek.Sunday);
        }
    }

    [Fact]
    public void EasterSunday_RejectsYearsBeforeTheGregorianReform()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => EasterCalculator.EasterSunday(1582));
    }
}

/// <summary>
/// Covers the generated holiday calendar: the count, the movable dates, and the merge rule for
/// observances that land on the same day.
/// </summary>
public class HolidayGeneratorTests
{
    [Fact]
    public void ForYear_ProducesTheFourteenSeychellesHolidays()
    {
        HolidayGenerator.ForYear(2026).Count.ShouldBe(14);
    }

    [Fact]
    public void ForYear_PlacesTheMovableHolidaysRelativeToEaster()
    {
        var easter = EasterCalculator.EasterSunday(2026);
        var holidays = HolidayGenerator.ForYear(2026).ToDictionary(h => h.Name, h => h.Date);

        holidays["Good Friday"].ShouldBe(easter.AddDays(-2));
        holidays["Easter Saturday"].ShouldBe(easter.AddDays(-1));
        holidays["Easter Monday"].ShouldBe(easter.AddDays(1));
        holidays["Corpus Christi"].ShouldBe(easter.AddDays(60));
    }

    [Fact]
    public void ForYear_PlacesTheFixedHolidaysOnTheirOwnDates()
    {
        var holidays = HolidayGenerator.ForYear(2026).ToDictionary(h => h.Name, h => h.Date);

        holidays["New Year's Day"].ShouldBe(new DateOnly(2026, 1, 1));
        holidays["Liberation Day"].ShouldBe(new DateOnly(2026, 6, 5));
        holidays["National Day"].ShouldBe(new DateOnly(2026, 6, 18));
        holidays["Christmas Day"].ShouldBe(new DateOnly(2026, 12, 25));
    }

    [Fact]
    public void ForYear_ReturnsOneEntryPerDate()
    {
        // The unique index on PublicHoliday.Date makes a duplicate a runtime failure, so the
        // generator has to merge coinciding observances rather than emit both.
        for (var year = 2020; year <= 2100; year++)
        {
            var holidays = HolidayGenerator.ForYear(year);

            holidays.Select(h => h.Date).Distinct().Count().ShouldBe(holidays.Count);
        }
    }

    [Fact]
    public void ForYear_MergesCoincidingObservancesIntoOneNamedEntry()
    {
        // Corpus Christi is Easter + 60, which lands on Liberation Day (5 June) in 2140.
        var collisionYear = Enumerable.Range(2020, 200)
            .First(year => EasterCalculator.EasterSunday(year).AddDays(60) == new DateOnly(year, 6, 5));

        var merged = HolidayGenerator.ForYear(collisionYear)
            .Single(h => h.Date == new DateOnly(collisionYear, 6, 5));

        merged.Name.ShouldContain("Liberation Day");
        merged.Name.ShouldContain("Corpus Christi");

        // The Easter-relative rule survives the merge, because it is the one that has to be
        // recomputed for the following year.
        merged.Rule.ShouldBe(HolidayRule.EasterRelative);
        merged.EasterOffset.ShouldBe(60);
    }

    [Fact]
    public void ForYear_ReturnsHolidaysInDateOrder()
    {
        var dates = HolidayGenerator.ForYear(2027).Select(h => h.Date).ToList();

        dates.ShouldBe(dates.Order().ToList());
    }
}
