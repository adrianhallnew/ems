using EMS.Domain.Enums;

namespace EMS.Application.Holidays;

/// <summary>A public holiday as shown on the calendar.</summary>
/// <param name="Id">The holiday key.</param>
/// <param name="Name">The holiday name.</param>
/// <param name="Date">The date it falls on.</param>
/// <param name="Rule">Whether the date is fixed or computed from Easter.</param>
/// <param name="EasterOffset">Days from Easter Sunday, when the rule is Easter-relative.</param>
/// <param name="IsSystemGenerated">False once an Admin has edited the entry.</param>
public sealed record PublicHolidayDto(
    Guid Id,
    string Name,
    DateOnly Date,
    HolidayRule Rule,
    int? EasterOffset,
    bool IsSystemGenerated);

/// <summary>A holiday the generator produced for a target year, before it is persisted.</summary>
/// <param name="Name">The holiday name.</param>
/// <param name="Date">The date in the target year.</param>
/// <param name="Rule">Whether the date is fixed or computed from Easter.</param>
/// <param name="EasterOffset">Days from Easter Sunday, when the rule is Easter-relative.</param>
public sealed record GeneratedHoliday(string Name, DateOnly Date, HolidayRule Rule, int? EasterOffset);

/// <summary>Adds a holiday to the calendar.</summary>
/// <param name="Name">The holiday name.</param>
/// <param name="Date">The date it falls on, which must not already be taken.</param>
public sealed record CreateHolidayCommand(string Name, DateOnly Date);

/// <summary>Edits a holiday.</summary>
/// <param name="HolidayId">The holiday to edit.</param>
/// <param name="Name">The holiday name.</param>
/// <param name="Date">The date it falls on.</param>
/// <remarks>
/// Editing marks the entry as no longer system-generated, so regeneration leaves it alone
/// (spec section 3.7.4).
/// </remarks>
public sealed record UpdateHolidayCommand(Guid HolidayId, string Name, DateOnly Date);
