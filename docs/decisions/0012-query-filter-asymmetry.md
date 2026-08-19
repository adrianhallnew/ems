# ADR-0012: The Soft-Delete Filter Sits on Employee Alone

**Status:** Accepted
**Date:** 2026-08-19
**Extends:** `architecture.md` §2.5

## Context

`Employee` carries a global query filter on `Status == Active` (§2.5). Its dependents — attendance records, leave requests, leave balances, notifications — carry none, because a departed employee's history is exactly what reports, the audit log, and attendance state resolution need to keep reading.

EF Core objects to the asymmetry at model-build time, once per dependent:

```
Entity 'Employee' has a global query filter defined and is the required end of a relationship
with the entity 'AttendanceRecord'. This may lead to unexpected results when the required
entity is filtered out.
```

The warning is accurate. A query that joins through a required `Employee` navigation gets an inner join, and rows belonging to inactive employees vanish from the result without any predicate saying so.

## Decision

Keep the filter on `Employee` only. Suppress the model-validation warning centrally, in `AddInfrastructure`, with the reasoning stated at the suppression:

```csharp
.ConfigureWarnings(w => w.Ignore(
    CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning))
```

Any query that must see a departed employee's rows calls `IgnoreQueryFilters()` explicitly. `AttendanceStateResolver` already does, which is what lets `DeactivatedAt` resolve post-departure dates to `NotEmployed` rather than counting them as absences.

## Alternatives considered

**Filter the dependents to match.** Silences the warning honestly, and deletes former employees from every report, the audit trail, and historical attendance. Rejected — it breaks the requirement the filter exists to serve.

**Configure the navigations as optional.** Also silences it, by declaring foreign keys nullable that are not. Rejected for lying about the model to satisfy a diagnostic.

**Drop the filter entirely and predicate every query by hand.** Makes the unsafe behaviour the default and the safe one opt-in, which is the wrong polarity (§2.5). Rejected.

**Leave the warning in place.** It fires on every model build with nothing actionable behind it, and warnings nobody can act on stop being read. Rejected.

## Consequences

- The discipline is now load-bearing: a Phase 4 or 5 query that `Include`s `Employee` for historical data and forgets `IgnoreQueryFilters()` silently returns fewer rows. It is a wrong answer, not an exception.
- Queries that filter by `EmployeeId` without touching the navigation are unaffected, which covers most of the read paths.
- Integration tests in Phase 7 should cover at least one historical read for a deactivated employee, since that is the case the suppression makes possible and the omission makes wrong.

## Revisit when

A second entity gains a global query filter, or the codebase acquires enough historical reads that a dedicated "include departed employees" query helper is cheaper than repeating `IgnoreQueryFilters()`.
