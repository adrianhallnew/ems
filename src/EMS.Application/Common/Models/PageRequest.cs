namespace EMS.Application.Common.Models;

/// <summary>
/// The paging and sorting a grid asks for.
/// </summary>
/// <remarks>
/// Every value here arrives from the client and is therefore untrusted: the page size is clamped
/// and the sort column is resolved through a per-entity allow-list before any query is built.
/// </remarks>
public abstract record PageRequest
{
    /// <summary>Gets the one-based page number.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Gets the requested page size, before clamping.</summary>
    public int PageSize { get; init; } = 25;

    /// <summary>Gets the requested sort column name, or null for the entity default.</summary>
    public string? SortBy { get; init; }

    /// <summary>Gets a value indicating whether the sort runs descending.</summary>
    public bool SortDescending { get; init; }
}
