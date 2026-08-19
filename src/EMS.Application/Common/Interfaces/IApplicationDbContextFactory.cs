namespace EMS.Application.Common.Interfaces;

/// <summary>
/// Creates a short-lived <see cref="IApplicationDbContext"/>, one per operation.
/// </summary>
/// <remarks>
/// EF Core's own <c>IDbContextFactory&lt;TContext&gt;</c> names a concrete context type, which the
/// application layer is not allowed to reference (ADR-0003), so the port is declared here and
/// implemented in Infrastructure over the EF factory.
/// <para>
/// A factory rather than an injected context: in Blazor Server a scoped service lives for the whole
/// circuit, so a shared context would accumulate tracked entities, serve stale reads, and hold a
/// pooled connection open for hours.
/// </para>
/// </remarks>
public interface IApplicationDbContextFactory
{
    /// <summary>Creates a context the caller owns and disposes.</summary>
    /// <param name="ct">Cancels the creation.</param>
    /// <returns>A new context.</returns>
    Task<IApplicationDbContext> CreateAsync(CancellationToken ct = default);
}
