using EMS.Domain.Common;

namespace EMS.Domain.Entities;

/// <summary>
/// An in-app notification addressed to exactly one employee.
/// </summary>
/// <remarks>
/// There is no role-addressed notification. An event addressed to "all Admins" fans out to one
/// row per active Admin. Rows older than the retention period are purged by a background job.
/// </remarks>
public class Notification : BaseEntity, ICreatedAtEntity
{
    /// <summary>Gets or sets the employee this notification is addressed to.</summary>
    public Guid RecipientId { get; set; }

    /// <summary>Gets or sets the recipient navigation.</summary>
    public Employee? Recipient { get; set; }

    /// <summary>Gets or sets the short heading shown in the bell dropdown.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the notification body.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the recipient has read this.</summary>
    public bool IsRead { get; set; }

    /// <summary>Gets or sets the relative URL to open when the notification is clicked.</summary>
    public string? NavigationUrl { get; set; }

    /// <inheritdoc/>
    public DateTime CreatedAt { get; set; }
}
