using EMS.Application.Common.Options;
using EMS.Application.Common.Time;
using EMS.Application.Holidays;
using EMS.Infrastructure.Data;
using EMS.Infrastructure.Data.Seed;
using EMS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EMS.Web;

/// <summary>
/// Brings the database up to date at startup, and optionally seeds it.
/// </summary>
public static class DatabaseInitialization
{
    /// <summary>The configuration key holding the seeded accounts' temporary password.</summary>
    private const string AdminPasswordKey = "Seed:AdminPassword";

    /// <summary>
    /// Generates public holidays for the current and next year, whether or not seeding is enabled.
    /// </summary>
    /// <param name="services">The startup scope.</param>
    /// <param name="logger">Records what was generated.</param>
    /// <param name="ct">Cancels with application shutdown.</param>
    /// <remarks>
    /// Spec §3.7.4 requires generation at startup for both years unconditionally. It sits above the
    /// seeding switch deliberately: holidays are not sample data, and without them every leave
    /// request over Easter counts the wrong number of business days.
    /// </remarks>
    private static async Task GenerateHolidaysAsync(
        IServiceProvider services,
        ILogger logger,
        CancellationToken ct)
    {
        var holidays = services.GetRequiredService<IHolidayService>();
        var clock = services.GetRequiredService<SctClock>();
        var year = clock.Today.Year;

        var created = await holidays.EnsureGeneratedAsync(year, ct).ConfigureAwait(false);
        created += await holidays.EnsureGeneratedAsync(year + 1, ct).ConfigureAwait(false);

        if (created > 0)
        {
            logger.LogInformation(
                "Generated {Count} public holiday(s) for {Year} and {NextYear}.",
                created,
                year,
                year + 1);
        }
    }

    /// <summary>
    /// Applies pending migrations, then seeds when seeding is enabled.
    /// </summary>
    /// <param name="app">The built application.</param>
    /// <returns>A task that completes when the database is ready.</returns>
    /// <remarks>
    /// Migrating here is what makes a fresh LocalDB instance or a fresh container volume work on
    /// first run: EF Core issues the CREATE DATABASE and then the schema. Nothing is provisioned
    /// by hand.
    /// </remarks>
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitialization));

        var db = services.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync(app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        await GenerateHolidaysAsync(services, logger, app.Lifetime.ApplicationStopping)
            .ConfigureAwait(false);

        var settings = services.GetRequiredService<IOptions<AppSettings>>().Value.SeedData;

        if (!settings.Enabled)
        {
            return;
        }

        var password = ResolveSeedPassword(app, logger);

        await DatabaseSeeder.SeedAsync(
            db,
            services.GetRequiredService<UserManager<ApplicationUser>>(),
            services.GetRequiredService<RoleManager<IdentityRole>>(),
            settings,
            password,
            logger,
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);
    }

    private static string ResolveSeedPassword(WebApplication app, ILogger logger)
    {
        var configured = app.Configuration[AdminPasswordKey];

        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        if (!app.Environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                $"Seeding is enabled but '{AdminPasswordKey}' is not configured. Supply it through " +
                "an environment variable or user-secrets; there is no default outside Development.");
        }

        // Development convenience: a generated password, written to the log exactly once so the
        // developer can sign in. Nothing is persisted to configuration.
        var generated = $"Ems!{Guid.NewGuid():N}";

        logger.LogWarning(
            "No {Key} configured. Seeded accounts use the generated password {Password}. " +
            "Set the value through user-secrets to keep it stable across runs.",
            AdminPasswordKey,
            generated);

        return generated;
    }
}
