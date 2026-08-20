using EMS.Application.Common.Options;
using EMS.Domain.Entities;
using EMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EMS.Infrastructure.Jobs;

/// <summary>
/// A daily job that processes every date it has missed, not just yesterday.
/// </summary>
/// <param name="scopeFactory">Creates a scope per pass; a hosted service is a singleton.</param>
/// <param name="time">The clock, injected so the timer is testable.</param>
/// <param name="settings">Supplies the SCT offset used to decide which dates have elapsed.</param>
/// <param name="logger">Records each pass and its outcome.</param>
/// <remarks>
/// "Run once every 24 hours" is a schedule, not a correctness guarantee, for an application that is
/// routinely stopped. Each pass reads its <see cref="JobRun"/> watermark, processes every
/// outstanding SCT date up to yesterday, and advances the watermark only on success. A job that has
/// not run for a week catches up on the next start (architecture.md §4.10, spec §3.3.5).
/// </remarks>
public abstract class CatchUpJob(
    IServiceScopeFactory scopeFactory,
    TimeProvider time,
    IOptions<AppSettings> settings,
    ILogger logger)
    : BackgroundService
{
    /// <summary>Gets the watermark key, unique per job.</summary>
    protected abstract string JobName { get; }

    /// <summary>Gets the clock. Derived jobs read it from here rather than capturing their own.</summary>
    protected TimeProvider Time => time;

    /// <summary>Gets the validated settings.</summary>
    protected AppSettings Settings => settings.Value;

    /// <summary>Gets the logger this job was constructed with.</summary>
    protected ILogger Logger => logger;

    /// <summary>
    /// Gets the furthest back a first run will reach when there is no watermark yet.
    /// </summary>
    /// <remarks>
    /// Without a floor, the first pass of a job on an old database would walk every date since the
    /// epoch. A week is enough to cover a container that was stopped over a holiday.
    /// </remarks>
    protected virtual int InitialCatchUpDays => 7;

    /// <summary>Processes one date. Must be idempotent: a re-run over it changes nothing.</summary>
    /// <param name="sctDate">The SCT date to process.</param>
    /// <param name="db">A context scoped to this pass.</param>
    /// <param name="ct">Cancels the work.</param>
    protected abstract Task ProcessDateAsync(
        DateOnly sctDate,
        ApplicationDbContext db,
        CancellationToken ct);

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24), time);

        // do-while, so the first pass runs at startup rather than 24 hours later. That is the pass
        // that catches up on everything missed while the container was down.
        do
        {
            try
            {
                await RunAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031 // A failed pass must not take the host down; the next one retries.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                logger.LogError(exception, "{JobName} failed. The watermark is unchanged.", JobName);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private async Task RunAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var offset = TimeSpan.FromHours(settings.Value.TimeZoneOffsetHours);
        var today = DateOnly.FromDateTime(time.GetUtcNow().ToOffset(offset).DateTime);

        var run = await db.JobRuns
            .SingleOrDefaultAsync(j => j.JobName == JobName, ct)
            .ConfigureAwait(false);

        if (run is null)
        {
            run = new JobRun
            {
                JobName = JobName,
                LastProcessedDate = null,
            };

            db.JobRuns.Add(run);
        }

        var (from, lastProcessable, hasWork) = CatchUpWindow.Resolve(
            run.LastProcessedDate,
            today,
            InitialCatchUpDays);

        var processed = 0;

        for (var date = from; date <= lastProcessable; date = date.AddDays(1))
        {
            await ProcessDateAsync(date, db, ct).ConfigureAwait(false);
            processed++;
        }

        // The watermark advances only after every date above ran without throwing.
        run.LastProcessedDate = hasWork ? lastProcessable : run.LastProcessedDate;
        run.LastRunAt = time.GetUtcNow().UtcDateTime;
        run.LastResult = processed == 0 ? "Nothing to process" : $"Processed {processed} date(s)";

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "{JobName} completed. {Result}. Watermark now {Watermark}.",
            JobName,
            run.LastResult,
            run.LastProcessedDate);
    }
}
