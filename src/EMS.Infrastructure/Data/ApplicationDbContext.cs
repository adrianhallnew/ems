using EMS.Application.Common.Interfaces;
using EMS.Domain.Entities;
using EMS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EMS.Infrastructure.Data;

/// <summary>
/// The single database context, covering both the Identity schema and the domain schema.
/// </summary>
/// <param name="options">Provider and interceptor configuration, supplied by the context factory.</param>
/// <remarks>
/// One context type keeps Identity and domain writes in the same transaction, which is what lets an
/// email change update Identity and <see cref="Employee"/> atomically (spec section 3.1.6).
/// </remarks>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options), IApplicationDbContext
{
    /// <inheritdoc/>
    public DbSet<Employee> Employees => Set<Employee>();

    /// <inheritdoc/>
    public DbSet<Department> Departments => Set<Department>();

    /// <inheritdoc/>
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();

    /// <inheritdoc/>
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();

    /// <inheritdoc/>
    public DbSet<LeaveBalance> LeaveBalances => Set<LeaveBalance>();

    /// <inheritdoc/>
    public DbSet<PublicHoliday> PublicHolidays => Set<PublicHoliday>();

    /// <inheritdoc/>
    public DbSet<Notification> Notifications => Set<Notification>();

    /// <inheritdoc/>
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    /// <inheritdoc/>
    public DbSet<JobRun> JobRuns => Set<JobRun>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Identity's own configuration runs first; the scan below then adds the domain types.
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
