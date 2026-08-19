using EMS.Application.Common.Options;
using Microsoft.Extensions.Options;

namespace EMS.Application.Common.Time;

/// <summary>
/// The single source of "now" and "today", in Seychelles Time.
/// </summary>
/// <param name="time">The platform clock. <c>FakeTimeProvider</c> substitutes for it in tests.</param>
/// <param name="settings">Supplies the fixed offset from UTC.</param>
/// <remarks>
/// Every value is a <see cref="DateTimeOffset"/>, so nothing carries an ambiguous
/// <see cref="DateTimeKind"/>. Seychelles observes no daylight saving, so a fixed offset is correct
/// in perpetuity; centralising it here gives that assumption exactly one home. Server local time is
/// never read — see ADR-0008.
/// </remarks>
public sealed class SctClock(TimeProvider time, IOptions<AppSettings> settings)
{
    private readonly TimeSpan _offset = TimeSpan.FromHours(settings.Value.TimeZoneOffsetHours);

    /// <summary>Gets the current instant in UTC.</summary>
    public DateTimeOffset UtcNow => time.GetUtcNow();

    /// <summary>Gets the current instant expressed at the Seychelles offset.</summary>
    public DateTimeOffset Now => time.GetUtcNow().ToOffset(_offset);

    /// <summary>Gets the current Seychelles calendar date.</summary>
    /// <remarks>
    /// The only source of "today" in the application, which is what makes the day-boundary rule in
    /// spec section 3.3.3 enforceable rather than aspirational.
    /// </remarks>
    public DateOnly Today => DateOnly.FromDateTime(Now.DateTime);

    /// <summary>
    /// Returns the Seychelles calendar date a UTC instant falls on.
    /// </summary>
    /// <param name="utcInstant">The instant, in UTC.</param>
    /// <returns>The SCT calendar date.</returns>
    /// <remarks>
    /// Taking the UTC date instead assigns every instant between 20:00 and 24:00 UTC to the
    /// previous SCT day, which is the exact bug this method exists to prevent.
    /// </remarks>
    public DateOnly DateOf(DateTime utcInstant) =>
        DateOnly.FromDateTime(new DateTimeOffset(utcInstant, TimeSpan.Zero).ToOffset(_offset).DateTime);

    /// <summary>
    /// Returns the Seychelles calendar date an instant falls on.
    /// </summary>
    /// <param name="instant">The instant, in any offset.</param>
    /// <returns>The SCT calendar date.</returns>
    public DateOnly DateOf(DateTimeOffset instant) =>
        DateOnly.FromDateTime(instant.ToOffset(_offset).DateTime);

    /// <summary>
    /// Returns the Seychelles wall-clock time an instant falls on.
    /// </summary>
    /// <param name="utcInstant">The instant, in UTC.</param>
    /// <returns>The SCT time of day, used for the late-arrival comparison.</returns>
    public TimeOnly TimeOf(DateTime utcInstant) =>
        TimeOnly.FromDateTime(new DateTimeOffset(utcInstant, TimeSpan.Zero).ToOffset(_offset).DateTime);
}
