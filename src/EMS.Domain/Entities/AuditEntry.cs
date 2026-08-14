using EMS.Domain.Common;
using EMS.Domain.Enums;

namespace EMS.Domain.Entities;

/// <summary>
/// One immutable audit trail record.
/// </summary>
/// <remarks>
/// Written by a SaveChanges interceptor in the same transaction as the change it describes.
/// Entries are never modified or deleted through the application, and are never purged.
/// </remarks>
public class AuditEntry : BaseEntity
{
    /// <summary>Gets or sets the entity type affected, such as "Employee".</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Gets or sets the primary key of the affected record, as text.</summary>
    public string EntityId { get; set; } = string.Empty;

    /// <summary>Gets or sets the kind of change.</summary>
    public AuditAction Action { get; set; }

    /// <summary>
    /// Gets or sets the before and after values of each changed field, as JSON.
    /// </summary>
    /// <remarks>
    /// Password hashes, security stamps, and authentication tokens never reach this payload.
    /// </remarks>
    public string ChangedFields { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the acting employee, or null for a system actor.
    /// </summary>
    /// <remarks>
    /// Nullable because background jobs, the seeder, and startup migrations legitimately change
    /// data with no user present. A required actor column would make every one of them throw.
    /// </remarks>
    public Guid? ChangedById { get; set; }

    /// <summary>
    /// Gets or sets a description of the actor — the acting user's email, or a system label
    /// such as "System: NightlyAttendanceFlag".
    /// </summary>
    public string ActorDescription { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC instant of the change.</summary>
    public DateTime ChangedAt { get; set; }
}
