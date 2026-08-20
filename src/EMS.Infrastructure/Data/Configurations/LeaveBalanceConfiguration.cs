using EMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EMS.Infrastructure.Data.Configurations;

/// <summary>Maps <see cref="LeaveBalance"/>.</summary>
public sealed class LeaveBalanceConfiguration : IEntityTypeConfiguration<LeaveBalance>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<LeaveBalance> builder)
    {
        builder.Property(b => b.LeaveType).HasConversion<string>().HasMaxLength(20);
        builder.Property(b => b.LastAdjustmentNote).HasMaxLength(500);
        builder.Property(b => b.RowVersion).IsRowVersion();

        // Derived from Entitlement - Used, so it is a read-time projection rather than a column.
        builder.Ignore(b => b.Remaining);

        // Also the idempotence guard for lazy period materialisation (ADR-0006).
        builder.HasIndex(b => new { b.EmployeeId, b.LeaveType, b.PeriodStart }).IsUnique();

        builder.HasOne(b => b.Employee)
               .WithMany()
               .HasForeignKey(b => b.EmployeeId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
