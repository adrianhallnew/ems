using EMS.Application.Common.Models;

namespace EMS.Application.Audit;

/// <summary>Read-only access to the audit trail. Admin only.</summary>
/// <remarks>
/// There is no write method here on purpose: entries are produced by the SaveChanges interceptor
/// and by the Identity-adjacent services for security events. Nothing in the application edits or
/// deletes them (spec section 3.8.4).
/// </remarks>
public interface IAuditQueryService
{
    /// <summary>Lists audit entries.</summary>
    /// <param name="filter">Range, entity, action, paging and sorting.</param>
    /// <param name="ct">Cancels the query.</param>
    /// <returns>One page of entries.</returns>
    Task<PagedResult<AuditEntryDto>> GetAsync(AuditFilter filter, CancellationToken ct);

    /// <summary>Lists one record's history.</summary>
    /// <param name="entityType">The entity's type name.</param>
    /// <param name="entityId">The entity's key.</param>
    /// <param name="ct">Cancels the query.</param>
    /// <returns>The entries for that record, newest first.</returns>
    Task<IReadOnlyList<AuditEntryDto>> GetForEntityAsync(
        string entityType,
        string entityId,
        CancellationToken ct);

    /// <summary>Writes a security event, which corresponds to no tracked entity change.</summary>
    /// <param name="description">What happened, such as "Login failure" or "Admin unlock".</param>
    /// <param name="subjectEmail">The account the event concerns.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>The outcome.</returns>
    Task<Result> RecordSecurityEventAsync(
        string description,
        string subjectEmail,
        CancellationToken ct);
}
