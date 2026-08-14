namespace EMS.Domain.Entities;

/// <summary>
/// The watermark and last outcome for one background job.
/// </summary>
/// <remarks>
/// Keyed by name rather than a surrogate identifier, so it does not derive from BaseEntity.
/// A job reads its watermark, processes every date from there to yesterday, and advances it
/// only on success — which is what makes the jobs correct on an application that is routinely
/// stopped. "Run once every 24 hours" is a schedule, not a correctness guarantee.
/// </remarks>
public class JobRun
{
    /// <summary>Gets or sets the job name. Primary key.</summary>
    public string JobName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the last SCT date this job processed successfully, or null before its
    /// first run.
    /// </summary>
    public DateOnly? LastProcessedDate { get; set; }

    /// <summary>Gets or sets the UTC instant of the last run, successful or not.</summary>
    public DateTime LastRunAt { get; set; }

    /// <summary>Gets or sets the outcome of the last run.</summary>
    public string LastResult { get; set; } = string.Empty;
}
