using EMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EMS.Infrastructure.Data.Configurations;

/// <summary>Maps <see cref="AuditEntry"/>.</summary>
public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.Property(a => a.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityId).HasMaxLength(64).IsRequired();
        builder.Property(a => a.ActorDescription).HasMaxLength(256).IsRequired();
        builder.Property(a => a.Action).HasConversion<string>().HasMaxLength(20);

        // The one deliberate nvarchar(max) in the model: a JSON before/after payload of unbounded
        // shape. It is never filtered or indexed on.
        builder.Property(a => a.ChangedFields).IsRequired();

        builder.HasIndex(a => new { a.EntityType, a.EntityId });
        builder.HasIndex(a => a.ChangedAt);

        // Nullable actor: background jobs, the seeder, and startup migrations write with no user
        // present, and a required column would make every one of those writes throw.
        builder.HasOne<Employee>()
               .WithMany()
               .HasForeignKey(a => a.ChangedById)
               .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
