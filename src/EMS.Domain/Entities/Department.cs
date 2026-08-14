using EMS.Domain.Common;

namespace EMS.Domain.Entities;

/// <summary>
/// An organisational unit. Reporting structure is department-level only — there is no
/// per-employee manager foreign key.
/// </summary>
public class Department : BaseEntity, ICreatedAtEntity
{
    /// <summary>Gets or sets the department name. Unique across the organisation.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets an optional description of the department's function.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the assigned manager, or null when the department has none.
    /// </summary>
    /// <remarks>
    /// Left as a bare foreign key: it points at Employee, which also points back here, and a
    /// navigation on both ends would need explicit disambiguation for no gain. Deactivating
    /// the assigned manager sets this to null and notifies every Admin.
    /// </remarks>
    public Guid? ManagerId { get; set; }

    /// <inheritdoc/>
    public DateTime CreatedAt { get; set; }
}
