using EMS.Domain.Common;
using EMS.Domain.Enums;

namespace EMS.Domain.Entities;

/// <summary>
/// A public holiday on a specific date.
/// </summary>
/// <remarks>
/// Holidays for a year are generated on demand and idempotently. Dates are unique across the
/// table: two observances that coincide are recorded as one entry with a combined name.
/// </remarks>
public class PublicHoliday : BaseEntity
{
    /// <summary>Gets or sets the holiday name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the date this holiday falls on. Unique.</summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Gets or sets how the date is determined for a year.
    /// </summary>
    /// <remarks>
    /// This also carries recurrence: a FixedDate holiday is projected onto each target year,
    /// and an EasterRelative one is recomputed from that year's Easter Sunday.
    /// </remarks>
    public HolidayRule Rule { get; set; }

    /// <summary>
    /// Gets or sets the offset in days from Easter Sunday, when <see cref="Rule"/> is
    /// EasterRelative. Null otherwise.
    /// </summary>
    public int? EasterOffset { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this entry came from generation rather than an
    /// Admin. Generation never overwrites an entry an Admin has edited.
    /// </summary>
    public bool IsSystemGenerated { get; set; }
}
