using EMS.Domain.Entities;
using EMS.Domain.Enums;
using EMS.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EMS.Infrastructure.Data.Configurations;

/// <summary>Maps <see cref="Employee"/>.</summary>
public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        // 450 matches the Identity key column this points at.
        builder.Property(e => e.UserId).HasMaxLength(450).IsRequired();
        builder.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.LastName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Phone).HasMaxLength(30).IsRequired();
        builder.Property(e => e.Address).HasMaxLength(500).IsRequired();
        builder.Property(e => e.EmergencyContactName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.EmergencyContactPhone).HasMaxLength(30).IsRequired();
        builder.Property(e => e.JobTitle).HasMaxLength(100).IsRequired();

        // Provider-neutral form of decimal(18,2). See ADR-0010.
        builder.Property(e => e.Salary).HasPrecision(18, 2);

        // Enums are stored as strings: integer storage makes the table unreadable and breaks
        // silently if anyone reorders an enum. The length keeps the column indexable.
        builder.Property(e => e.ContractType).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Role).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(e => e.UserId).IsUnique();
        builder.HasIndex(e => e.DepartmentId);
        builder.HasIndex(e => e.Status);

        // Spec 3.1.2: no two employees may share an email, active or inactive.
        builder.HasIndex(e => e.Email).IsUnique();

        // No navigation to the Identity user: Employee lives in Domain, which has no reference to
        // Identity. The relationship is configured from this side alone.
        builder.HasOne<ApplicationUser>()
               .WithOne()
               .HasForeignKey<Employee>(e => e.UserId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Department)
               .WithMany()
               .HasForeignKey(e => e.DepartmentId)
               .OnDelete(DeleteBehavior.Restrict);

        // Soft delete is the default. Reports, audit history, and department deletion checks opt
        // out explicitly with IgnoreQueryFilters(); everything else gets the safe behaviour.
        builder.HasQueryFilter(e => e.Status == EmployeeStatus.Active);
    }
}
