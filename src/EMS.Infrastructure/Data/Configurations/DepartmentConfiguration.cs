using EMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EMS.Infrastructure.Data.Configurations;

/// <summary>Maps <see cref="Department"/>.</summary>
public sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.Property(d => d.Name).HasMaxLength(100).IsRequired();
        builder.Property(d => d.Description).HasMaxLength(500);

        builder.HasIndex(d => d.Name).IsUnique();

        // The manager is an employee, not necessarily one of this department's own (spec 3.2.2).
        // Deactivating a manager clears the assignment rather than blocking the operation, which
        // the service does explicitly; ClientSetNull keeps the database out of a cascade cycle
        // with Employees.DepartmentId (SQL Server error 1785).
        builder.HasOne<Employee>()
               .WithMany()
               .HasForeignKey(d => d.ManagerId)
               .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
