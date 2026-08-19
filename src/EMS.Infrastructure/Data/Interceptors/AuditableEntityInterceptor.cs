using EMS.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EMS.Infrastructure.Data.Interceptors;

/// <summary>
/// Stamps creation and modification instants from <see cref="TimeProvider"/>.
/// </summary>
/// <param name="time">The clock. Never server local time; see ADR-0008.</param>
/// <remarks>
/// Services never set these values by hand. Hand-set timestamps get forgotten on exactly the code
/// path nobody tested, and one interceptor cannot forget.
/// </remarks>
public sealed class AuditableEntityInterceptor(TimeProvider time) : SaveChangesInterceptor
{
    /// <inheritdoc/>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = time.GetUtcNow().UtcDateTime;

        foreach (EntityEntry<ICreatedAtEntity> entry in context.ChangeTracker.Entries<ICreatedAtEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
            }
        }

        foreach (EntityEntry<IAuditableEntity> entry in context.ChangeTracker.Entries<IAuditableEntity>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}
