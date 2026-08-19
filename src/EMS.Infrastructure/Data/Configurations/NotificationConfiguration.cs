using EMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EMS.Infrastructure.Data.Configurations;

/// <summary>Maps <see cref="Notification"/>.</summary>
public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Message).HasMaxLength(1000).IsRequired();
        builder.Property(n => n.NavigationUrl).HasMaxLength(500);

        builder.HasIndex(n => new { n.RecipientId, n.IsRead });

        // Drives the 30-day purge job's range delete.
        builder.HasIndex(n => n.CreatedAt);

        builder.HasOne(n => n.Recipient)
               .WithMany()
               .HasForeignKey(n => n.RecipientId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
