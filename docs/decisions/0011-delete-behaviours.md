# ADR-0011: ClientSetNull for Secondary Employee References

**Status:** Accepted
**Date:** 2026-08-19
**Amends:** `architecture.md` §2.3

## Context

`architecture.md` §2.3 specified `SetNull` for every nullable foreign key pointing at `Employee`: the attendance corrector, the leave reviewer and canceller, the audit actor, and a department's manager.

Applying the initial migration failed:

```
Error Number:1785 - Introducing FOREIGN KEY constraint
'FK_LeaveRequests_Employees_ReviewedById' on table 'LeaveRequests' may cause cycles or
multiple cascade paths.
```

SQL Server counts `ON DELETE SET NULL` as a cascade action. `LeaveRequests` holds three foreign keys to `Employees`, `AttendanceRecords` holds two, and `Departments` and `Employees` reference each other. Each is a multiple-path or cycle case, and the engine refuses the constraint rather than the delete.

## Decision

The primary reference in each table keeps `Restrict`. Every secondary reference to `Employee` uses `DeleteBehavior.ClientSetNull`:

| Table | Column | Behaviour |
|---|---|---|
| `AttendanceRecords` | `CorrectedById` | ClientSetNull |
| `LeaveRequests` | `ReviewedById` | ClientSetNull |
| `LeaveRequests` | `CancelledById` | ClientSetNull |
| `AuditEntries` | `ChangedById` | ClientSetNull |
| `Departments` | `ManagerId` | ClientSetNull |

`ClientSetNull` emits `ON DELETE NO ACTION` in the schema while keeping EF Core's behaviour of nulling the reference on entities it is already tracking. The documented intent survives in the model; only the database-level action changes.

## Alternatives considered

**`NoAction`.** Same schema, but EF stops nulling tracked references too, so the model no longer expresses the intent at all. Rejected for losing information for no gain.

**Make one path cascade.** Would satisfy the engine and silently delete leave history when an employee row went away. Rejected outright.

**Drop the foreign key constraints.** Solves the error by removing the guarantee that motivated the columns. Rejected.

**Triggers to emulate SET NULL.** Works, and puts business behaviour in a place nobody reviews. Rejected.

## Consequences

- The distinction is theoretical in this system: spec §3.1.2 makes employee deletion soft, so no `DELETE` against `Employees` ever runs and no delete action fires.
- Should a hard delete ever be required — a data-protection erasure, for instance — it will fail on these constraints until the service nulls the referencing columns first. That is the correct order of operations anyway, and it fails loudly rather than quietly discarding history.
- `LeaveBalances` and `Notifications` keep `Cascade`, as §2.3 specifies. They are single-path and carry no independent history.

## Revisit when

A hard-delete path is added, or the schema gains a second table with three or more foreign keys into `Employees`.
