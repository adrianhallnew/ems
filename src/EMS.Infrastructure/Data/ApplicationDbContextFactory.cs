using EMS.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EMS.Infrastructure.Data;

/// <summary>
/// Adapts EF Core's context factory to the port the application layer declares.
/// </summary>
/// <param name="factory">The EF Core factory, configured with the provider and interceptors.</param>
/// <remarks>
/// The adapter exists only so <c>EMS.Application</c> never has to name
/// <see cref="ApplicationDbContext"/>, which would drag the provider across the layer boundary
/// (ADR-0003).
/// </remarks>
public sealed class ApplicationDbContextFactory(IDbContextFactory<ApplicationDbContext> factory)
    : IApplicationDbContextFactory
{
    /// <inheritdoc/>
    public async Task<IApplicationDbContext> CreateAsync(CancellationToken ct = default) =>
        await factory.CreateDbContextAsync(ct).ConfigureAwait(false);
}
