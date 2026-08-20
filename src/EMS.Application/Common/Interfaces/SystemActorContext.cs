namespace EMS.Application.Common.Interfaces;

/// <summary>
/// Names the system process acting in this scope, when no user is.
/// </summary>
/// <remarks>
/// The audit trail needs a readable actor even where <see cref="ICurrentUser.EmployeeId"/> is null:
/// spec §3.8.1 gives <c>"System: NightlyAttendanceFlag"</c> as the shape. A bare <c>"System"</c>
/// leaves every background write indistinguishable from every other.
/// <para>
/// Scoped and mutable, because the name is known only once a job starts a pass. A job sets it on
/// the scope it creates; anything running under a real principal never touches it.
/// </para>
/// </remarks>
public sealed class SystemActorContext
{
    /// <summary>
    /// Gets or sets the acting process, such as <c>"MissedClockOut"</c>, or null outside one.
    /// </summary>
    public string? JobName { get; set; }

    /// <summary>Gets the actor label for the audit trail.</summary>
    /// <returns><c>"System: {JobName}"</c> when a job is running, otherwise <c>"System"</c>.</returns>
    public string Describe() => JobName is { Length: > 0 } name ? $"System: {name}" : "System";
}
