# ADR-0004: Derive Attendance State; Store Only Real Events

**Status:** Accepted
**Date:** 2026-08-12

## Context

Version 2.0 stored a `Status` column on `AttendanceRecord` with values `Present`, `Late`, `Absent`, `Holiday`, and `Weekend`. A record is only created when an employee clocks in.

That combination is incoherent. Nobody clocks in on a weekend, a public holiday, or a day they were absent, so no record exists to carry those statuses — three of the five values were unreachable. The Admin dashboard counted absences and the monthly report broke down holidays, both against data that could never exist.

The specification also counted "On Leave" on the dashboard, which was not in the enum at all. An employee on approved leave had no attendance record, so any absence calculation would have counted their approved leave as an absence.

## Decision

Store only real events: clock-in, clock-out, and admin corrections. Derive every state at read time.

`AttendanceState` is a domain enum with no column. A single `AttendanceStateResolver` takes an employee set and a date range, issues three indexed queries — attendance records, public holidays, approved leave — and projects one result per employee per date, applying this resolution order:

1. Before hire date, or after deactivation → `NotEmployed`
2. Saturday or Sunday → `Weekend`
3. Public holiday → `Holiday`
4. Within an approved leave request → `OnLeave`
5. Record exists, clock-in after 08:00 SCT → `Late`
6. Record exists, clock-in at or before 08:00 SCT → `Present`
7. No record → `Absent`

Every consumer — dashboard, records grid, monthly report — goes through this one component.

## Alternatives considered

**Materialise rows nightly.** A job creates `Absent`, `Weekend`, and `Holiday` rows for each employee for each elapsed day. This is what the v2.0 model implied but never specified.

Rejected on the maintenance surface. The materialised rows must stay consistent with leave approved after the fact, leave cancelled after the fact, holidays an admin adds or removes retroactively, employees hired mid-month, and employees deactivated mid-month. Every one of those is a separate reconciliation path, and each is a place where the stored value silently diverges from the truth. The job must also be catch-up capable, since the application is not continuously running.

**Materialise on read and cache.** Combines the storage cost with the staleness risk. Rejected.

**Compute in each consumer.** Three screens, three implementations, guaranteed drift. Rejected.

## Consequences

- Three background jobs are not needed, and an entire class of sync bug cannot occur. Retroactive leave approval, holiday edits, and mid-period hires are all correct automatically, because nothing was cached.
- The rules live in exactly one place, so the dashboard and the report cannot disagree.
- Reads cost more. A 31-day month for 50 employees is 1,550 projected results assembled from three indexed queries. Negligible at this scale; it would need revisiting at a few thousand employees.
- The attendance table stays small — one row per actual attendance, not one row per employee per calendar day.
- `Employee.DeactivatedAt` becomes necessary, so that dates after departure resolve to `NotEmployed` rather than counting as absences.
- Historical reports reflect current holiday data. If an admin deletes a holiday, past months re-derive as working days. Accepted, and the reason approved leave fixes its business-day count at submission time rather than re-deriving it.
