# ADR-0008: `TimeProvider` as the Only Clock

**Status:** Accepted
**Date:** 2026-08-12

## Context

EMS has an unusual density of time-dependent business rules for its size: probation expiry at three months from hire, leave balance periods on hire anniversaries, late arrival relative to 08:00 SCT, missed clock-out detection after end of day, and attendance dates that must be SCT calendar dates rather than UTC ones.

Version 2.0 specified a custom `IDateTimeService`:

```csharp
public DateTime ToSct(DateTime utc) => utc.Add(SctOffset);
```

Two problems. `DateTime.Add` returns a value whose `Kind` is `Unspecified`, so the result is indistinguishable from a UTC or local value downstream — the type carries no evidence of which it is, and the compiler cannot help. And the interface has no test double, so every probation and anniversary test would pass or fail according to the calendar on the day it ran.

Version 2.0 also used `DateTime.Today` in a validator. That reads server local time, which in the container is UTC and for the users is UTC+4 — a bug that appears for four hours each day and disappears again.

## Decision

`TimeProvider` from the base class library is the only clock. It is registered as `TimeProvider.System` and injected wherever time is read.

A single `SctClock` wraps it for the fixed UTC+4 conversion:

```csharp
public sealed class SctClock(TimeProvider time)
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(4);
    public DateTimeOffset UtcNow => time.GetUtcNow();
    public DateTimeOffset Now => time.GetUtcNow().ToOffset(Offset);
    public DateOnly Today => DateOnly.FromDateTime(Now.DateTime);
    public DateOnly DateOf(DateTime utcInstant) => /* … */;
}
```

`DateTimeOffset` throughout, so every value carries its offset. `SctClock.Today` is the only source of "today" in the application.

`DateTime.Now` and `DateTime.Today` are prohibited. Tests use `FakeTimeProvider` from `Microsoft.Extensions.TimeProvider.Testing`.

## Alternatives considered

**Custom `IDateTimeService`, as in v2.0.** Rejected: no test double, and `DateTime.Add` loses the offset.

**`TimeZoneInfo.FindSystemTimeZoneById("Indian/Mahe")`.** More principled in general, and correct if Seychelles ever adopted daylight saving. Rejected here because it introduces a dependency on the host's timezone database, which differs between Windows development machines and the chiselled Linux container — a class of environment-specific failure that a fixed offset does not have. Seychelles has never observed daylight saving. The offset lives in one constant in one class, so revisiting this is a small change.

**`DateTimeOffset.UtcNow` directly at call sites.** Rejected: untestable, and it scatters the SCT conversion.

## Consequences

- Time-dependent tests are deterministic. A probation boundary test asserts the behaviour at exactly 2 months 29 days and at 3 months 1 day, on any day of the year.
- The SCT day boundary rule (spec §3.3.3) becomes enforceable rather than aspirational, because there is exactly one place that computes "today".
- No value in the system carries an ambiguous `Kind`.
- `PeriodicTimer` in the background jobs takes the `TimeProvider`, so job scheduling is testable too — the catch-up behaviour after simulated downtime is a unit test rather than an operational surprise.
- A fixed offset is an assumption about Seychelles that is currently correct and is isolated to one constant.
- The prohibition on `DateTime.Now` needs enforcement, not just documentation. An analyzer rule is preferable to review discipline.
