namespace EMS.Domain.Common;

/// <summary>
/// Marks an entity whose creation and modification instants are stamped by the persistence layer.
/// </summary>
/// <remarks>
/// Both values are set by an interceptor, never by hand in a service. Hand-set timestamps get
/// forgotten on exactly the code path nobody tested.
/// </remarks>
public interface IAuditableEntity : ICreatedAtEntity
{
    /// <summary>Gets or sets the UTC instant the entity was last updated.</summary>
    DateTime UpdatedAt { get; set; }
}
