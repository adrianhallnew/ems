using EMS.Application.Common.Models;
using EMS.Domain.Enums;

namespace EMS.Application.Common.Interfaces;

/// <summary>
/// The Identity operations the application needs. Implemented in Infrastructure.
/// </summary>
/// <remarks>
/// Identity owns credentials, the login email, and role membership (spec section 3.1.6). This port
/// exists so the application can drive those without referencing ASP.NET Identity types.
/// </remarks>
public interface IIdentityAccountService
{
    /// <summary>Creates an account for a new employee.</summary>
    /// <param name="email">The login email.</param>
    /// <param name="password">The temporary password, or null to generate one.</param>
    /// <param name="role">The role to grant.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>The Identity user key and the generated password, when one was generated.</returns>
    Task<Result<(string UserId, string? GeneratedPassword)>> CreateAccountAsync(
        string email,
        string? password,
        EmployeeRole role,
        CancellationToken ct);

    /// <summary>Sets a new temporary password and forces a change at next sign-in.</summary>
    /// <param name="userId">The Identity user.</param>
    /// <param name="password">The new password, or null to generate one.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>The generated password, when one was generated.</returns>
    /// <remarks>Refreshes the security stamp, which terminates that account's sessions.</remarks>
    Task<Result<string?>> ResetPasswordAsync(string userId, string? password, CancellationToken ct);

    /// <summary>Clears a lockout before it expires.</summary>
    /// <param name="userId">The Identity user.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>The outcome.</returns>
    Task<Result> UnlockAsync(string userId, CancellationToken ct);

    /// <summary>Changes the login email, which Identity checks for uniqueness.</summary>
    /// <param name="userId">The Identity user.</param>
    /// <param name="newEmail">The new address.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>The outcome.</returns>
    Task<Result> ChangeEmailAsync(string userId, string newEmail, CancellationToken ct);

    /// <summary>Moves an account between roles and refreshes the security stamp.</summary>
    /// <param name="userId">The Identity user.</param>
    /// <param name="role">The new role.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>The outcome.</returns>
    Task<Result> ChangeRoleAsync(string userId, EmployeeRole role, CancellationToken ct);

    /// <summary>Invalidates every session for an account.</summary>
    /// <param name="userId">The Identity user.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>The outcome.</returns>
    Task<Result> RevokeSessionsAsync(string userId, CancellationToken ct);

    /// <summary>Clears the forced-password-change marker after a successful change.</summary>
    /// <param name="userId">The Identity user.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>The outcome.</returns>
    Task<Result> ClearMustChangePasswordAsync(string userId, CancellationToken ct);
}
