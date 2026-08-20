using EMS.Application.Common.Interfaces;
using EMS.Application.Notifications;
using EMS.Application.Reports;
using EMS.Infrastructure.Data;
using EMS.Infrastructure.Data.Interceptors;
using EMS.Infrastructure.Identity;
using EMS.Infrastructure.Jobs;
using EMS.Infrastructure.Notifications;
using EMS.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using QuestPDF.Infrastructure;

namespace EMS.Infrastructure;

/// <summary>
/// Registers the infrastructure layer: the database context, its interceptors, and the clock.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds the infrastructure services to <paramref name="services"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The SQL Server connection string.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.TryAddSingleton(TimeProvider.System);

        // Depends only on the clock, so it is safe as a singleton.
        services.AddSingleton<AuditableEntityInterceptor>();

        // Phase 4 replaces SystemCurrentUser with the claims-backed implementation. The audit
        // interceptor is scoped because it reads the acting user.
        services.AddScoped<ICurrentUser, SystemCurrentUser>();
        services.AddScoped<AuditSaveChangesInterceptor>();

        // A factory, not AddDbContext: in Blazor Server a scoped service lives for the whole
        // circuit, so a scoped context would accumulate tracked entities, serve stale reads, and
        // hold a pooled connection open for hours. Services create one context per operation.
        //
        // The scoped factory lifetime is required. The default is singleton, which resolves its
        // dependencies from the root provider and therefore cannot see the scoped ICurrentUser the
        // audit interceptor needs -- including at design time, where it stops `dotnet ef` from
        // finding the context at all.
        services.AddDbContextFactory<ApplicationDbContext>(
            (sp, options) => options
                .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure())

                // Employee carries the soft-delete filter and its dependents deliberately do not:
                // attendance, leave, and notification history for a departed employee must stay
                // readable by reports, the audit log, and attendance state resolution. The cost is
                // that a query joining through a required Employee navigation drops rows for
                // inactive employees, so those queries call IgnoreQueryFilters() explicitly. The
                // warning describes that asymmetry, which is the intended design.
                .ConfigureWarnings(w => w.Ignore(
                    CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning))
                // Order is load-bearing. Interceptors run in registration order, and the audit
                // interceptor serialises the entity as it finds it: register it first and every
                // Created audit row records CreatedAt and UpdatedAt as 0001-01-01, because the
                // stamping has not happened yet.
                .AddInterceptors(
                    sp.GetRequiredService<AuditableEntityInterceptor>(),
                    sp.GetRequiredService<AuditSaveChangesInterceptor>()),
            lifetime: ServiceLifetime.Scoped);

        // Identity's entity framework stores resolve the context directly. The account pages are
        // static SSR, so this instance lives for a request rather than for a circuit.
        services.AddScoped(sp =>
            sp.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext());

        services.AddScoped<IApplicationDbContext>(sp =>
            sp.GetRequiredService<ApplicationDbContext>());

        // The port the application layer creates its own short-lived contexts through.
        services.AddScoped<IApplicationDbContextFactory, ApplicationDbContextFactory>();

        // Lets the application treat a unique index violation as an outcome without naming a
        // provider type. See ADR-0015.
        services.AddSingleton<IDatabaseErrorClassifier, SqlServerErrorClassifier>();

        // The adapter over UserManager. Employee creation needs it; Phase 6 adds the pages that use
        // the rest of its surface.
        services.AddScoped<IIdentityAccountService, IdentityAccountService>();

        // A singleton on purpose: a subscription has to outlive the scope that raised the
        // notification, and it holds weak references so a dropped circuit cannot leak
        // (architecture.md §4.9).
        services.AddSingleton<INotificationPublisher, NotificationPublisher>();

        services.AddScoped<IReportRenderer, ReportRenderer>();

        // Hosted services are singletons, so each pass opens its own scope for a context.
        services.AddHostedService<MissedClockOutJob>();
        services.AddHostedService<NotificationPurgeJob>();

        // A process-wide static that QuestPDF requires to be set before the first document is
        // created. Composition is the one place guaranteed to run first.
        QuestPDF.Settings.License = LicenseType.Community;

        return services;
    }
}
