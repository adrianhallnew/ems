# ADR-0007: Shouldly for Assertions

**Status:** Accepted
**Date:** 2026-08-12

## Context

Version 2.0 specified FluentAssertions with no version constraint.

FluentAssertions changed its licensing at version 8: the current line is commercially licensed for non-open-source use. Version 7 remains under the previous terms but is frozen, so it receives no fixes and no support for future .NET versions.

This is not a hypothetical concern. An internal business application at a company is exactly the use that the commercial terms cover, and the obligation attaches quietly through a transitive package reference that nobody re-reads.

The same thing happened to two other packages this stack would conventionally use: **MediatR** and **AutoMapper** both moved to commercial licensing. Neither is used, and the `Commands/`/`Queries/` folder names in the architecture are organisational only — they do not imply a mediator.

## Decision

Use **Shouldly 4.3.0**. MIT licensed, actively maintained.

FluentAssertions is absent from `Directory.Packages.props` deliberately, and its absence is noted there so that a future contributor does not add it back reflexively.

## Alternatives considered

**Pin FluentAssertions 7.x.** Permitted under the older terms and requires no learning. Rejected as a dead end: a frozen package accumulates incompatibility with each .NET release, and the migration would then happen under time pressure during a framework upgrade rather than now, at zero cost, on a codebase with no assertions written yet.

**AwesomeAssertions.** A community fork of FluentAssertions 7 under MIT, API-compatible. Legally sound. Rejected on cost-benefit: its sole advantage is drop-in compatibility with existing FluentAssertions code, and this codebase has none. That leaves a fork carrying the governance uncertainty of its origin, chosen over an established independent project, for a benefit that does not apply.

**xUnit's built-in `Assert`.** No dependency at all, and adequate. Rejected for failure message quality — `Assert.Equal(expected, actual)` on a collection or a record produces markedly less useful output than a fluent library, and diagnosis time is the thing assertions are optimising.

## Consequences

- No licence obligation and no version ceiling.
- Different syntax from FluentAssertions: `result.ShouldBe(expected)` rather than `result.Should().Be(expected)`. Since no test code exists yet, there is nothing to migrate.
- Contributors familiar with FluentAssertions need a short orientation. Phase 7 lists a Context7 lookup for Shouldly syntax for exactly this reason.
- The general lesson is recorded here rather than left implicit: this stack has seen several foundational packages relicense. Licence terms are checked when a package is added, not assumed from what they were.
