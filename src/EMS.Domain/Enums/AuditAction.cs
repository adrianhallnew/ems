namespace EMS.Domain.Enums;

/// <summary>
/// Kind of change recorded in the audit trail.
/// </summary>
public enum AuditAction
{
    /// <summary>A record was created.</summary>
    Created,

    /// <summary>Field values changed on an existing record.</summary>
    Updated,

    /// <summary>A record was soft deleted.</summary>
    Deleted,

    /// <summary>A lifecycle status changed, such as deactivation or reactivation.</summary>
    StatusChanged,

    /// <summary>An authentication or account-recovery event with no tracked entity change.</summary>
    SecurityEvent,
}
