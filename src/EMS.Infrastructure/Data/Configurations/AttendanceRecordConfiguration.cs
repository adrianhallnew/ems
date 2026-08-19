using EMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EMS.Infrastructure.Data.Configurations;

/// <summary>Maps <see cref="AttendanceRecord"/>.</summary>
public sealed class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        builder.Property(a => a.CorrectionNote).HasMaxLength(500);

        // One record per employee per SCT date. This index is also the double-submit guard: the
        // clock-in service treats a violation of it as an ordinary "already clocked in" outcome.
        builder.HasIndex(a => new { a.EmployeeId, a.Date }).IsUnique();
        builder.HasIndex(a => new { a.Date, a.IsFlagged });

        builder.HasOne(a => a.Employee)
               .WithMany()
               .HasForeignKey(a => a.EmployeeId)
               .OnDelete(DeleteBehavior.Restrict);

        // ClientSetNull, not SetNull: two foreign keys from this table to Employees give SQL Server
        // multiple cascade paths (error 1785). Employees are soft-deleted and never removed, so no
        // delete action on Employees ever fires. See architecture.md 2.3.
        builder.HasOne<Employee>()
               .WithMany()
               .HasForeignKey(a => a.CorrectedById)
               .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
