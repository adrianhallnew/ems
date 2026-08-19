using EMS.Application.Common.Models;

namespace EMS.Application.Audit;

/// <summary>Read-only access to the audit trail. Admin only.</summary>
/// <remarks>
/// There is no write method here on purpose: entries are produced by the SaveChanges interceptor,
/// and the one hand-written case has its own port in <see cref="ISecurityEventWriter"/>. Nothing in
/// the application edits or deletes an entry (spec section 3.8.4).
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
}

/// <summary>
/// Writes the audit entries that correspond to no tracked entity change.
/// </summary>
/// <remarks>
/// Login failure, lockout, password change, admin reset, admin unlock and role change all happen
/// inside Identity, where the SaveChanges interceptor sees nothing to audit. They are written here
/// instead, by the Identity-adjacent services (spec section 3.8.2).
/// <para>
/// Separate from <see cref="IAuditQueryService"/> because the audit log is read-only to the
/// application: entries cannot be edited or deleted, and only this one narrow path creates them
/// by hand.
/// </para>
/// </remarks>
public interface ISecurityEventWriter
{
    /// <summary>Writes a security event.</summary>
    /// <param name="description">What happened, such as "Login failure" or "Admin unlock".</param>
    /// <param name="subjectEmail">The account the event concerns.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>The outcome.</returns>
    Task<Result> RecordSecurityEventAsync(
        string description,
        string subjectEmail,
        CancellationToken ct);
}
