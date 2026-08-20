using System.Security.Cryptography;
using EMS.Application.Common.Interfaces;
using EMS.Application.Common.Models;
using EMS.Domain.Enums;
using EMS.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EMS.Infrastructure.Identity;

/// <summary>Identity account operations behind the application's port.</summary>
/// <param name="users">Identity's user manager.</param>
/// <param name="db">Used only for the employee-side <c>MustChangePassword</c> flag.</param>
/// <remarks>
/// Everything Identity-specific stays here: the application layer knows an employee has an account
/// and nothing about how it is stored.
/// </remarks>
public sealed class IdentityAccountService(
    UserManager<ApplicationUser> users,
    ApplicationDbContext db)
    : IIdentityAccountService
{
    /// <inheritdoc/>
    public async Task<Result<(string UserId, string? GeneratedPassword)>> CreateAccountAsync(
        string email,
        string? password,
        EmployeeRole role,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var existing = await users.FindByEmailAsync(email).ConfigureAwait(false);

        if (existing is not null)
        {
            return Result<(string, string?)>.Fail(
                ErrorCode.Conflict,
                "An account with that email already exists.");
        }

        var generated = string.IsNullOrWhiteSpace(password) ? GeneratePassword() : null;

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,

            // Email delivery is out of scope, so an unconfirmed account could never sign in
            // (ADR-0005).
            EmailConfirmed = true,
        };

        var created = await users.CreateAsync(user, generated ?? password!).ConfigureAwait(false);

        if (!created.Succeeded)
        {
            return Result<(string, string?)>.Fail(ErrorCode.Validation, Describe(created));
        }

        var assigned = await users.AddToRoleAsync(user, role.ToString()).ConfigureAwait(false);

        if (!assigned.Succeeded)
        {
            // The account without its role is worse than no account: it authenticates and can
            // reach nothing.
            await users.DeleteAsync(user).ConfigureAwait(false);

            return Result<(string, string?)>.Fail(ErrorCode.Validation, Describe(assigned));
        }

        return Result<(string, string?)>.Success((user.Id, generated));
    }

    /// <inheritdoc/>
    public async Task<Result> DeleteAccountAsync(string userId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var user = await users.FindByIdAsync(userId).ConfigureAwait(false);

        if (user is null)
        {
            return Result.Fail(ErrorCode.NotFound, "Account not found.");
        }

        var deleted = await users.DeleteAsync(user).ConfigureAwait(false);

        return deleted.Succeeded
            ? Result.Success()
            : Result.Fail(ErrorCode.BusinessRule, Describe(deleted));
    }

    /// <inheritdoc/>
    public async Task<Result<string?>> ResetPasswordAsync(
        string userId,
        string? password,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var user = await users.FindByIdAsync(userId).ConfigureAwait(false);

        if (user is null)
        {
            return Result<string?>.Fail(ErrorCode.NotFound, "Account not found.");
        }

        var generated = string.IsNullOrWhiteSpace(password) ? GeneratePassword() : null;
        var token = await users.GeneratePasswordResetTokenAsync(user).ConfigureAwait(false);

        var reset = await users
            .ResetPasswordAsync(user, token, generated ?? password!)
            .ConfigureAwait(false);

        if (!reset.Succeeded)
        {
            return Result<string?>.Fail(ErrorCode.Validation, Describe(reset));
        }

        // A reset password must be changed on next sign-in (spec §3.1.7).
        await SetMustChangePasswordAsync(userId, true, ct).ConfigureAwait(false);
        await users.UpdateSecurityStampAsync(user).ConfigureAwait(false);

        return Result<string?>.Success(generated);
    }

    /// <inheritdoc/>
    public async Task<Result> UnlockAsync(string userId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var user = await users.FindByIdAsync(userId).ConfigureAwait(false);

        if (user is null)
        {
            return Result.Fail(ErrorCode.NotFound, "Account not found.");
        }

        await users.SetLockoutEndDateAsync(user, null).ConfigureAwait(false);
        await users.ResetAccessFailedCountAsync(user).ConfigureAwait(false);

        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result> ChangeEmailAsync(string userId, string newEmail, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var user = await users.FindByIdAsync(userId).ConfigureAwait(false);

        if (user is null)
        {
            return Result.Fail(ErrorCode.NotFound, "Account not found.");
        }

        var taken = await users.FindByEmailAsync(newEmail).ConfigureAwait(false);

        if (taken is not null && taken.Id != user.Id)
        {
            return Result.Fail(ErrorCode.Conflict, "An account with that email already exists.");
        }

        // The username is the email here, so both move together or the user cannot sign in.
        var emailSet = await users.SetEmailAsync(user, newEmail).ConfigureAwait(false);

        if (!emailSet.Succeeded)
        {
            return Result.Fail(ErrorCode.Validation, Describe(emailSet));
        }

        var nameSet = await users.SetUserNameAsync(user, newEmail).ConfigureAwait(false);

        if (!nameSet.Succeeded)
        {
            return Result.Fail(ErrorCode.Validation, Describe(nameSet));
        }

        user.EmailConfirmed = true;
        await users.UpdateAsync(user).ConfigureAwait(false);
        await users.UpdateSecurityStampAsync(user).ConfigureAwait(false);

        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result> ChangeRoleAsync(string userId, EmployeeRole role, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var user = await users.FindByIdAsync(userId).ConfigureAwait(false);

        if (user is null)
        {
            return Result.Fail(ErrorCode.NotFound, "Account not found.");
        }

        var current = await users.GetRolesAsync(user).ConfigureAwait(false);
        var removed = await users.RemoveFromRolesAsync(user, current).ConfigureAwait(false);

        if (!removed.Succeeded)
        {
            return Result.Fail(ErrorCode.Validation, Describe(removed));
        }

        var added = await users.AddToRoleAsync(user, role.ToString()).ConfigureAwait(false);

        if (!added.Succeeded)
        {
            return Result.Fail(ErrorCode.Validation, Describe(added));
        }

        // Without this the old role survives on an open circuit until the cookie expires
        // (architecture.md §3.1).
        await users.UpdateSecurityStampAsync(user).ConfigureAwait(false);

        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result> RevokeSessionsAsync(string userId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var user = await users.FindByIdAsync(userId).ConfigureAwait(false);

        if (user is null)
        {
            return Result.Fail(ErrorCode.NotFound, "Account not found.");
        }

        await users.UpdateSecurityStampAsync(user).ConfigureAwait(false);

        return Result.Success();
    }

    /// <inheritdoc/>
    public Task<Result> ClearMustChangePasswordAsync(string userId, CancellationToken ct) =>
        SetMustChangePasswordAsync(userId, false, ct);

    /// <remarks>
    /// Identity's own model carries no such flag; it lives on the employee row, which is why this
    /// adapter needs a context as well as a user manager.
    /// </remarks>
    private async Task<Result> SetMustChangePasswordAsync(
        string userId,
        bool value,
        CancellationToken ct)
    {
        var employee = await db.Employees
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(e => e.UserId == userId, ct)
            .ConfigureAwait(false);

        if (employee is null)
        {
            return Result.Fail(ErrorCode.NotFound, "Employee not found.");
        }

        employee.MustChangePassword = value;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result.Success();
    }

    /// <summary>
    /// Produces a temporary password that satisfies the default Identity complexity rules.
    /// </summary>
    private static string GeneratePassword()
    {
        const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string Lower = "abcdefghijkmnopqrstuvwxyz";
        const string Digits = "23456789";
        const string Symbols = "!@#$%^&*-_";

        var all = Upper + Lower + Digits + Symbols;

        // One of each required class first, then filled to length, then shuffled so the classes do
        // not always land in the same positions.
        var characters = new List<char>
        {
            Pick(Upper),
            Pick(Lower),
            Pick(Digits),
            Pick(Symbols),
        };

        while (characters.Count < 16)
        {
            characters.Add(Pick(all));
        }

        for (var i = characters.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (characters[i], characters[j]) = (characters[j], characters[i]);
        }

        return new string([.. characters]);

        static char Pick(string source) => source[RandomNumberGenerator.GetInt32(source.Length)];
    }

    private static string Describe(IdentityResult result) =>
        string.Join(" ", result.Errors.Select(e => e.Description));
}
