using System.ComponentModel.DataAnnotations;
using EMS.Domain.Enums;

namespace EMS.Application.Common.Options;

/// <summary>
/// Strongly-typed application configuration, bound from the <c>AppSettings</c> section.
/// </summary>
/// <remarks>
/// Validated at startup so a malformed value fails immediately rather than at first use.
/// <para>
/// The nested settings are validated here rather than by the framework:
/// <c>ValidateDataAnnotations()</c> checks the annotations on this type and does not recurse into
/// complex properties, so a bad <c>Lockout</c> or <c>SeedData</c> value would otherwise start the
/// application and fail at first use.
/// </para>
/// </remarks>
public sealed class AppSettings : IValidatableObject
{
    /// <summary>The configuration section this type binds to.</summary>
    public const string SectionName = "AppSettings";

    /// <summary>
    /// Gets or sets the fixed offset from UTC for Seychelles Time.
    /// </summary>
    /// <remarks>
    /// Seychelles observes no daylight saving, so a fixed offset is correct in perpetuity. It is
    /// still configured in one place so the assumption has exactly one home.
    /// </remarks>
    [Range(-12, 14)]
    public int TimeZoneOffsetHours { get; set; } = 4;

    /// <summary>Gets or sets the hour the working day starts, in SCT.</summary>
    [Range(0, 23)]
    public int WorkDayStartHour { get; set; } = 8;

    /// <summary>Gets or sets the hour the working day ends, in SCT.</summary>
    [Range(0, 23)]
    public int WorkDayEndHour { get; set; } = 16;

    /// <summary>Gets or sets the sliding session lifetime in minutes.</summary>
    [Range(1, 1440)]
    public int SessionTimeoutMinutes { get; set; } = 30;

    /// <summary>Gets or sets how often the security stamp is revalidated on a live connection.</summary>
    [Range(1, 1440)]
    public int SecurityStampRevalidationMinutes { get; set; } = 30;

    /// <summary>Gets or sets how long notifications are kept before the purge job deletes them.</summary>
    [Range(1, 3650)]
    public int NotificationRetentionDays { get; set; } = 30;

    /// <summary>Gets or sets the probation period length in months.</summary>
    [Range(0, 24)]
    public int ProbationMonths { get; set; } = 3;

    /// <summary>Gets or sets the hard ceiling on a requested page size.</summary>
    [Range(1, 1000)]
    public int MaxPageSize { get; set; } = 100;

    /// <summary>Gets the default annual entitlement in days for each leave type.</summary>
    public Dictionary<LeaveType, int> DefaultLeaveEntitlements { get; } = [];

    /// <summary>Gets the account lockout settings.</summary>
    public LockoutSettings Lockout { get; } = new();

    /// <summary>Gets the login rate limiting settings.</summary>
    public RateLimitSettings RateLimit { get; } = new();

    /// <summary>Gets the development seeding settings.</summary>
    public SeedDataSettings SeedData { get; } = new();

    /// <summary>Validates the nested settings objects.</summary>
    /// <param name="validationContext">Unused; the nested objects carry their own context.</param>
    /// <returns>One result per broken rule, across every nested object.</returns>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        ValidateNested(Lockout, nameof(Lockout))
            .Concat(ValidateNested(RateLimit, nameof(RateLimit)))
            .Concat(ValidateNested(SeedData, nameof(SeedData)));

    /// <summary>Runs data annotations over one nested settings object.</summary>
    /// <param name="instance">The nested object.</param>
    /// <param name="sectionName">Its property name, used to qualify the message.</param>
    /// <returns>One result per broken rule.</returns>
    private static IEnumerable<ValidationResult> ValidateNested(object instance, string sectionName)
    {
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(
            instance,
            new ValidationContext(instance),
            results,
            validateAllProperties: true);

        return results.Select(result => new ValidationResult(
            $"{sectionName}: {result.ErrorMessage}",
            result.MemberNames.Select(member => $"{sectionName}.{member}")));
    }
}

/// <summary>Account lockout settings (spec section 3.1.5).</summary>
public sealed class LockoutSettings
{
    /// <summary>Gets or sets how many consecutive failures lock an account.</summary>
    [Range(1, 100)]
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary>Gets or sets how long a lockout lasts.</summary>
    [Range(1, 1440)]
    public int LockoutDurationMinutes { get; set; } = 15;
}

/// <summary>Login rate limiting settings, which are independent of lockout.</summary>
public sealed class RateLimitSettings
{
    /// <summary>Gets or sets the permitted login attempts per minute per partition.</summary>
    [Range(1, 1000)]
    public int LoginAttemptsPerMinute { get; set; } = 10;
}

/// <summary>Development seeding settings (spec section 5).</summary>
public sealed class SeedDataSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether seeding runs at startup. Off by default.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets how many employees the fake dataset contains.</summary>
    [Range(0, 1000)]
    public int EmployeeCount { get; set; } = 15;

    /// <summary>Gets or sets how many days of attendance history are generated.</summary>
    [Range(0, 3650)]
    public int AttendanceHistoryDays { get; set; } = 30;

    /// <summary>
    /// Gets or sets the Bogus randomizer seed, which makes the generated dataset reproducible
    /// across runs and machines.
    /// </summary>
    public int RandomizerSeed { get; set; } = 20260812;
}
