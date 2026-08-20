using EMS.Application.Common.Interfaces;
using EMS.Application.Common.Models;
using EMS.Application.Common.Options;
using EMS.Application.Common.Security;
using EMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EMS.Application.Audit;

/// <summary>Reads the audit trail. Admin only, and read-only (spec §3.8.4).</summary>
/// <param name="factory">Creates one short-lived context per operation.</param>
/// <param name="settings">Supplies the page size ceiling.</param>
/// <remarks>
/// There is no write method here by design. Entries come from the SaveChanges interceptor; the one
/// exception, security events that correspond to no tracked entity change, has its own narrow port
/// in <see cref="ISecurityEventWriter"/>.
/// </remarks>
public sealed class AuditQueryService(
    IApplicationDbContextFactory factory,
    IOptions<AppSettings> settings)
    : IAuditQueryService
{
    /// <inheritdoc/>
    public async Task<PagedResult<AuditEntryDto>> GetAsync(AuditFilter filter, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filter);

        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        var (page, pageSize) = filter.Clamp(settings.Value.MaxPageSize);

        var query = db.AuditEntries.AsNoTracking();

        if (filter.From is { } from)
        {
            query = query.Where(a => a.ChangedAt >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(a => a.ChangedAt <= to);
        }

        if (!string.IsNullOrWhiteSpace(filter.EntityType))
        {
            query = query.Where(a => a.EntityType == filter.EntityType);
        }

        if (!string.IsNullOrWhiteSpace(filter.EntityId))
        {
            query = query.Where(a => a.EntityId == filter.EntityId);
        }

        if (filter.Action is { } action)
        {
            query = query.Where(a => a.Action == action);
        }

        var total = await query.CountAsync(ct).ConfigureAwait(false);

        var items = await Project(query.ApplySort(filter.SortBy, filter.SortDescending))
            .Skip(PageRequestExtensions.SkipFor(page, pageSize))
            .Take(pageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new PagedResult<AuditEntryDto>(items, total, page, pageSize);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AuditEntryDto>> GetForEntityAsync(
        string entityType,
        string entityId,
        CancellationToken ct)
    {
        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        return await Project(db.AuditEntries
                .AsNoTracking()
                .Where(a => a.EntityType == entityType && a.EntityId == entityId)
                .OrderByDescending(a => a.ChangedAt))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    private static IQueryable<AuditEntryDto> Project(IQueryable<AuditEntry> query) =>
        query.Select(a => new AuditEntryDto(
            a.Id,
            a.EntityType,
            a.EntityId,
            a.Action,
            a.ChangedFields,
            a.ActorDescription,
            a.ChangedAt));
}
