using EMS.Application.Common.Models;

namespace EMS.Application.Common.Security;

/// <summary>Turns an untrusted page request into safe query arguments.</summary>
public static class PageRequestExtensions
{
    /// <summary>The page size used when a request asks for a nonsensical one.</summary>
    public const int DefaultPageSize = 25;

    /// <summary>
    /// Clamps the requested paging to something the database can be asked for.
    /// </summary>
    /// <param name="request">The request, whose values arrived from the client.</param>
    /// <param name="maxPageSize">The ceiling, from <c>AppSettings.MaxPageSize</c>.</param>
    /// <returns>A page number of at least 1 and a page size within the ceiling.</returns>
    /// <remarks>
    /// An unclamped page size lets one grid request pull the whole table into memory.
    /// </remarks>
    public static (int Page, int PageSize) Clamp(this PageRequest request, int maxPageSize)
    {
        ArgumentNullException.ThrowIfNull(request);

        var page = request.Page < 1 ? 1 : request.Page;

        var pageSize = request.PageSize switch
        {
            < 1 => DefaultPageSize,
            var size when size > maxPageSize => maxPageSize,
            var size => size,
        };

        return (page, pageSize);
    }

    /// <summary>Returns how many rows to skip for a clamped page.</summary>
    /// <param name="page">The one-based page number.</param>
    /// <param name="pageSize">The clamped page size.</param>
    /// <returns>The offset.</returns>
    public static int SkipFor(int page, int pageSize) => (page - 1) * pageSize;
}
