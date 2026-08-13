# ADR-0003: EF Core Abstractions in Application; No Repositories

**Status:** Accepted
**Date:** 2026-08-12

## Context

Version 2.0 of the architecture document declared that `EMS.Application` depends on `EMS.Domain` and nothing else. Two sections later it showed `BusinessDayCalculator` — placed in Application — running `_db.PublicHolidays.Where(...).ToListAsync()`.

Both statements cannot be true. The rule as written was not the rule the design intended to follow, and a rule nobody follows provides no guidance while still generating arguments.

Version 2.0 also specified a `Data/Repositories/` folder alongside the same services, without saying what the repositories were for or which layer owned querying.

## Decision

**Application may reference `Microsoft.EntityFrameworkCore`.** It may not reference any database provider package. `Microsoft.EntityFrameworkCore.SqlServer` appears only in Infrastructure.

Application defines `IApplicationDbContext`, exposing `DbSet<T>` and `SaveChangesAsync`. Infrastructure implements it on `ApplicationDbContext`.

**No repository layer.** Application services query through `IApplicationDbContext` directly.

An architecture test asserts that `EMS.Application` has no transitive reference to `Microsoft.EntityFrameworkCore.SqlServer` or `Microsoft.Data.SqlClient`, so the boundary is enforced rather than described.

## Alternatives considered

**Strict purity — no EF Core reference in Application.** Every query moves to Infrastructure behind a hand-written interface per use case. This is defensible and is what the v2.0 text claimed. Rejected because the interfaces end up being one method per query, each returning a fully materialised list; the Application layer loses composition, projection, and `Include`, and the interface count grows without bound. The cost is real and the benefit — provider independence — is not something this application will ever exercise.

**The v2.0 arrangement.** Services in Infrastructure, business logic outside the layer named for it. Rejected: it makes the layer names actively misleading.

**Repository per aggregate over EF Core.** Rejected on two grounds. `DbContext` already is a Unit of Work and `DbSet<T>` already is a repository, so the wrapper adds indirection without adding capability. And in practice the wrapper blocks the things worth having — `Include`, projection to DTOs, split queries, `ExecuteDeleteAsync` — until it grows leaky overloads that expose `IQueryable` and abandon the pretence.

## Consequences

- The stated rule matches the enforced rule, and a test proves it.
- Application services compose queries directly, projecting to DTOs without materialising entities.
- Swapping database providers would touch Application if any provider-specific translation leaked in. Accepted: the architecture test catches package-level leakage, and no such swap is anticipated (ADR-0009).
- Unit-testing an Application service means substituting `IApplicationDbContext`. In practice, service tests that involve queries are integration tests against a real SQL Server instance, which is more honest — a substituted `IQueryable` tests LINQ-to-Objects, not the query that will actually run.
- Domain remains genuinely dependency-free. That boundary is unchanged and is not negotiable.
