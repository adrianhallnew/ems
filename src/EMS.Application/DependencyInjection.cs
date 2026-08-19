using EMS.Application.Attendance;
using EMS.Application.Common.Time;
using EMS.Application.Leave;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace EMS.Application;

/// <summary>Registers the application layer.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds the clock, the calculators, and every validator in this assembly.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    /// <remarks>
    /// The business services themselves are registered by Infrastructure once they exist; this
    /// phase ships their contracts and the pure logic behind them.
    /// </remarks>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<SctClock>();
        services.AddScoped<IBusinessDayCalculator, BusinessDayCalculator>();
        services.AddScoped<IAttendanceStateResolver, AttendanceStateResolver>();

        services.AddValidatorsFromAssemblyContaining<SubmitLeaveValidator>(ServiceLifetime.Scoped);

        return services;
    }
}
