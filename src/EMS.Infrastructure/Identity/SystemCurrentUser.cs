using EMS.Application.Common.Interfaces;

namespace EMS.Infrastructure.Identity;

/// <summary>
/// A <see cref="ICurrentUser"/> that reports no signed-in user.
/// </summary>
/// <remarks>
/// Phase 2 needs an implementation so the audit interceptor can resolve its dependency while the
/// database schema is being built. Phase 4 replaces this with the claims-backed implementation
/// that reads the authenticated principal; until then every write is attributed to the system.
/// </remarks>
public sealed class SystemCurrentUser : ICurrentUser
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
    public string ActorDescription => "System";
}
