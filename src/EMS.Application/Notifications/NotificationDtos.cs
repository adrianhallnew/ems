using EMS.Application.Common.Models;

namespace EMS.Application.Notifications;

/// <summary>One in-app notification.</summary>
/// <param name="Id">The notification key.</param>
/// <param name="Title">The headline.</param>
/// <param name="Message">The body.</param>
/// <param name="IsRead">Whether the recipient has opened it.</param>
/// <param name="NavigationUrl">Where clicking it goes, if anywhere.</param>
/// <param name="CreatedAt">When it was raised, in UTC.</param>
public sealed record NotificationDto(
    Guid Id,
    string Title,
    string Message,
    bool IsRead,
    string? NavigationUrl,
    DateTime CreatedAt);

/// <summary>The filter behind the notification list.</summary>
public sealed record NotificationFilter : PageRequest
{
    /// <summary>Gets a value indicating whether only unread notifications are returned.</summary>
    public bool UnreadOnly { get; init; }
}
