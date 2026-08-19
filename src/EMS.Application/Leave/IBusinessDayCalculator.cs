namespace EMS.Application.Leave;

/// <summary>Counts working days, excluding weekends and public holidays.</summary>
public interface IBusinessDayCalculator
{
    /// <summary>Counts the business days in an inclusive range.</summary>
    /// <param name="startDate">The first date.</param>
    /// <param name="endDate">The last date.</param>
    /// <param name="ct">Cancels the holiday query.</param>
    /// <returns>The count, which is zero for a range of only weekends and holidays.</returns>
    Task<int> CountBusinessDaysAsync(DateOnly startDate, DateOnly endDate, CancellationToken ct);
}
