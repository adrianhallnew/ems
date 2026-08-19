using EMS.Application.Common.Models;

namespace EMS.Application.Attendance;

/// <summary>Clock events and attendance reads.</summary>
/// <remarks>
/// The clock operations take no employee identifier. The acting employee comes from
/// <c>ICurrentUser</c>, which makes clocking in for somebody else unrepresentable rather than
/// merely refused.
/// </remarks>
public interface IAttendanceService
{
    /// <summary>Records a clock-in for the acting employee on today's SCT date.</summary>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>
    /// The outcome. A unique-index violation surfaces as <see cref="ErrorCode.Conflict"/> — an
    /// ordinary "already clocked in today" result, not a database error.
    /// </returns>
    Task<Result> ClockInAsync(CancellationToken ct);

    /// <summary>Records a clock-out for the acting employee and computes worked minutes.</summary>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>The outcome.</returns>
    Task<Result> ClockOutAsync(CancellationToken ct);

    /// <summary>Reads the acting employee's attendance for today.</summary>
    /// <param name="ct">Cancels the query.</param>
    /// <returns>Today's record and resolved state, or null before any employment.</returns>
    Task<AttendanceTodayDto?> GetTodayAsync(CancellationToken ct);

    /// <summary>Reads attendance across a date range for everyone in scope.</summary>
    /// <param name="filter">The range, scope narrowing, paging and sorting.</param>
    /// <param name="ct">Cancels the query.</param>
    /// <returns>One page of employee-days, each with a derived state.</returns>
    Task<PagedResult<AttendanceDayDto>> GetRecordsAsync(AttendanceFilter filter, CancellationToken ct);

    /// <summary>Creates or corrects a record. Admin only.</summary>
    /// <param name="command">The record and the mandatory correction note.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>The outcome. Correcting a flagged record clears the flag.</returns>
    Task<Result> CorrectRecordAsync(CorrectAttendanceCommand command, CancellationToken ct);

    /// <summary>Deletes a record. Admin only, and audited.</summary>
    /// <param name="recordId">The record to delete.</param>
    /// <param name="reason">The mandatory reason.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>The outcome.</returns>
    Task<Result> DeleteRecordAsync(Guid recordId, string reason, CancellationToken ct);
}
