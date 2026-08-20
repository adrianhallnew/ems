using System.Security.Claims;
using EMS.Application.Common.Interfaces;
using EMS.Infrastructure.Identity;
using Microsoft.AspNetCore.Components.Authorization;

namespace EMS.Web.Security;

/// <summary>
/// The authenticated user, read from the principal.
/// </summary>
/// <param name="httpContextAccessor">Supplies the principal on a static SSR request or an endpoint.</param>
/// <param name="authenticationStateProvider">Supplies it inside an interactive circuit.</param>
/// <param name="systemActor">Names the background process acting, when no principal is.</param>
/// <remarks>
/// This lives in the web project rather than in Infrastructure because both sources of a principal
/// are framework types the inner layers do not reference.
/// <para>
/// Two sources are needed because a Blazor Server application has two hosting shapes. Account pages,
/// report download endpoints, and the first render of any page run over an <c>HttpContext</c>.
/// Everything after the circuit opens does not, and reads the state the framework handed the
/// provider before the first component rendered.
/// </para>
/// </remarks>
public sealed class ClaimsCurrentUser(
    IHttpContextAccessor httpContextAccessor,
    AuthenticationStateProvider authenticationStateProvider,
    SystemActorContext systemActor)
    : ICurrentUser
{
    /// <inheritdoc/>
    public Guid? EmployeeId =>
        Guid.TryParse(Principal?.FindFirst(EmployeeClaims.EmployeeId)?.Value, out var id)
            ? id
            : null;

    /// <inheritdoc/>
    public string? Email => Principal?.FindFirst(ClaimTypes.Email)?.Value
                            ?? Principal?.Identity?.Name;

    /// <inheritdoc/>
    public bool IsAdmin => Principal?.IsInRole("Admin") ?? false;

    /// <inheritdoc/>
    public bool IsManager => Principal?.IsInRole("Manager") ?? false;

    /// <inheritdoc/>
    public IReadOnlySet<Guid> ManagedDepartmentIds
    {
        get
        {
            var principal = Principal;

            if (principal is null)
            {
                return new HashSet<Guid>();
            }

            return principal.FindAll(EmployeeClaims.ManagedDepartment)
                .Select(claim => Guid.TryParse(claim.Value, out var id) ? id : (Guid?)null)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToHashSet();
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Never null and never empty: the audit trail needs an actor label even when no employee is
    /// behind the change. Falling back to the running job's name rather than a bare "System" is what
    /// makes a background write attributable (spec §3.8.1).
    /// </remarks>
    public string ActorDescription => Email ?? systemActor.Describe();

    private ClaimsPrincipal? Principal
    {
        get
        {
            var fromRequest = httpContextAccessor.HttpContext?.User;

            if (fromRequest?.Identity?.IsAuthenticated == true)
            {
                return fromRequest;
            }

            // A background pass has no user by definition, and asking the provider for one throws.
            if (systemActor.JobName is not null)
            {
                return null;
            }

            try
            {
                // Inside a circuit the framework has already set this state, so the task is
                // complete and reading its result does not block. If it is not complete the caller
                // is running before the first render, where there is no user to report anyway.
                var state = authenticationStateProvider.GetAuthenticationStateAsync();

                return state.IsCompletedSuccessfully ? state.Result.User : null;
            }
            catch (InvalidOperationException)
            {
                // "Do not call GetAuthenticationStateAsync outside of the DI scope for a Razor
                // component" — the framework's way of saying there is no circuit here. Startup
                // migration and seeding both land on this path, and both are system actors.
                return null;
            }
        }
    }
}
