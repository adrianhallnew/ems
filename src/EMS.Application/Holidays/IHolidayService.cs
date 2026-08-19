using EMS.Application.Common.Models;

namespace EMS.Application.Holidays;

/// <summary>The public holiday calendar.</summary>
public interface IHolidayService
{
    /// <summary>Reads the holidays for one calendar year, generating them if absent.</summary>
    /// <param name="year">The year.</param>
    /// <param name="ct">Cancels the work.</param>
    /// <returns>The year's holidays, ordered by date.</returns>
    /// <remarks>
    /// Generation is idempotent and never overwrites an entry an Admin has edited or deleted for
    /// that year (spec section 3.7.4).
    /// </remarks>
    Task<IReadOnlyList<PublicHolidayDto>> GetForYearAsync(int year, CancellationToken ct);

    /// <summary>Reads the holidays falling inside a date range.</summary>
    /// <param name="startDate">The first date, inclusive.</param>
    /// <param name="endDate">The last date, inclusive.</param>
    /// <param name="ct">Cancels the query.</param>
    /// <returns>The holidays in the range.</returns>
    Task<IReadOnlyList<PublicHolidayDto>> GetInRangeAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct);

    /// <summary>Generates and stores any missing holidays for a year.</summary>
    /// <param name="year">The year to generate.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>How many holidays were added.</returns>
    Task<int> EnsureGeneratedAsync(int year, CancellationToken ct);

    /// <summary>Adds a holiday. Admin only.</summary>
    /// <param name="command">The name and date.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>The new key, or Conflict when the date is already taken.</returns>
    Task<Result<Guid>> CreateAsync(CreateHolidayCommand command, CancellationToken ct);

    /// <summary>Edits a holiday. Admin only.</summary>
    /// <param name="command">The holiday and its new values.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>The outcome.</returns>
    Task<Result> UpdateAsync(UpdateHolidayCommand command, CancellationToken ct);

    /// <summary>Deletes a holiday. Admin only.</summary>
    /// <param name="holidayId">The holiday to delete.</param>
    /// <param name="ct">Cancels the write.</param>
    /// <returns>
    /// The outcome. Deleting does not retroactively alter approved leave day counts, which were
    /// fixed at submission (spec section 3.7.3).
    /// </returns>
    Task<Result> DeleteAsync(Guid holidayId, CancellationToken ct);
}
