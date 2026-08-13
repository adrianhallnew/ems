# ADR-0006: Materialise Leave Balance Periods Lazily

**Status:** Accepted
**Date:** 2026-08-12

## Context

Leave entitlements reset on each employee's hire anniversary rather than on a shared calendar date, so resets are spread across all 365 days of the year.

Version 2.0 specified the reset trigger — "hire anniversary date" — and provided no mechanism to perform it. The only background service in the design was notification purging.

The obvious mechanism is a daily job that finds employees whose anniversary is today and creates their new balance rows. It has a failure mode that matters here: EMS runs in a Docker container that is routinely stopped. Any employee whose anniversary falls during downtime is silently skipped, and keeps last period's depleted balance until someone notices. The failure is invisible — no error, no log entry, just a wrong number on one employee's screen.

## Decision

Balance rows for the current period are created on first access, idempotently, by `LeaveBalanceAccessor.EnsureCurrentPeriodAsync`. Every read, validation, and mutation of a balance calls it first.

There is no scheduled reset.

The period is derived from the employee's hire date: it runs from the most recent hire anniversary on or before today, to the day before the next one. A unique index on `(EmployeeId, LeaveType, PeriodStart)` makes concurrent materialisation safe — the loser of a race catches the constraint violation and re-reads.

## Alternatives considered

**Nightly reset job.** Rejected for the reason above: correctness depends on uptime, and the failure is silent. A catch-up watermark would fix it (as used for the attendance and purge jobs), but that is more machinery than the lazy approach needs, and it still leaves a window where a balance is stale between the anniversary and the next job run.

**Compute balances from leave request history on every read.** No balance table at all; sum approved requests within the period. Genuinely appealing — it removes the stored value and the reset problem together. Rejected because admin adjustments (spec §3.4.7), manually granted maternity entitlements, and partial restores on mid-leave cancellation are all facts that cannot be derived from request history. They would each need their own adjustment table, at which point the balance table has been reinvented with more moving parts.

**Materialise all future periods in advance.** Rejected: entitlements change, employees leave, and rows for periods that never arrive have to be cleaned up.

## Consequences

- The balance is correct whenever it is next read, regardless of how long the application was stopped. Uptime is not a correctness dependency.
- No background job, no watermark, no scheduling for this concern.
- Every code path that touches a balance must call the accessor first. This is a discipline requirement; it is enforced by routing all balance access through `ILeaveBalanceService` rather than querying `LeaveBalances` directly, and by an integration test that asserts a fresh employee gets a materialised period on first read.
- An employee who never logs in accumulates no rows until someone looks. Their historical periods are never materialised, so a report covering a period they never accessed shows no balance row. Reports call the accessor for each employee in scope, which resolves this at the cost of a write during a read operation. Accepted, and documented, because the alternative is a report that under-reports entitlements.
- Concurrent first access is handled by the unique index, not by a lock.
