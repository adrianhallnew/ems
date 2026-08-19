namespace EMS.Application.Holidays;

/// <summary>Computes Easter Sunday.</summary>
/// <remarks>
/// The anonymous Gregorian computus. Pure arithmetic with no dependencies, which is what lets the
/// four movable Seychelles holidays be generated rather than hand-entered every year.
/// </remarks>
public static class EasterCalculator
{
    /// <summary>Returns the date of Easter Sunday in a given year.</summary>
    /// <param name="year">The Gregorian year.</param>
    /// <returns>Easter Sunday, which always falls between 22 March and 25 April.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The year is outside 1583-9999.</exception>
    public static DateOnly EasterSunday(int year)
    {
        // The Gregorian calendar began in 1582; the algorithm is undefined before it.
        ArgumentOutOfRangeException.ThrowIfLessThan(year, 1583);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, 9999);

        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = ((19 * a) + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + (2 * e) + (2 * i) - h - k) % 7;
        var m = (a + (11 * h) + (22 * l)) / 451;
        var month = (h + l - (7 * m) + 114) / 31;
        var day = ((h + l - (7 * m) + 114) % 31) + 1;

        return new DateOnly(year, month, day);
    }
}
