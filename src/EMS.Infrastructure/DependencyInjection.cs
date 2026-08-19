using EMS.Application.Common.Interfaces;
using EMS.Infrastructure.Data;
using EMS.Infrastructure.Data.Interceptors;
using EMS.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
                .AddInterceptors(
                    sp.GetRequiredService<AuditSaveChangesInterceptor>(),
                    sp.GetRequiredService<AuditableEntityInterceptor>()),
            lifetime: ServiceLifetime.Scoped);

        // Identity's entity framework stores resolve the context directly. The account pages are
        // static SSR, so this instance lives for a request rather than for a circuit.
        services.AddScoped(sp =>
            sp.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext());

        services.AddScoped<IApplicationDbContext>(sp =>
            sp.GetRequiredService<ApplicationDbContext>());

        return services;
    }
}
