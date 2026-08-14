namespace EMS.Domain.Enums;

/// <summary>
/// How a public holiday's date is determined for a given year.
/// </summary>
public enum HolidayRule
{
    /// <summary>Same calendar day every year, projected onto the target year.</summary>
    FixedDate,

    /// <summary>An offset in days from Easter Sunday, computed per year.</summary>
    EasterRelative,
}
