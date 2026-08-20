using EMS.Application.Attendance;
using EMS.Application.Audit;
using EMS.Application.Common.Interfaces;
using EMS.Application.Common.Time;
using EMS.Application.Departments;
using EMS.Application.Employees;
using EMS.Application.Holidays;
using EMS.Application.Leave;
using EMS.Application.Notifications;
using EMS.Application.Reports;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace EMS.Application;

/// <summary>Registers the application layer.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds the clock, the calculators, the business services, and every validator in this assembly.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    /// <remarks>
    /// The services live in this layer (architecture.md §1, §5.1), so this is where they are
    /// registered. Infrastructure supplies only the ports they depend on — the context factory, the
    /// error classifier, the notification publisher, and the Identity account adapter.
    /// <para>
    /// Everything is scoped. Each service creates its own short-lived context per operation, so
    /// nothing here holds state between calls (ADR-0013).
    /// </para>
    /// </remarks>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<SctClock>();
        services.AddScoped<IBusinessDayCalculator, BusinessDayCalculator>();
        services.AddScoped<IAttendanceStateResolver, AttendanceStateResolver>();

        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IHolidayService, HolidayService>();
        services.AddScoped<IAttendanceService, AttendanceService>();
        services.AddScoped<ILeaveService, LeaveService>();
        services.AddScoped<ILeaveBalanceService, LeaveBalanceService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IReportDataService, ReportDataService>();
        services.AddScoped<IAuditQueryService, AuditQueryService>();
        services.AddScoped<ISecurityEventWriter, SecurityEventWriter>();

        services.AddValidatorsFromAssemblyContaining<SubmitLeaveValidator>(ServiceLifetime.Scoped);

        return services;
    }
}
