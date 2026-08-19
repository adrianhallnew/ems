using EMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace EMS.Application.Common.Interfaces;

/// <summary>
/// The persistence surface the application layer is allowed to see.
/// </summary>
/// <remarks>
/// Implemented by Infrastructure. There is no repository layer: <see cref="DbContext"/> is already
/// a unit of work over a set of repositories, and wrapping it blocks <c>Include</c>, projection,
/// and split queries for no benefit. See ADR-0003.
/// </remarks>
public interface IApplicationDbContext : IAsyncDisposable, IDisposable
{
    /// <summary>Gets the employee records.</summary>
    DbSet<Employee> Employees { get; }

    /// <summary>Gets the departments.</summary>
    DbSet<Department> Departments { get; }

    /// <summary>Gets the attendance records.</summary>
    DbSet<AttendanceRecord> AttendanceRecords { get; }

    /// <summary>Gets the leave requests.</summary>
    DbSet<LeaveRequest> LeaveRequests { get; }

    /// <summary>Gets the leave balance periods.</summary>
    DbSet<LeaveBalance> LeaveBalances { get; }

    /// <summary>Gets the public holidays.</summary>
    DbSet<PublicHoliday> PublicHolidays { get; }

    /// <summary>Gets the in-app notifications.</summary>
    DbSet<Notification> Notifications { get; }

    /// <summary>Gets the audit trail entries.</summary>
    DbSet<AuditEntry> AuditEntries { get; }

    /// <summary>Gets the background job watermarks.</summary>
    DbSet<JobRun> JobRuns { get; }

    /// <summary>Gets the facade over database-level operations, including transactions.</summary>
    DatabaseFacade Database { get; }

    /// <summary>Writes all pending changes.</summary>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>The number of state entries written.</returns>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
