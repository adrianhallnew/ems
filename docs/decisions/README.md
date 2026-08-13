# Architecture Decision Records

Each record captures one decision that was not obvious, the alternatives considered, and the consequences accepted. They exist so that a future reader can tell the difference between a deliberate choice and an accident.

A record is never edited to reflect a change of mind. If a decision is reversed, a new record supersedes it and the old one is marked accordingly.

| # | Decision | Status |
|---|---|---|
| [0001](0001-sqlite-datastore.md) | SQLite as the datastore, and the ceiling that imposes | Superseded by 0009 |
| [0002](0002-no-decimal-columns.md) | No decimal columns — integer minor units | Superseded by 0010 |
| [0003](0003-layer-boundary.md) | EF Core abstractions in Application; no repositories | Accepted |
| [0004](0004-derived-attendance-state.md) | Derive attendance state; store only real events | Accepted |
| [0005](0005-identity-template.md) | Adopt the Blazor Identity template rather than hand-rolling auth | Accepted |
| [0006](0006-lazy-leave-balance.md) | Materialise leave balance periods lazily, not on a schedule | Accepted |
| [0007](0007-assertion-library.md) | Shouldly for assertions | Accepted |
| [0008](0008-timeprovider.md) | `TimeProvider` as the only clock | Accepted |
| [0009](0009-sql-server-datastore.md) | SQL Server as the datastore — LocalDB, container, Testcontainers | Accepted |
| [0010](0010-money-and-duration-types.md) | Money as decimal, duration as integer minutes | Accepted |

## Format

```
# ADR-NNNN: Title

Status · Date · Context · Decision · Alternatives considered · Consequences
```

Keep them short. A record that needs more than a page is usually describing several decisions.
