namespace EMS.Domain.Common;

/// <summary>
/// Marks an entity whose creation instant is stamped by the persistence layer.
/// </summary>
/// <remarks>
/// Department, LeaveRequest, and Notification record when they were created but are never
/// updated in place, so they carry this marker alone. The Phase 2 interceptor binds to this
/// interface, which is what keeps timestamps out of the services.
/// </remarks>
public interface ICreatedAtEntity
{
    /// <summary>Gets or sets the UTC instant the entity was created.</summary>
    DateTime CreatedAt { get; set; }
}
