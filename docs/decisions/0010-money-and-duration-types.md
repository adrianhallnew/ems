# ADR-0010: Money as Decimal, Duration as Integer Minutes

**Status:** Accepted
**Date:** 2026-08-12
**Supersedes:** [ADR-0002](0002-no-decimal-columns.md)

## Context

ADR-0002 banned `decimal` columns and stored salary as `long` SCR cents. That decision existed almost entirely to work around SQLite, where `decimal` is stored as text and ordering and comparison fall back to client-side evaluation.

ADR-0009 moved the datastore to SQL Server, which has a true fixed-point `decimal`. The constraint that produced ADR-0002 no longer exists, so the decision needs re-deciding rather than inheriting.

The two halves of ADR-0002 turn out to have had different justifications, and they do not survive equally.

## Decision

| Concept | Type | Mapping |
|---|---|---|
| Salary | `decimal` | `HasPrecision(18, 2)` |
| Worked time | `int` | Whole minutes |
| Leave days | `int` | Whole business days |
| Concurrency tokens | `byte[]` | `IsRowVersion()` |

No floating-point type appears anywhere in the model.

## Reasoning

**Money becomes `decimal`.** On SQL Server this is exact, orders and compares in SQL, and aggregates natively. It is also the conventional choice for currency in .NET and SQL Server, which matters for the next person reading the model: `decimal Salary` needs no explanation, while `long SalaryCents` invites someone to wonder what problem it was solving and, eventually, to "fix" it.

Integer minor units remain a legitimate practice, particularly where multiple currencies or sub-cent precision are in play. Neither applies here — one currency, two decimal places — so the practice would be carrying its cost without its benefit.

**Duration stays integer minutes.** This half of ADR-0002 was never about SQLite. Minutes are what the system actually measures: two UTC timestamps subtracted. "7.5 hours" is a formatting decision made at the presentation edge. Storing the fractional hour would move a rounding decision into the data, where it is irreversible, in exchange for nothing.

`decimal` hours would be exact, so this is not a precision argument. It is that the stored value should be the measurement, not a derived presentation of it.

**Concurrency tokens become `rowversion`.** ADR-0002's sibling decision used an application-managed integer because SQLite has no `rowversion`. SQL Server maintains `rowversion` itself on every update, so correctness stops depending on the application remembering to increment a column — which is exactly the kind of thing that gets forgotten on the one code path nobody tested.

## Consequences

- Salary values round-trip exactly and sort correctly in SQL. An integration test asserts this rather than assuming it.
- `HasPrecision(18, 2)` is mandatory. Without it, EF Core maps `decimal` to `decimal(18,2)` by default on SQL Server, but relying on a provider default for a money column is the kind of implicit behaviour that changes between versions.
- The presentation layer formats salary as SCR and worked minutes as hours and minutes. Both conversions live in one helper each; formatting inline at call sites would produce the inconsistency this decision is meant to avoid.
- Reports that total worked time sum integers and convert once at the end, which avoids accumulating a rounding error across a month of rows.
- `double` and `float` are prohibited for both concepts. A Phase 1 checklist item enforces it.
