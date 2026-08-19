using EMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EMS.Infrastructure.Data.Configurations;

/// <summary>Maps <see cref="LeaveRequest"/>.</summary>
public sealed class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<LeaveRequest> builder)
    {
        builder.Property(r => r.Reason).HasMaxLength(500);
        builder.Property(r => r.ReviewNote).HasMaxLength(500);

        builder.Property(r => r.LeaveType).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);

        // Maintained by SQL Server on every update, so nothing depends on the application
        // remembering to increment it. See ADR-0009 and architecture.md 4.7.
        builder.Property(r => r.RowVersion).IsRowVersion();

        builder.HasIndex(r => new { r.EmployeeId, r.Status, r.StartDate });
        builder.HasIndex(r => new { r.Status, r.CreatedAt });

        builder.HasOne(r => r.Employee)
               .WithMany()
               .HasForeignKey(r => r.EmployeeId)
               .OnDelete(DeleteBehavior.Restrict);

        // ClientSetNull, not SetNull: three foreign keys from this table to Employees give SQL
        // Server multiple cascade paths (error 1785), and it counts SET NULL as a cascade action.
        // The distinction is theoretical here -- employees are soft-deleted and never removed, so
        // no delete action on Employees ever fires. See architecture.md 2.3.
        builder.HasOne<Employee>()
               .WithMany()
               .HasForeignKey(r => r.ReviewedById)
               .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne<Employee>()
               .WithMany()
               .HasForeignKey(r => r.CancelledById)
               .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
