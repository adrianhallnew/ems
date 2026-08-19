using EMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EMS.Infrastructure.Data.Configurations;

/// <summary>Maps <see cref="PublicHoliday"/>.</summary>
public sealed class PublicHolidayConfiguration : IEntityTypeConfiguration<PublicHoliday>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PublicHoliday> builder)
    {
        builder.Property(h => h.Name).HasMaxLength(100).IsRequired();
        builder.Property(h => h.Rule).HasConversion<string>().HasMaxLength(20);

        // Probed once per day of a range by the business-day calculator and the state resolver.
        builder.HasIndex(h => h.Date).IsUnique();
    }
}
