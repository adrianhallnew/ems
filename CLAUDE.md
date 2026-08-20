# EMS — working agreement

An Employee Management System for a Seychelles organisation. .NET 10, Blazor Server, EF Core against
SQL Server, Clean Architecture across four projects.

## `docs/` is normative

Four documents govern this repository. Read the governing section before writing code, not after
getting stuck.

| Document | Authority over |
|---|---|
| `docs/spec.md` | What the system does — roles, rules, fields, workflows |
| `docs/architecture.md` | How it is structured — layers, data model, indexes, contracts |
| `docs/implementation.md` | The build order — 11 phases, each with a checklist |
| `docs/decisions/` | Why a non-obvious choice was made, and what it costs |

Rules:

1. **Cite what you rely on.** `docs/spec.md §3.4.5`, not "the spec says". A claim with no citation
   is an assumption and must be labelled one.
2. **A deviation is not done until it is written down.** Record it in the phase's "Deviations
   recorded during execution" table in `docs/implementation.md`, and add an ADR when it carries
   rationale worth keeping. Undocumented-but-justified is still undocumented.
3. **The documents can be wrong.** `implementation.md` §2.2 once prescribed an interceptor order
   that corrupted every audit row. When a document and reality conflict, say which is wrong and
   why — then fix both.
4. **Prefer a gate to a sentence.** If a rule can be an analyzer, a test, or a startup assertion,
   make it one. The RS0030 ban list in `Directory.Build.props` and
   `tests/EMS.UnitTests/Architecture/` exist because prose alone does not stop the next commit.
5. **Run `/grill-with-docs` at the end of a phase.** It audits code against these documents and
   reports drift with citations.

## Commands

```bash
dotnet build EMS.sln                    # zero warnings, TreatWarningsAsErrors
dotnet format EMS.sln --verify-no-changes
dotnet tests/EMS.UnitTests/bin/Debug/net10.0/EMS.UnitTests.dll
dotnet ef migrations add <Name> --project src/EMS.Infrastructure --startup-project src/EMS.Web --output-dir Data/Migrations
```

Always name `EMS.sln` explicitly.

`dotnet test` fails on this machine with `Win32Exception (5): Access is denied` when it launches the
test host. Run the test assembly directly, as above — same tests, same result.

## Conventions this codebase already holds to

- **No clock but `TimeProvider`.** `DateTime.Now`, `DateTime.Today` and both `UtcNow` properties are
  banned by the RS0030 list in `Directory.Build.props` and fail the build. `SctClock` is the only
  source of "today".
- **Every service method takes a `CancellationToken`**, and no own-data operation takes an employee
  identifier — `ClockInAsync(CancellationToken)`, never `ClockInAsync(Guid, CancellationToken)`.
- **`EMS.Application` never references a database provider.** EF Core abstractions only; the
  boundary is asserted by a test. See ADR-0003 and ADR-0013.
- **Results for outcomes, exceptions for bugs.** A duplicate clock-in returns
  `Result.Fail(Conflict, …)`; a null dependency throws.
- **Field-level rules in validators, stateful rules in the service** — inside the transaction that
  commits the change. A validator that queries the database creates a check-then-act gap.
- **Enums are stored as strings** with an explicit `HasMaxLength`; every string column is bounded
  except `AuditEntry.ChangedFields`.
- CRLF line endings, file-scoped namespaces, XML doc comments on public members.

## Git

Adrian commits and pushes. Leave finished work in the working tree and say what the commit
boundaries are; do not run `git commit` or `git push`.
