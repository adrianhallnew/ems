using System.Text.Json;
using EMS.Application.Common.Interfaces;
using EMS.Application.Common.Models;
using EMS.Application.Common.Time;
using EMS.Domain.Entities;
using EMS.Domain.Enums;

namespace EMS.Application.Audit;

/// <summary>Writes the audit entries that no entity change produces.</summary>
/// <param name="factory">Creates one short-lived context per operation.</param>
/// <param name="currentUser">Supplies the actor, which is null for an anonymous failure.</param>
/// <param name="clock">The only source of "now".</param>
/// <remarks>
/// Login failure, lockout, password change, admin reset, unlock, and role change are audit-worthy
/// (spec §3.8.2) and correspond to no tracked entity, so the SaveChanges interceptor never sees
/// them. This is the one hand-written path into the audit trail, kept deliberately narrow.
/// </remarks>
public sealed class SecurityEventWriter(
    IApplicationDbContextFactory factory,
    ICurrentUser currentUser,
    SctClock clock)
    : ISecurityEventWriter
{
    /// <summary>Matches the EntityId column bound in the audit configuration.</summary>
    private const int EntityIdLength = 64;

    /// <inheritdoc/>
    public async Task<Result> RecordSecurityEventAsync(
        string description,
        string subjectEmail,
        CancellationToken ct)
    {
        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        ArgumentNullException.ThrowIfNull(subjectEmail);

        db.AuditEntries.Add(new AuditEntry
        {
            EntityType = "Security",

            // The subject rather than a row identifier: a failed login has no entity behind it.
            // EntityId is bounded at 64 characters and an email address is not, so the column
            // carries a prefix and the payload carries the whole value.
            EntityId = subjectEmail.Length <= EntityIdLength
                ? subjectEmail
                : subjectEmail[..EntityIdLength],
            Action = AuditAction.SecurityEvent,

            // ChangedFields is JSON everywhere else (spec §3.8.1); a security event keeps the shape.
            ChangedFields = JsonSerializer.Serialize(new
            {
                Event = description,
                Subject = subjectEmail,
            }),
            ChangedById = currentUser.EmployeeId,
            ActorDescription = currentUser.ActorDescription,
            ChangedAt = clock.UtcNow.UtcDateTime,
        });

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result.Success();
    }
}
