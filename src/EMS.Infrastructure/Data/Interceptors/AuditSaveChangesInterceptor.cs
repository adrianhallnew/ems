using System.Globalization;
using System.Text.Json;
using EMS.Application.Common.Interfaces;
using EMS.Domain.Entities;
using EMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EMS.Infrastructure.Data.Interceptors;

/// <summary>
/// Writes <see cref="AuditEntry"/> rows for tracked domain changes, in the same transaction as the
/// change itself.
/// </summary>
/// <param name="currentUser">The acting user, which is legitimately absent for system writes.</param>
/// <param name="time">The clock. Never server local time; see ADR-0008.</param>
/// <remarks>
/// Security events — login failure, lockout, password change, admin reset, unlock, role change —
/// do not correspond to a tracked entity change and are written directly by the Identity-adjacent
/// services instead (spec section 3.8.2).
/// </remarks>
public sealed class AuditSaveChangesInterceptor(ICurrentUser currentUser, TimeProvider time)
    : SaveChangesInterceptor
{
    /// <summary>
    /// The entity types that carry an audit trail. <see cref="AuditEntry"/> is deliberately absent:
    /// auditing the audit table is an infinite loop.
    /// </summary>
    private static readonly HashSet<Type> AuditedTypes =
    [
        typeof(Employee),
        typeof(AttendanceRecord),
        typeof(LeaveRequest),
        typeof(LeaveBalance),
        typeof(Department),
    ];

    /// <summary>
    /// Property names that never reach the payload, even when present on a changed entity
    /// (spec section 3.8.3). RowVersion is here because it is database-maintained noise, not a
    /// business field.
    /// </summary>
    private static readonly HashSet<string> RedactedProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash",
        "SecurityStamp",
        "ConcurrencyStamp",
        "Token",
        "TwoFactorRecoveryCode",
        "AuthenticatorKey",
        "RowVersion",
    };

    /// <inheritdoc/>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Audit(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Audit(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Audit(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        // Snapshot first: adding audit rows below mutates the change tracker.
        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(e => AuditedTypes.Contains(e.Entity.GetType()))
            .ToList();

        if (entries.Count == 0)
        {
            return;
        }

        var changedAt = time.GetUtcNow().UtcDateTime;

        foreach (var entry in entries)
        {
            context.Add(BuildAuditEntry(entry, changedAt));
        }
    }

    private AuditEntry BuildAuditEntry(EntityEntry entry, DateTime changedAt)
    {
        var changes = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var property in entry.Properties)
        {
            var name = property.Metadata.Name;

            if (RedactedProperties.Contains(name))
            {
                continue;
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    changes[name] = new { After = Format(property.CurrentValue) };
                    break;

                case EntityState.Deleted:
                    changes[name] = new { Before = Format(property.OriginalValue) };
                    break;

                case EntityState.Modified when property.IsModified
                                               && !Equals(property.OriginalValue, property.CurrentValue):
                    changes[name] = new
                    {
                        Before = Format(property.OriginalValue),
                        After = Format(property.CurrentValue),
                    };
                    break;

                default:
                    break;
            }
        }

        return new AuditEntry
        {
            EntityType = entry.Entity.GetType().Name,
            EntityId = PrimaryKeyOf(entry),
            Action = ActionFor(entry),
            ChangedFields = JsonSerializer.Serialize(changes),
            ChangedById = currentUser.EmployeeId,
            ActorDescription = currentUser.Email ?? currentUser.ActorDescription,
            ChangedAt = changedAt,
        };
    }

    private static AuditAction ActionFor(EntityEntry entry) => entry.State switch
    {
        EntityState.Added => AuditAction.Created,
        EntityState.Deleted => AuditAction.Deleted,
        _ => StatusChanged(entry) ? AuditAction.StatusChanged : AuditAction.Updated,
    };

    private static bool StatusChanged(EntityEntry entry)
    {
        var status = entry.Properties.FirstOrDefault(p =>
            string.Equals(p.Metadata.Name, "Status", StringComparison.Ordinal));

        return status is { IsModified: true } && !Equals(status.OriginalValue, status.CurrentValue);
    }

    private static string PrimaryKeyOf(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();

        if (key is null)
        {
            return string.Empty;
        }

        var values = key.Properties
            .Select(p => Format(entry.Property(p.Name).CurrentValue))
            .ToArray();

        return string.Join(',', values);
    }

    /// <summary>Renders a value for the JSON payload.</summary>
    /// <param name="value">The property value.</param>
    /// <returns>A string form that parses back to the original value.</returns>
    /// <remarks>
    /// Dates and times use the round-trip "O" format. The default invariant form renders
    /// 1 January 1985 as <c>01/01/1985</c>, which an audit reader cannot parse back without
    /// guessing the field order.
    /// </remarks>
    private static string? Format(object? value) => value switch
    {
        null => null,
        byte[] bytes => Convert.ToHexString(bytes),
        DateTime date => date.ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset offset => offset.ToString("O", CultureInfo.InvariantCulture),
        DateOnly date => date.ToString("O", CultureInfo.InvariantCulture),
        TimeOnly time => time.ToString("O", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };
}
