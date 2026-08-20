using EMS.Application.Common.Interfaces;

namespace EMS.Infrastructure.Identity;

/// <summary>
/// A <see cref="ICurrentUser"/> that reports no signed-in user.
/// </summary>
/// <remarks>
/// The web project replaces this with the claims-backed implementation. It stays registered as the
/// default so anything hosted without a principal — the seeder, a design-time context, a background
/// host — still resolves an actor, and <see cref="SystemActorContext"/> supplies the label.
/// </remarks>
public sealed class SystemCurrentUser(SystemActorContext systemActor) : ICurrentUser
{
    /// <inheritdoc/>
    public Guid? EmployeeId => null;

    /// <inheritdoc/>
    public string? Email => null;

    /// <inheritdoc/>
    public bool IsAdmin => false;

    /// <inheritdoc/>
    public bool IsManager => false;

    /// <inheritdoc/>
    public IReadOnlySet<Guid> ManagedDepartmentIds { get; } = new HashSet<Guid>();

    /// <inheritdoc/>
    public string ActorDescription => systemActor.Describe();
}
