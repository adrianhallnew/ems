# ADR-0013: An Application-Owned Context Factory Port

**Status:** Accepted
**Date:** 2026-08-19
**Extends:** [ADR-0003](0003-layer-boundary.md)

## Context

Two rules from earlier documents meet here and cannot both be satisfied as written.

ADR-0003 and `architecture.md` §1.1: `EMS.Application` may reference EF Core abstractions but never a provider, and never the concrete `ApplicationDbContext`, which lives in `EMS.Infrastructure`.

`implementation.md` §3.5 and §4.2, and `architecture.md` §4.3: every Application service creates one short-lived context per operation through a factory —

```csharp
await using var db = await _factory.CreateDbContextAsync(ct);
```

EF Core's `IDbContextFactory<TContext>` is generic over a concrete `DbContext`. Naming it in Application means naming `ApplicationDbContext` in Application, which the first rule forbids. The guide's code sample cannot compile in the project it describes.

## Decision

Application declares its own port and Infrastructure adapts EF Core's factory to it:

```csharp
// EMS.Application/Common/Interfaces
public interface IApplicationDbContextFactory
{
    Task<IApplicationDbContext> CreateAsync(CancellationToken ct = default);
}

// EMS.Infrastructure/Data
public sealed class ApplicationDbContextFactory(IDbContextFactory<ApplicationDbContext> factory)
    : IApplicationDbContextFactory
{
    public async Task<IApplicationDbContext> CreateAsync(CancellationToken ct = default) =>
        await factory.CreateDbContextAsync(ct).ConfigureAwait(false);
}
```

`IApplicationDbContext` now extends `IAsyncDisposable` and `IDisposable`, so callers keep the documented `await using` shape and dispose what they create.

## Alternatives considered

**Let Application reference `ApplicationDbContext`.** One line, and it collapses the layer boundary ADR-0003 exists to hold. The provider follows the context, and the architecture test that asserts no `Microsoft.Data.SqlClient` in Application starts failing. Rejected.

**Move the business services back to Infrastructure.** Removes the problem by reverting the v3.0 decision that put them in Application. Rejected — that decision was made for reasons the compile error does not touch.

**Inject a scoped `IApplicationDbContext` instead of a factory.** Simple, and in Blazor Server a scoped service lives for the whole circuit: hours of accumulated tracking, stale reads, overlapping operations on one context, and a pooled connection held open. Rejected for the reasons in `architecture.md` §4.3.

**Define the port as returning EF's `DbContext` base type.** Keeps one interface instead of two, and hands Application a type whose `Set<T>()` reaches anything in the model, defeating the point of `IApplicationDbContext`. Rejected.

## Consequences

- One interface and one four-line adapter, registered scoped alongside the EF factory.
- Application code never names a `DbContext` type. The provider-leak check in the Phase 3 verification stays clean.
- Faking the port in a unit test is now possible without a database, though the calculators that need one are still covered by integration tests rather than fakes.
- `implementation.md` §3.5's sample is superseded by this record; the shape is the same, the type is not.
