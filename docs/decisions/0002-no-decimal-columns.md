# ADR-0002: No Decimal Columns — Integer Minor Units

**Status:** Superseded by [ADR-0010](0010-money-and-duration-types.md)
**Date:** 2026-08-12
**Superseded:** 2026-08-12

> Retained for the record. This decision existed almost entirely to work around a SQLite limitation. ADR-0009 removed that limitation, so the constraint no longer applies. The duration half of the decision survived; the money half did not. See ADR-0010.

## Context

EMS stores two quantities that would conventionally be `decimal`: employee salary in SCR, and hours worked per attendance record.

SQLite has no decimal storage class. EF Core maps `decimal` to `TEXT`. The consequences, from the provider's own documented limitations:

- Equality comparison works.
- **Ordering and comparison require client-side evaluation.** Sorting an employee grid by salary, or filtering attendance by hours worked, either loads the table into memory or fails to translate.
- Aggregation does translate, via the `ef_sum` and `ef_avg` custom functions added in EF Core 9. This is narrower relief than it appears — it rescues `SUM` and `AVG` while leaving `ORDER BY` and `WHERE` broken.

## Decision

No `decimal` column anywhere in the schema.

| Concept | Storage | Display |
|---|---|---|
| Salary | `long`, SCR cents | Divided by 100, formatted as SCR |
| Worked time | `int`, whole minutes | Rendered as hours and minutes |
| Leave days | `int`, whole business days | As-is |

Conversion happens in the presentation layer. The domain deals only in minor units.

## Alternatives considered

**`decimal` with `HasConversion<double>()`.** The provider documentation offers this, and it restores server-side ordering. Rejected for money: binary floating point cannot represent most decimal fractions exactly, and accumulating rounding error in a salary field is the classic version of this mistake.

**`decimal` accepted as-is, with sorting disabled.** Rejected: it requires every future contributor to remember an invisible constraint. The first person to add a sortable salary column breaks it, and the failure is a silent performance cliff or a translation exception, neither of which points at the cause.

**`double` for hours worked.** Rejected: `7.5` hours is exact in decimal and not in binary, and minutes are the natural unit anyway. `int` minutes has no representational error at all.

## Consequences

- Every value is exact. No rounding drift, no floating-point comparison hazards.
- Ordering, filtering, and aggregation all translate to SQL. Nothing falls back to client evaluation.
- No value converters, so the model stays simple and migrations stay readable.
- Presentation must format consistently. A single helper does the conversion; formatting inline at each call site would reintroduce the inconsistency this decision removes.
- A reader inspecting the database sees `4500000` rather than `45000.00`. Accepted — the tradeoff is worth exactness, and the column name (`SalaryCents`) carries the unit.
- This decision survives a move to PostgreSQL. Integer minor units are correct there too; it simply stops being forced.
