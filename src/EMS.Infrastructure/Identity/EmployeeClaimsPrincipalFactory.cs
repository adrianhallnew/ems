using System.Security.Claims;
using EMS.Domain.Enums;
using EMS.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EMS.Infrastructure.Identity;

/// <summary>
/// Adds the employee identity and, for a Manager, their department scope to the principal.
/// </summary>
/// <param name="userManager">Identity's user store.</param>
/// <param name="roleManager">Identity's role store.</param>
/// <param name="options">Identity options, which name the role and name claim types.</param>
/// <param name="db">The context used to resolve the employee behind the user.</param>
/// <remarks>
/// The scope a Manager gets is read once, here, rather than on every query. It goes stale only
/// until the security stamp changes, and every operation that alters scope — deactivation, role
/// change, manager assignment — refreshes the stamp (architecture.md §3.1, §6.3).
/// </remarks>
public sealed class EmployeeClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<IdentityOptions> options,
    ApplicationDbContext db)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>(userManager, roleManager, options)
{
    /// <inheritdoc/>
    public override async Task<ClaimsPrincipal> CreateAsync(ApplicationUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var principal = await base.CreateAsync(user).ConfigureAwait(false);

        var identity = (ClaimsIdentity)principal.Identity!;

        // The soft-delete filter is deliberate here: a deactivated employee gets no employee
        // claim, so every scoped query resolves to nothing even if the cookie outlives the change.
        var employee = await db.Employees
            .AsNoTracking()
            .Where(e => e.UserId == user.Id)
            .Select(e => new { e.Id, e.Role })
            .SingleOrDefaultAsync()
            .ConfigureAwait(false);

        if (employee is null)
        {
            return principal;
        }

        identity.AddClaim(new Claim(
            EmployeeClaims.EmployeeId,
            employee.Id.ToString()));

        if (employee.Role != EmployeeRole.Manager)
        {
            return principal;
        }

        var managed = await db.Departments
            .AsNoTracking()
            .Where(d => d.ManagerId == employee.Id)
            .Select(d => d.Id)
            .ToListAsync()
            .ConfigureAwait(false);

        foreach (var departmentId in managed)
        {
            identity.AddClaim(new Claim(
                EmployeeClaims.ManagedDepartment,
                departmentId.ToString()));
        }

        return principal;
    }
}
