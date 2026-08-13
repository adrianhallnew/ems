# ADR-0009: SQL Server as the Datastore

**Status:** Accepted
**Date:** 2026-08-12
**Supersedes:** [ADR-0001](0001-sqlite-datastore.md)

## Context

ADR-0001 chose SQLite: correct for the stated scope of 10–50 employees deployed locally, and it kept the container story to a single service with no configuration.

Two things changed the calculation.

The developer works in Visual Studio, which ships SQL Server Express LocalDB, and wanted to inspect the database with the tooling already installed. That alone would not justify a datastore change — SQLite has free viewers, and swapping stacks to solve a viewer problem is a bad trade.

The stronger reason is that ADR-0001's consequences were a ceiling, not just a set of tradeoffs. Single instance only. No true decimal. No `rowversion`. No encryption at rest. A backup procedure that has to avoid copying a live file. Every one of those is a future migration, and the project was explicitly framed as aiming for industry-grade practice.

Zero code existed. This was the cheapest moment the change would ever be.

## Decision

SQL Server, reached through `Microsoft.EntityFrameworkCore.SqlServer` version 10.0.11.

| Context | Host |
|---|---|
| Local development, `dotnet ef` | SQL Server Express LocalDB — `(localdb)\MSSQLLocalDB` |
| Docker Compose | `mcr.microsoft.com/mssql/server:2022-latest`, Developer edition |
| Integration tests | Testcontainers, same image |

One provider, one migration set, one set of translation behaviours. The three hosts differ only by connection string.

Connection resiliency is enabled with `EnableRetryOnFailure()`.

## Alternatives considered

**Stay on SQLite.** Still defensible for the scope as written. Rejected because the ceiling above is real and the migration cost only grows.

**LocalDB with no container at all.** Simplest possible local setup. Rejected: LocalDB is Windows-only and cannot run in a Linux container, so this would have meant deleting Phase 8, the Docker workflow, and the containerisation requirements from the specification. That is a larger loss than the setup complexity it saves.

**PostgreSQL in a container for both local and Docker.** Arguably the better engineering choice in the abstract — same engine everywhere, no Windows dependency, no edition licensing to think about. Rejected on the specific ground that motivated the change: the developer has SQL Server tooling installed and working, and would have needed to install and learn a separate client for PostgreSQL. Choosing the technically marginal winner while defeating the reason for changing would be a poor trade.

**SQL Server in a container even for local development.** Uniform, and avoids the LocalDB/container split. Rejected because it requires Docker running for every `dotnet run` and every `dotnet ef` command during Phases 1 through 6, which is a real tax on the inner loop for no gain — the engine and the provider are the same either way.

## Consequences

Gained:

- True `decimal`, which retires ADR-0002 (see ADR-0010).
- `rowversion` concurrency tokens maintained by the database rather than by application code.
- The single-instance ceiling lifts. Multi-instance stops being architecturally foreclosed, though it is still out of scope for v1.0.
- A real backup and restore story via `BACKUP DATABASE`.
- Three SQLite-specific pitfalls disappear entirely: WAL configuration, the per-connection `PRAGMA foreign_keys` trap, and `Cache=Shared`.

Accepted:

- **`EnableRetryOnFailure` breaks naive explicit transactions.** A retrying execution strategy refuses a user-initiated transaction unless the whole transaction is wrapped in `CreateExecutionStrategy().ExecuteAsync(...)`, because it cannot replay part of one. Every explicit transaction in the codebase uses the wrapper, and any non-transactional side effect inside it must move after the commit or a retry will duplicate it. This is the single new pitfall the change introduces, and it replaces three that it removed.
- **Compose grows to two services.** Startup goes from instant to roughly 20–30 seconds, and needs `depends_on: condition: service_healthy`, because SQL Server does not accept connections immediately.
- **The mssql image is about 1.6 GB and wants 2 GB of RAM.** Integration tests via Testcontainers pay container startup per test class, roughly 15–20 seconds.
- **Not native on ARM64.** Irrelevant on the current x64 development machine; it would matter for a contributor on Apple Silicon.
- **Two databases exist locally** — the LocalDB one and the container one. They hold different data and neither backs up the other. This surprises people and is documented in the implementation guide.
- **Still unencrypted at rest.** Transparent Data Encryption is not available in LocalDB or Express editions. The local-only deployment scope makes this acceptable; a hosted deployment would need to revisit it.
- **`MSSQL_PID=Developer`** licenses the container for development and testing only. A deployed instance needs a different edition and a licence.

## Revisit when

- The application is deployed anywhere other than a developer's machine — Developer edition licensing and encryption at rest both become live questions.
- More than one instance is required. The datastore permits it, but `MigrateAsync` at startup and the in-process notification publisher do not.
