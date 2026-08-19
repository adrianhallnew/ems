using EMS.Domain.Enums;

namespace EMS.Application.Holidays;

/// <summary>Produces the Seychelles public holidays for a calendar year.</summary>
/// <remarks>
/// Ten fixed dates and four computed from Easter (spec section 3.7.2). Generation is pure: the
/// service decides what to persist, and never overwrites an entry an Admin has edited.
/// </remarks>
public static class HolidayGenerator
{
    /// <summary>The fixed-date holidays, as month and day.</summary>
    private static readonly (string Name, int Month, int Day)[] FixedHolidays =
    [
        ("New Year's Day", 1, 1),
        ("New Year Holiday", 1, 2),
        ("Labour Day", 5, 1),
        ("Liberation Day", 6, 5),
        ("National Day", 6, 18),
        ("Independence Day", 6, 29),
        ("Assumption Day", 8, 15),
        ("All Saints' Day", 11, 1),
        ("Immaculate Conception", 12, 8),
        ("Christmas Day", 12, 25),
    ];

    /// <summary>The movable holidays, as an offset in days from Easter Sunday.</summary>
    private static readonly (string Name, int Offset)[] EasterHolidays =
    [
        ("Good Friday", -2),
        ("Easter Saturday", -1),
        ("Easter Monday", 1),
        ("Corpus Christi", 60),
    ];

    /// <summary>Generates one year's holidays.</summary>
    /// <param name="year">The year to generate.</param>
    /// <returns>The holidays, ordered by date, with no two sharing a date.</returns>
    /// <remarks>
    /// Corpus Christi lands anywhere between late May and late June, so it can coincide with
    /// Liberation Day or National Day. Spec section 3.7.1 requires one entry per date, so
    /// coinciding observances are merged into a single entry carrying both names.
    /// </remarks>
    public static IReadOnlyList<GeneratedHoliday> ForYear(int year)
    {
        var easter = EasterCalculator.EasterSunday(year);

        var generated = FixedHolidays
            .Select(h => new GeneratedHoliday(
                h.Name,
                new DateOnly(year, h.Month, h.Day),
                HolidayRule.FixedDate,
                null))
            .Concat(EasterHolidays.Select(h => new GeneratedHoliday(
                h.Name,
                easter.AddDays(h.Offset),
                HolidayRule.EasterRelative,
                h.Offset)));

        return generated
            .GroupBy(h => h.Date)
            .Select(Merge)
            .OrderBy(h => h.Date)
            .ToList();
    }

    /// <summary>Collapses observances that fall on the same date into one entry.</summary>
    /// <param name="sameDate">The observances sharing a date.</param>
    /// <returns>The single entry for that date.</returns>
    private static GeneratedHoliday Merge(IGrouping<DateOnly, GeneratedHoliday> sameDate)
    {
        var first = sameDate.First();

        if (!sameDate.Skip(1).Any())
        {
            return first;
        }

        // The Easter-relative rule wins, because it is the one that has to be recomputed next year.
        var movable = sameDate.FirstOrDefault(h => h.Rule == HolidayRule.EasterRelative);

        return new GeneratedHoliday(
            string.Join(" / ", sameDate.Select(h => h.Name)),
            sameDate.Key,
            movable?.Rule ?? first.Rule,
            movable?.EasterOffset ?? first.EasterOffset);
    }
}
