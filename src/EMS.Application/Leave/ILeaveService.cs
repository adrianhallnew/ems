using EMS.Application.Common.Models;

namespace EMS.Application.Leave;

/// <summary>Leave submission, decisions and cancellation.</summary>
/// <remarks>
/// Overlap and balance checks live inside the committing transaction, not in a validator: checking
/// before opening one reintroduces the race the transaction exists to prevent.
/// </remarks>
public interface ILeaveService
{
    /// <summary>Submits a request for the acting employee.</summary>
    /// <param name="command">The leave type, dates and reason.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>The new request's key, or the first rule that refused it.</returns>
    Task<Result<Guid>> SubmitAsync(SubmitLeaveCommand command, CancellationToken ct);

    /// <summary>Lists leave requests in the caller's scope.</summary>
    /// <param name="filter">Range, scope narrowing, paging and sorting.</param>
    /// <param name="ct">Cancels the query.</param>
    /// <returns>One page of requests.</returns>
    Task<PagedResult<LeaveRequestListDto>> GetAsync(LeaveFilter filter, CancellationToken ct);

    /// <summary>Lists the acting employee's own requests.</summary>
    /// <param name="filter">Range, paging and sorting.</param>
    /// <param name="ct">Cancels the query.</param>
    /// <returns>One page of requests.</returns>
    Task<PagedResult<LeaveRequestListDto>> GetOwnAsync(LeaveFilter filter, CancellationToken ct);

    /// <summary>Reads one request in full.</summary>
    /// <param name="requestId">The request to read.</param>
    /// <param name="ct">Cancels the query.</param>
    /// <returns>The request, or NotFound when out of scope.</returns>
    Task<Result<LeaveRequestDetailDto>> GetByIdAsync(Guid requestId, CancellationToken ct);

    /// <summary>Approves a pending request and decrements the balance.</summary>
    /// <param name="command">The request and an optional note.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>
    /// The outcome. Refused when the reviewer is the requester (separation of duties), and
    /// <see cref="ErrorCode.ConcurrencyConflict"/> when another Admin decided first.
    /// </returns>
    Task<Result> ApproveAsync(ApproveLeaveCommand command, CancellationToken ct);

    /// <summary>Rejects a pending request.</summary>
    /// <param name="command">The request and an optional note.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>The outcome.</returns>
    Task<Result> RejectAsync(RejectLeaveCommand command, CancellationToken ct);

    /// <summary>Cancels a request, restoring balance according to when it is cancelled.</summary>
    /// <param name="command">The request and an optional note.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>
    /// The outcome. Cancelling before the start date restores every business day; cancelling on or
    /// after it restores only the days from today forward (spec section 3.4.5).
    /// </returns>
    Task<Result> CancelAsync(CancelLeaveCommand command, CancellationToken ct);
}
