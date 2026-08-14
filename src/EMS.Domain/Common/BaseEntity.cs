namespace EMS.Domain.Common;

/// <summary>
/// Base class for every entity with a surrogate key.
/// </summary>
/// <remarks>
/// Identifiers are version-7 GUIDs, which are time-ordered. Random GUIDs as primary keys
/// fragment the clustered index on every insert; version-7 values append.
/// </remarks>
public abstract class BaseEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();
}
