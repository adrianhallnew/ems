using EMS.Application.Common.Interfaces;
using EMS.Application.Common.Models;
using EMS.Domain.Entities;
using EMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EMS.Application.Holidays;

/// <summary>Public holiday reads, administration, and on-demand generation.</summary>
/// <param name="factory">Creates one short-lived context per operation.</param>
public sealed class HolidayService(IApplicationDbContextFactory factory) : IHolidayService
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<PublicHolidayDto>> GetForYearAsync(int year, CancellationToken ct)
    {
        await EnsureGeneratedAsync(year, ct).ConfigureAwait(false);

        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        var start = new DateOnly(year, 1, 1);
        var end = new DateOnly(year, 12, 31);

        return await Project(db.PublicHolidays.AsNoTracking(), start, end)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PublicHolidayDto>> GetInRangeAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct)
    {
        if (endDate < startDate)
        {
            return [];
        }

        // A range may span a year that has never been asked for (spec §3.7.4).
        for (var year = startDate.Year; year <= endDate.Year; year++)
        {
            await EnsureGeneratedAsync(year, ct).ConfigureAwait(false);
        }

        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        return await Project(db.PublicHolidays.AsNoTracking(), startDate, endDate)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A year is generated once. The guard is "does this year hold any holiday at all", not "is
    /// this particular date present", because spec §3.7.4 requires generation never to resurrect a
    /// holiday an Admin deleted for that year — and a deleted row is indistinguishable from one
    /// that was never written. The cost is that emptying a year completely makes it generatable
    /// again.
    /// </remarks>
    public async Task<int> EnsureGeneratedAsync(int year, CancellationToken ct)
    {
        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        var start = new DateOnly(year, 1, 1);
        var end = new DateOnly(year, 12, 31);

        var alreadyGenerated = await db.PublicHolidays
            .AnyAsync(h => h.Date >= start && h.Date <= end, ct)
            .ConfigureAwait(false);

        if (alreadyGenerated)
        {
            return 0;
        }

        var generated = HolidayGenerator.ForYear(year);

        foreach (var holiday in generated)
        {
            db.PublicHolidays.Add(new PublicHoliday
            {
                Name = holiday.Name,
                Date = holiday.Date,
                Rule = holiday.Rule,
                EasterOffset = holiday.EasterOffset,
                IsSystemGenerated = true,
            });
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return generated.Count;
    }

    /// <inheritdoc/>
    public async Task<Result<Guid>> CreateAsync(CreateHolidayCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        var taken = await db.PublicHolidays
            .AnyAsync(h => h.Date == command.Date, ct)
            .ConfigureAwait(false);

        if (taken)
        {
            return Result<Guid>.Fail(
                ErrorCode.Conflict,
                "A holiday already exists on that date. Two observances on one date are recorded as a single entry.");
        }

        var holiday = new PublicHoliday
        {
            Name = command.Name,
            Date = command.Date,
            Rule = HolidayRule.FixedDate,
            EasterOffset = null,

            // Admin-authored, so regeneration must leave it alone (spec §3.7.1).
            IsSystemGenerated = false,
        };

        db.PublicHolidays.Add(holiday);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result<Guid>.Success(holiday.Id);
    }

    /// <inheritdoc/>
    public async Task<Result> UpdateAsync(UpdateHolidayCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        var holiday = await db.PublicHolidays
            .SingleOrDefaultAsync(h => h.Id == command.HolidayId, ct)
            .ConfigureAwait(false);

        if (holiday is null)
        {
            return Result.Fail(ErrorCode.NotFound, "Holiday not found.");
        }

        var taken = await db.PublicHolidays
            .AnyAsync(h => h.Date == command.Date && h.Id != command.HolidayId, ct)
            .ConfigureAwait(false);

        if (taken)
        {
            return Result.Fail(ErrorCode.Conflict, "A holiday already exists on that date.");
        }

        holiday.Name = command.Name;
        holiday.Date = command.Date;

        // An edited holiday stops being system-generated, which is what protects it from
        // regeneration (spec §3.7.1).
        holiday.IsSystemGenerated = false;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result.Success();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Deleting a holiday does not alter leave already approved: those day counts were fixed at
    /// submission (spec §3.7.3).
    /// </remarks>
    public async Task<Result> DeleteAsync(Guid holidayId, CancellationToken ct)
    {
        await using var db = await factory.CreateAsync(ct).ConfigureAwait(false);

        var holiday = await db.PublicHolidays
            .SingleOrDefaultAsync(h => h.Id == holidayId, ct)
            .ConfigureAwait(false);

        if (holiday is null)
        {
            return Result.Fail(ErrorCode.NotFound, "Holiday not found.");
        }

        db.PublicHolidays.Remove(holiday);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result.Success();
    }

    private static IQueryable<PublicHolidayDto> Project(
        IQueryable<PublicHoliday> query,
        DateOnly startDate,
        DateOnly endDate) =>
        query
            .Where(h => h.Date >= startDate && h.Date <= endDate)
            .OrderBy(h => h.Date)
            .Select(h => new PublicHolidayDto(
                h.Id,
                h.Name,
                h.Date,
                h.Rule,
                h.EasterOffset,
                h.IsSystemGenerated));
}
