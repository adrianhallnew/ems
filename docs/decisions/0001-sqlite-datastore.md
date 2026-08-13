# ADR-0001: SQLite as the Datastore

**Status:** Superseded by [ADR-0009](0009-sql-server-datastore.md)
**Date:** 2026-08-12
**Superseded:** 2026-08-12

> Retained for the record. The reasoning below was sound for the scope as originally stated; it was reversed because the developer's tooling and the project's stated ambition both pointed at SQL Server, and no code had yet been written. See ADR-0009.

## Context

EMS serves 10–50 employees in one location, deployed locally via Docker with no hosted environment in scope. Peak concurrency is the 08:00 clock-in window, when most of the organisation writes within a few minutes of each other.

## Decision

Use SQLite in WAL (Write-Ahead Logging) mode as the only datastore, in a single application instance.

Connection string:

```
Data Source=/app/data/ems.db;Foreign Keys=True;Default Timeout=5
```

## Alternatives considered

**PostgreSQL in a second container.** Correct for a hosted deployment and a natural fit for the eventual migration, but it adds a service to run, a schema to provision, and a backup story to operate, for a workload that a single file handles comfortably. Rejected as premature for the stated scope.

**SQL Server LocalDB.** Windows-only, which conflicts with the Linux container target.

**SQLite with the default rollback journal.** Rejected: the journal serialises all access, so the 08:00 window produces "database is locked" errors. WAL exists precisely for this.

## Consequences

Accepted:

- **One writer at a time.** WAL permits concurrent readers during a write, not concurrent writers. At this scale the write rate is far below the limit.
- **Single instance only.** Two application instances against one SQLite file over any shared filesystem is a corruption risk. This forecloses horizontal scaling.
- **No decimal type**, which drives ADR-0002.
- **No server-side encryption at rest.** The file sits on a Docker volume in plaintext, holding dates of birth, addresses, emergency contacts, and salaries. Accepted only because the deployment is local.
- **Backups require SQLite's online backup API**, not a file copy — copying a live WAL database yields a corrupt snapshot.
- The in-process notification publisher (architecture §4.9) inherits the single-instance assumption.

## Revisit when

Any one of these makes this decision wrong:

- The application is deployed anywhere other than a developer's machine.
- More than one instance is required, for scale or availability.
- The personal data it holds becomes subject to an encryption-at-rest obligation.

The migration target is PostgreSQL. EF Core makes the provider swap mechanical; the work is in the operational surface — connection management, migrations, and backups — not the code.
