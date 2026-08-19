using EMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EMS.Infrastructure.Data.Configurations;

/// <summary>Maps <see cref="JobRun"/>.</summary>
public sealed class JobRunConfiguration : IEntityTypeConfiguration<JobRun>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<JobRun> builder)
    {
        // The only entity keyed by something other than a surrogate GUID: one row per job name.
        builder.HasKey(j => j.JobName);

        builder.Property(j => j.JobName).HasMaxLength(100);
        builder.Property(j => j.LastResult).HasMaxLength(500).IsRequired();
    }
}
