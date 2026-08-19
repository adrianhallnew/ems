namespace EMS.Application.Common.Models;

/// <summary>
/// The classes of expected failure a service can return.
/// </summary>
/// <remarks>
/// A code rather than a bare string, so the UI can distinguish a conflict from a validation
/// failure without matching on message text, and so messages stay free to change.
/// </remarks>
public enum ErrorCode
{
    /// <summary>No failure.</summary>
    None,

    /// <summary>The record does not exist, or is outside the caller's scope.</summary>
    NotFound,

    /// <summary>The caller may not perform this operation on this record.</summary>
    Forbidden,

    /// <summary>A field-level rule was broken.</summary>
    Validation,

    /// <summary>The operation collided with existing state, such as a duplicate clock-in.</summary>
    Conflict,

    /// <summary>A business rule refused the operation.</summary>
    BusinessRule,

    /// <summary>Another writer changed the row first; the caller may retry.</summary>
    ConcurrencyConflict,
}

/// <summary>An expected failure.</summary>
/// <param name="Code">The failure class.</param>
/// <param name="Message">A message safe to show a user.</param>
public readonly record struct Error(ErrorCode Code, string Message);

/// <summary>The outcome of an operation that returns no value.</summary>
/// <param name="IsSuccess">Whether the operation succeeded.</param>
/// <param name="Error">The failure, when it did not.</param>
/// <remarks>
/// Exceptions are for bugs, results are for outcomes. A duplicate clock-in is an outcome; a null
/// dependency is a bug.
/// </remarks>
public sealed record Result(bool IsSuccess, Error? Error)
{
    /// <summary>Creates a successful result.</summary>
    /// <returns>The result.</returns>
    public static Result Success() => new(true, null);

    /// <summary>Creates a failed result.</summary>
    /// <param name="code">The failure class.</param>
    /// <param name="message">A message safe to show a user.</param>
    /// <returns>The result.</returns>
    public static Result Fail(ErrorCode code, string message) =>
        new(false, new Error(code, message));
}

/// <summary>The outcome of an operation that returns a value.</summary>
/// <typeparam name="T">The value type.</typeparam>
/// <param name="IsSuccess">Whether the operation succeeded.</param>
/// <param name="Value">The value, when it succeeded.</param>
/// <param name="Error">The failure, when it did not.</param>
public sealed record Result<T>(bool IsSuccess, T? Value, Error? Error)
{
    /// <summary>Creates a successful result.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The result.</returns>
    public static Result<T> Success(T value) => new(true, value, null);

    /// <summary>Creates a failed result.</summary>
    /// <param name="code">The failure class.</param>
    /// <param name="message">A message safe to show a user.</param>
    /// <returns>The result.</returns>
    public static Result<T> Fail(ErrorCode code, string message) =>
        new(false, default, new Error(code, message));
}

/// <summary>One page of a larger result set.</summary>
/// <typeparam name="T">The row type.</typeparam>
/// <param name="Items">The rows on this page.</param>
/// <param name="TotalCount">The row count across every page.</param>
/// <param name="Page">The one-based page number.</param>
/// <param name="PageSize">The page size actually applied, after clamping.</param>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    /// <summary>Gets an empty page.</summary>
    /// <param name="page">The requested page number.</param>
    /// <param name="pageSize">The applied page size.</param>
    /// <returns>A page with no rows.</returns>
    public static PagedResult<T> Empty(int page, int pageSize) => new([], 0, page, pageSize);
}
