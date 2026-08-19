using EMS.Application.Common.Models;
using EMS.Domain.Enums;

namespace EMS.Application.Audit;

/// <summary>One audit trail entry.</summary>
/// <param name="Id">The entry key.</param>
/// <param name="EntityType">The changed entity's type name.</param>
/// <param name="EntityId">The changed entity's key.</param>
/// <param name="Action">What happened.</param>
/// <param name="ChangedFields">The before and after payload, as JSON.</param>
/// <param name="ActorDescription">The acting user's email, or a system label.</param>
/// <param name="ChangedAt">When the change was written, in UTC.</param>
public sealed record AuditEntryDto(
    Guid Id,
    string EntityType,
    string EntityId,
    AuditAction Action,
    string ChangedFields,
    string ActorDescription,
    DateTime ChangedAt);

/// <summary>The filter behind the audit log. Admin only.</summary>
public sealed record AuditFilter : PageRequest
{
    /// <summary>Gets the earliest change instant to include, in UTC.</summary>
    public DateTime? From { get; init; }

    /// <summary>Gets the latest change instant to include, in UTC.</summary>
    public DateTime? To { get; init; }

    /// <summary>Gets the entity type to restrict to.</summary>
    public string? EntityType { get; init; }

    /// <summary>Gets the entity key to restrict to, for a single record's history.</summary>
    public string? EntityId { get; init; }

    /// <summary>Gets the action to restrict to.</summary>
    public AuditAction? Action { get; init; }
}
