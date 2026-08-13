# EMS — Employee Management System: Architecture Document

> **Version:** 3.0
> **Date:** August 12, 2026
> **Architecture Pattern:** Clean Architecture (multi-project solution)
> **Supersedes:** v2.0 (August 12, 2026)

---

## 0. Changes from v2.0

| Area | Change |
|---|---|
| Layer boundary | v2.0 declared "Application depends on Domain only" and then showed EF Core queries inside Application. The rule is now stated as it will actually be enforced: Application may reference EF Core **abstractions** (ADR-0003). |
| Repositories | Removed. `DbContext` is already a Unit of Work and a set of repositories; wrapping it blocked `Include`, projection, and split queries for no benefit. |
| Service placement | Business services moved from Infrastructure to Application. Infrastructure keeps only genuine external concerns. |
| DbContext lifetime | `AddDbContextFactory`, not scoped `AddDbContext`. A scoped context in Blazor Server lives for the whole circuit (§4.3). |
| Datastore | SQL Server replaces SQLite: LocalDB locally, a SQL Server container in Docker, one EF Core provider (§4.1, ADR-0009). |
| Money and duration | Salary is `decimal(18,2)`; worked time is `int` minutes (ADR-0010). |
| Attendance | `AttendanceStatus` is no longer stored (ADR-0004). |
| Auth | Adopts the .NET 10 Identity template layout, adds revalidation, and specifies two authorisation layers (§3). |
| Concurrency | `rowversion` concurrency tokens on `LeaveBalance` and `LeaveRequest` (§4.7). |
| New tables | `JobRun` (watermarks for catch-up jobs), audit actor fields. |
| Clock | `TimeProvider`, not a custom `IDateTimeService` (ADR-0008). |

---

## 1. Solution Structure

```
EMS.sln
│
├── .github/
│   ├── workflows/
│   │   ├── ci.yml                    # Build, format, test, scan (push + PR)
│   │   └── docker.yml                # Docker build + push (version tags)
│   └── dependabot.yml                # NuGet + Actions update automation
│
├── Directory.Build.props             # Shared compiler settings for all projects
├── Directory.Packages.props          # Central Package Management — all versions
├── nuget.config                      # Single source + package source mapping
│
├── src/
│   ├── EMS.Domain/                   # Enterprise business rules
│   │   ├── Entities/                 # Core domain entities
│   │   ├── Enums/                    # Domain enumerations
│   │   ├── Common/                   # Base classes, marker interfaces
│   │   ├── Exceptions/               # Domain exception types
│   │   └── EMS.Domain.csproj         # No package references
│   │
│   ├── EMS.Application/              # Application business rules
│   │   ├── Common/
│   │   │   ├── Interfaces/           # IApplicationDbContext, ICurrentUser, ...
│   │   │   ├── Models/               # DTOs, Result, PagedResult
│   │   │   ├── Security/             # Scope predicates, allow-lists
│   │   │   └── Time/                 # SCT conversion helpers over TimeProvider
│   │   ├── Employees/                # Commands, Queries, Validators, EmployeeService
│   │   ├── Departments/
│   │   ├── Attendance/               # Includes AttendanceStateResolver
│   │   ├── Leave/                    # Includes LeaveBalanceAccessor
│   │   ├── Reports/                  # Report data assembly (not rendering)
│   │   ├── Notifications/
│   │   ├── Audit/
│   │   ├── Holidays/                 # Includes EasterCalculator
│   │   └── EMS.Application.csproj    # Domain + EF Core abstractions + FluentValidation
│   │
│   ├── EMS.Infrastructure/           # External concerns only
│   │   ├── Data/
│   │   │   ├── ApplicationDbContext.cs
│   │   │   ├── Configurations/       # Entity type configurations (Fluent API)
│   │   │   ├── Interceptors/         # Audit, auditable timestamps
│   │   │   ├── Migrations/
│   │   │   └── Seed/                 # Data seeding (Bogus)
│   │   ├── Identity/                 # ApplicationUser, role provisioning, claims
│   │   ├── Jobs/                     # Hosted services + JobRun watermarks
│   │   ├── Reporting/                # QuestPDF documents, CsvHelper writers
│   │   ├── Notifications/            # In-process notification publisher
│   │   └── EMS.Infrastructure.csproj # Depends on: EMS.Application
│   │
│   └── EMS.Web/                      # Presentation layer
│       ├── Components/
│       │   ├── Account/              # Identity template pages (static SSR)
│       │   ├── Layout/               # MainLayout, NavMenu, NotificationBell
│       │   ├── Pages/                # Routable pages by feature
│       │   └── Shared/               # Reusable non-page components
│       ├── Endpoints/                # Report download endpoints
│       ├── Security/                 # Authorisation handlers, policy registration
│       ├── wwwroot/                  # Self-hosted static assets, including fonts
│       ├── Program.cs                # DI registration, middleware pipeline
│       └── EMS.Web.csproj            # Depends on: EMS.Infrastructure
│
├── tests/
│   ├── EMS.UnitTests/                # Domain + Application + bUnit (fast, no DB)
│   ├── EMS.IntegrationTests/         # EF Core against a Testcontainers SQL Server
│   └── EMS.E2E.Tests/                # Playwright against a real Kestrel host
│
├── Dockerfile
├── docker-compose.yml
├── .dockerignore
├── .editorconfig
└── README.md
```

### 1.1 Dependency Graph

```
EMS.Domain         ← No package references (pure C#)
     ↑
EMS.Application    ← Domain + EF Core abstractions + FluentValidation
     ↑
EMS.Infrastructure ← Application (implements IApplicationDbContext, external services)
     ↑
EMS.Web            ← Infrastructure (DI composition root)
```

**The dependency rule, stated precisely.** Inner layers never reference outer layers. Domain has zero package references. Application references `Microsoft.EntityFrameworkCore` for `DbSet<T>`, `IQueryable` extensions, and value conversions, but **never a database provider package** — no `Microsoft.EntityFrameworkCore.SqlServer` in Application. The provider, the migrations, and the connection are Infrastructure's concern alone.

This is the boundary that the Clean Architecture reference templates actually enforce, and it is testable: an architecture test asserts that `EMS.Application` has no transitive reference to `Microsoft.EntityFrameworkCore.SqlServer` or `Microsoft.Data.SqlClient`. See ADR-0003.

### 1.2 Test Project Dependencies

```
EMS.UnitTests          ← Domain, Application, Web (bUnit)
EMS.IntegrationTests   ← Application, Infrastructure
EMS.E2E.Tests          ← Web (Kestrel host fixture)
```

Test projects are grouped by test type, not by layer, because that maps directly onto staged CI execution: fast tests fail first.

---

## 2. Data Model

### 2.1 Entity Overview

```
┌──────────────────────────────────────────────────────────┐
│                      AspNetUsers                         │
│  (ASP.NET Identity — Id, Email, PasswordHash,            │
│   SecurityStamp, LockoutEnd, ...)                        │
│  Authoritative for: credentials, email/username, roles   │
└──────────────────────┬───────────────────────────────────┘
                       │ 1:1 (UserId FK)
                       ▼
┌──────────────────────────────────────────────────────────┐
│                       Employee                           │
│  Id (Guid v7 PK)                                         │
│  UserId (string FK → AspNetUsers.Id, unique)             │
│  FirstName, LastName, Email, Phone                       │
│  DateOfBirth (DateOnly), Address                         │
│  EmergencyContactName, EmergencyContactPhone             │
│  Salary (decimal 18,2), JobTitle, ContractType (enum)    │
│  DepartmentId (Guid FK), Role (enum, projected)          │
│  HireDate (DateOnly), Status (Active/Inactive)           │
│  DeactivatedAt (DateTime? UTC)                           │
│  MustChangePassword (bool)                               │
│  CreatedAt, UpdatedAt (DateTime UTC)                     │
└────┬──────────────┬──────────────┬───────────────────────┘
     │ 1:N          │ 1:N          │ 1:N
     ▼              ▼              ▼
┌──────────┐  ┌──────────┐  ┌──────────────┐
│Attendance│  │  Leave   │  │ Notification │
│  Record  │  │ Request  │  │              │
└──────────┘  └──────────┘  └──────────────┘
```

`Employee.Role` is a denormalised projection of Identity role membership, maintained on every role change. It exists so that reports and grids can filter by role without joining Identity tables. **Authorisation never reads it** (spec §3.1.6).

`DeactivatedAt` is new in v3.0. Attendance state resolution needs to know when an employee left, so that dates after their departure are excluded rather than counted as absences.

### 2.2 Entity Definitions

```
Department
  Id (Guid v7 PK)
  Name (string, unique), Description (string?)
  ManagerId (Guid? FK → Employee.Id)
  CreatedAt (DateTime UTC)

AttendanceRecord
  Id (Guid v7 PK)
  EmployeeId (Guid FK)
  Date (DateOnly)              -- SCT calendar date, see spec §3.3.3
  ClockIn (DateTime? UTC)
  ClockOut (DateTime? UTC)
  WorkedMinutes (int?)         -- computed on clock-out or correction
  IsFlagged (bool)
  CorrectionNote (string?)
  CorrectedById (Guid? FK → Employee.Id)
  CorrectedAt (DateTime? UTC)
  UNIQUE(EmployeeId, Date)

LeaveRequest
  Id (Guid v7 PK)
  EmployeeId (Guid FK)
  LeaveType (enum)
  StartDate (DateOnly), EndDate (DateOnly)
  BusinessDays (int)           -- fixed at submission
  RestoredDays (int)           -- days returned on cancellation, default 0
  Reason (string?)
  Status (enum)
  ReviewedById (Guid? FK → Employee.Id)
  ReviewedAt (DateTime? UTC), ReviewNote (string?)
  CancelledAt (DateTime? UTC), CancelledById (Guid? FK)
  CreatedAt (DateTime UTC)
  RowVersion (byte[], rowversion concurrency token)

LeaveBalance
  Id (Guid v7 PK)
  EmployeeId (Guid FK)
  LeaveType (enum)
  PeriodStart (DateOnly), PeriodEnd (DateOnly)
  Entitlement (int)
  Used (int)
  RowVersion (byte[], rowversion concurrency token)
  UNIQUE(EmployeeId, LeaveType, PeriodStart)
  -- Remaining is a computed .NET property, not a column

PublicHoliday
  Id (Guid v7 PK)
  Name (string)
  Date (DateOnly, unique)
  Rule (enum: FixedDate | EasterRelative)
  EasterOffset (int?)          -- days from Easter Sunday
  IsSystemGenerated (bool)     -- false once an Admin edits it

AuditEntry
  Id (Guid v7 PK)
  EntityType (string), EntityId (string)
  Action (enum)
  ChangedFields (string, JSON)
  ChangedById (Guid? FK → Employee.Id)   -- null for system actors
  ActorDescription (string)
  ChangedAt (DateTime UTC)

Notification
  Id (Guid v7 PK)
  RecipientId (Guid FK → Employee.Id)
  Title (string), Message (string)
  IsRead (bool, default false)
  NavigationUrl (string?)
  CreatedAt (DateTime UTC)

JobRun
  JobName (string PK)
  LastProcessedDate (DateOnly?)   -- watermark for catch-up jobs
  LastRunAt (DateTime UTC)
  LastResult (string)
```

`RestoredDays` on `LeaveRequest` records how many days a cancellation returned, which may be fewer than `BusinessDays` for a mid-leave admin cancellation (spec §3.4.5). Without it, the audit trail cannot explain a balance that does not reconcile against request history.

`JobRun` makes the nightly jobs catch-up capable. A job reads its watermark, processes every date from there to yesterday, and advances the watermark on success.

### 2.3 Key Relationships

| Relationship | Type | Delete behaviour |
|---|---|---|
| Employee → AspNetUsers | 1:1 via UserId | Restrict |
| Employee → Department | N:1 via DepartmentId | Restrict |
| Department → Employee (Manager) | 0..1 via ManagerId | SetNull |
| AttendanceRecord → Employee | N:1 | Restrict |
| AttendanceRecord → Employee (CorrectedBy) | N:0..1 | SetNull |
| LeaveRequest → Employee | N:1 | Restrict |
| LeaveRequest → Employee (Reviewer, Canceller) | N:0..1 | SetNull |
| LeaveBalance → Employee | N:1 | Cascade |
| Notification → Employee | N:1 | Cascade |
| AuditEntry → Employee | N:0..1 | SetNull |

`AuditEntry` uses SetNull rather than Restrict, because employees are soft-deleted and never actually removed; the relationship is nullable to accommodate system actors, and SetNull is the consistent choice.

### 2.4 Indexes

| Table | Index | Purpose |
|---|---|---|
| Employee | IX_Employee_UserId (unique) | Identity lookup on every request |
| Employee | IX_Employee_DepartmentId | Manager scoping |
| Employee | IX_Employee_Status | Active filter |
| AttendanceRecord | IX_Attendance_EmployeeId_Date (unique) | One record per day; also the double-submit guard |
| AttendanceRecord | IX_Attendance_Date_IsFlagged | Dashboard flagged query |
| LeaveRequest | IX_Leave_EmployeeId_Status_StartDate | Overlap check and balance reconciliation |
| LeaveRequest | IX_Leave_Status_CreatedAt | Admin pending queue |
| LeaveBalance | IX_LeaveBalance_Employee_Type_PeriodStart (unique) | Lazy period materialisation |
| PublicHoliday | IX_Holiday_Date (unique) | Business-day and state resolution |
| Notification | IX_Notification_RecipientId_IsRead | Unread count |
| Notification | IX_Notification_CreatedAt | Auto-purge |
| AuditEntry | IX_Audit_EntityType_EntityId | Entity history |
| AuditEntry | IX_Audit_ChangedAt | Audit log paging |

### 2.5 Global Query Filters

`Employee` carries a global query filter on `Status == Active`. Every query that legitimately needs inactive employees — reports, audit history, department deletion checks — opts out explicitly with `IgnoreQueryFilters()`.

Without this, soft-deleted employees leak into headcounts, dropdowns, and dashboards through any query that forgets the predicate. Making the safe behaviour the default and the unsafe behaviour explicit is the correct polarity.

---

## 3. Authentication & Authorisation

### 3.1 Foundation

EMS uses the .NET 10 Blazor Web App Individual Accounts template as its authentication foundation, generated with:

```
dotnet new blazor --interactivity Server --auth Individual
```

This is deliberate, not a shortcut. The template supplies working solutions to problems that are easy to get wrong:

- **Authentication pages render as static SSR, not interactive.** `SignInManager` must write an authentication cookie, which requires a writable HTTP response. An Interactive Server component runs over a WebSocket connection and has none. Hand-rolled Blazor login pages fail on exactly this point.
- **`IdentityRevalidatingAuthenticationStateProvider`** re-checks the user's security stamp on a recurring interval, which is what makes deactivation and role change take effect on a live connection.
- **`IdentityRedirectManager`** performs redirects correctly from static SSR components.
- Antiforgery, cascading authentication state, and the account endpoint routes are wired correctly.

Unused template features are removed: self-registration, email confirmation, external logins, and two-factor authentication.

| Component | Configuration |
|---|---|
| User entity | `ApplicationUser : IdentityUser` — no business fields |
| Role entity | `IdentityRole`, seeded with Admin, Manager, Employee |
| Auth scheme | Cookie, 30-minute sliding expiry |
| Revalidation interval | 30 minutes |
| Default admin | `admin@ems.local`, password from configuration, `MustChangePassword = true` |

### 3.2 Two Enforcement Layers

Blazor route authorisation has two distinct entry points, and protecting only one leaves a hole.

```
Direct HTTP request (first load, refresh, deep link, bookmark)
  └─► Endpoint authorisation
      app.MapRazorComponents<App>()
         .RequireAuthorization()
         .AddInteractiveServerRenderMode();

Navigation within an established interactive session
  └─► Router authorisation
      <AuthorizeRouteView> inside <Router><Found>
      builder.Services.AddCascadingAuthenticationState();
```

Endpoint authorisation alone does not run when the user navigates client-side inside an open circuit. Router authorisation alone does not run before the circuit exists. Both are required, and both are covered by E2E tests.

A fallback policy requiring an authenticated user is registered so that a page missing an explicit attribute is closed, not open. Anonymous access is granted explicitly to the login page, the error page, and `/health`.

### 3.3 Authorisation Policies

| Policy | Roles | Description |
|---|---|---|
| CanManageEmployees | Admin | Create, edit, deactivate employees |
| CanManageDepartments | Admin | CRUD departments |
| CanApproveLeave | Admin | Approve/reject leave (subject to §3.5) |
| CanCorrectAttendance | Admin | Edit attendance records |
| CanViewSalary | Admin | See salary fields |
| CanManageHolidays | Admin | CRUD public holidays |
| CanViewAudit | Admin | Access audit log |
| CanRecoverAccounts | Admin | Reset passwords, unlock accounts |
| CanViewTeam | Admin, Manager | View department employees (scoped, §3.4) |
| CanGenerateReports | Admin, Manager | Export reports (scoped, §3.4) |
| CanClockInOut | Admin, Manager, Employee | Self clock in/out |
| CanRequestLeave | Admin, Manager, Employee | Submit own leave |
| CanEditOwnProfile | Admin, Manager, Employee | Edit own contact fields |

### 3.4 Scoping Is Not a Policy

Role policies answer "may this user reach this page". They do not answer "which rows may this user see". That second question is answered in the Application layer:

- `ICurrentUser` exposes the authenticated employee's `EmployeeId`, role, and — for a Manager — the set of department identifiers they manage.
- Every query method applies a scope predicate derived from `ICurrentUser`. A Manager's employee query is filtered to their departments before any identifier lookup.
- A scoped lookup for an out-of-scope identifier returns "not found". It does not return a forbidden result, because distinguishing the two confirms the record exists.
- **No service method accepts an employee identifier for an own-data operation.** `ClockInAsync()` takes no parameter; it reads the acting employee from `ICurrentUser`. This eliminates an entire class of object-level authorisation bugs by making them unrepresentable.

### 3.5 Separation of Duties

An Admin cannot act on their own leave request. The approval service compares the reviewer's employee identifier against the request's employee identifier and refuses a match. This is enforced in the Application layer, not the UI.

### 3.6 Forced Password Reset

`MustChangePassword` is surfaced as a claim on the authenticated principal at sign-in. An authorisation requirement rejects every page except the password-change page while the claim is present. Once the password changes, the security stamp refreshes, the principal is re-issued without the claim, and normal access resumes.

v2.0 enforced this with a per-page redirect in component initialisation, which is bypassable and easy to forget on a new page.

---

## 4. Key Architectural Decisions

Full context for each of these is in `Docs/decisions/`.

### 4.1 SQL Server, Two Hosts, One Provider

**Decision:** SQL Server as the only datastore, reached through `Microsoft.EntityFrameworkCore.SqlServer`. SQL Server Express LocalDB during local development; a SQL Server container under Docker Compose.

**Rationale:** One provider means one migration set and one set of translation behaviours. The two hosts differ only by connection string, so nothing in the model, the queries, or the migrations is conditional on where it runs.

| Context | Connection string |
|---|---|
| Local (`dotnet run`, `dotnet ef`) | `Server=(localdb)\MSSQLLocalDB;Database=EMS;Trusted_Connection=True;TrustServerCertificate=True` |
| Container | `Server=db,1433;Database=EMS;User Id=sa;Password=<from secret>;TrustServerCertificate=True` |

`TrustServerCertificate=True` accepts a self-signed development certificate. It is a development affordance and must not survive into any deployed configuration — a deployed environment presents a real certificate and validates it.

**Connection resiliency:**

```csharp
options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure());
```

This matters more under Compose than locally, because the application can start before the database container finishes initialising.

**The consequence that catches people:** with a retrying execution strategy, a user-initiated transaction must be wrapped in that strategy, or EF Core throws. Every explicit transaction in this codebase — leave approval, cancellation, email change, role change — uses the wrapper:

```csharp
var strategy = db.Database.CreateExecutionStrategy();
await strategy.ExecuteAsync(async () =>
{
    await using var tx = await db.Database.BeginTransactionAsync(ct);
    // …
    await tx.CommitAsync(ct);
});
```

Without the wrapper the retry cannot replay the whole transaction, so EF Core refuses rather than retrying part of it. See ADR-0009.

### 4.2 Money as Decimal, Duration as Integer Minutes

**Decision:** `Salary` is `decimal(18,2)`. `WorkedMinutes` is `int`.

**Rationale:** SQL Server has a true fixed-point decimal type, so money is stored exactly and orders, compares, and aggregates in SQL. `decimal` is also the conventional choice for currency in this stack, which matters for the next person reading the model.

Duration stays an integer count of minutes rather than a fractional hour count. Minutes are what the system actually measures — two timestamps subtracted — and "7.5 hours" is a formatting decision made at the edge. Storing the derived fractional value would introduce a rounding decision into the data.

Floating-point types are not used for either. See ADR-0010.

### 4.3 DbContext Factory, Not Scoped DbContext

**Decision:** `AddDbContextFactory<ApplicationDbContext>` and one short-lived context per operation.

**Rationale:** In Blazor Server, a scoped service is scoped to the **circuit**, which lives as long as the user's browser tab. A scoped `DbContext` would therefore live for hours, accumulating tracked entities, serving stale reads from its first-level cache, and throwing "a second operation was started on this context" whenever two component events overlap. It would also hold a pooled SQL Server connection open for the duration.

```csharp
await using var db = await _factory.CreateDbContextAsync(ct);
```

Every Application service takes `IDbContextFactory<ApplicationDbContext>` and creates a context per operation.

### 4.4 Time via TimeProvider

**Decision:** `TimeProvider` from the base class library, plus a small `SctClock` helper for the fixed UTC+4 conversion.

**Rationale:** `TimeProvider` is the platform abstraction, and `FakeTimeProvider` makes probation boundaries, hire anniversaries, and late-arrival thresholds deterministically testable. v2.0's custom service returned `DateTime` values with `Kind = Unspecified` from `utc.Add(offset)`, which is a reliable source of downstream misuse.

Seychelles observes no daylight saving, so a fixed offset is correct in perpetuity. The conversion is still centralised in one place so that this assumption has exactly one home if it ever changes.

Server local time is never read. `DateTime.Now` and `DateTime.Today` are banned by an analyzer rule.

### 4.5 Derived Attendance States

**Decision:** Store only real clock events. Derive `Absent`, `Weekend`, `Holiday`, `OnLeave`, `Late`, and `Present` at query time.

**Rationale:** v2.0 stored an `AttendanceStatus` on each record but had no mechanism to create records for days nobody clocked in — so `Absent`, `Holiday`, and `Weekend` were unreachable states, and an employee on approved leave would have shown as absent. Fixing that by materialising rows would require a nightly job whose output must stay consistent with leave approvals, holiday edits, and hire dates, forever.

Deriving instead removes three background jobs and the entire sync-bug class. The cost is a projection over a date range: 50 employees across a 31-day month is 1,550 rows assembled from three indexed queries. See ADR-0004.

### 4.6 Lazy Leave Balance Periods

**Decision:** Materialise the current balance period on first access, idempotently. No scheduled reset.

**Rationale:** A timer-based reset assumes the application is running on every employee's hire anniversary. This application runs in a container that is routinely stopped. Any employee whose anniversary fell during downtime would silently keep last period's depleted balance.

Lazy materialisation has no such failure mode: the balance is correct whenever it is next read, regardless of uptime. See ADR-0006.

### 4.7 Optimistic Concurrency on Balances

**Decision:** `LeaveBalance` and `LeaveRequest` carry a `RowVersion` property mapped to SQL Server's `rowversion` type.

```csharp
builder.Property(b => b.RowVersion).IsRowVersion();
```

**Rationale:** Approval reads a balance, checks remaining days, and writes a decrement. A transaction alone does not prevent two concurrent approvals from both reading the same starting value.

`rowversion` is maintained by the database on every update, so nothing depends on the application remembering to increment it — which an application-managed integer token does. A conflict raises `DbUpdateConcurrencyException`, which the service translates into a retryable `ConcurrencyConflict` result rather than an overdrawn balance.

### 4.8 Audit via SaveChanges Interceptor

**Decision:** A `SaveChangesInterceptor` inspects the `ChangeTracker` and writes `AuditEntry` rows in the same transaction.

**Scope:** `Employee`, `AttendanceRecord`, `LeaveRequest`, `LeaveBalance`, `Department`. Never Identity tables and never `AuditEntry` itself.

**Actor resolution:** The interceptor reads `ICurrentUser`, which returns null for background jobs, the seeder, and startup migrations. `AuditEntry.ChangedById` is nullable and `ActorDescription` carries a system label in that case. v2.0 made the actor column required, which would have made every background write fail.

**Redaction:** A field allow-list prevents password hashes, security stamps, and tokens from reaching the JSON payload.

**Security events** — login failure, lockout, password change, admin reset, unlock, role change — are written directly by the Identity-adjacent services, since they do not correspond to a tracked entity change.

### 4.9 In-Process Notification Publishing

**Decision:** A singleton publisher with per-recipient subscriptions, consumed by the notification bell component.

**Rationale:** Without a push mechanism, an unread badge only updates on navigation. A single-instance deployment needs nothing more than an in-memory channel. Components subscribe on initialisation and unsubscribe on disposal; the publisher holds weak references so a dropped circuit cannot leak.

This is explicitly not a distributed mechanism, and it is the first thing that must change if the application is ever scaled beyond one instance.

### 4.10 Catch-Up Background Jobs

**Decision:** Two hosted services — notification purge and missed-clock-out flagging — each driven by a `JobRun` watermark.

**Rationale:** "Run once every 24 hours" is a schedule, not a correctness guarantee, for an application that is not continuously running. Each job reads its watermark, processes every outstanding date, and advances it only on success. A job that has not run for a week catches up on the next start.

Both jobs are idempotent, so a re-run over an already-processed date changes nothing.

### 4.11 Report Delivery

**Decision:** Reports are delivered as a stream, either via `DotNetStreamReference` interop or an authenticated download endpoint.

**Rationale:** v2.0 returned `byte[]` from the report service and passed the array to JavaScript. Blazor Server marshals that over the SignalR connection, whose default maximum message size is 32 KB. Any real PDF would have failed.

The framework pattern for files under 250 MB is a `DotNetStreamReference` consumed as an `arrayBuffer` on the client. Reports are generated on demand into a stream; nothing is buffered as a full byte array in memory.

Download endpoints carry the same authorisation and scoping as the pages that link to them. An endpoint that returns a department report must re-verify the caller's scope; it cannot assume the UI restricted it.

### 4.12 Multi-Stage Docker Build

**Decision:** SDK image for build, chiselled ASP.NET runtime image for the final layer.

**Rationale:** The SDK image carries compilers and tooling with no runtime purpose. The chiselled runtime image contains no shell and no package manager, which removes most of the container's attack surface.

Consequences that follow from choosing chiselled, and are accepted:

- No `curl`, therefore no in-image `HEALTHCHECK`. Health checking moves to Compose, which probes the mapped port.
- No `apt-get` and no native database client to install — `Microsoft.Data.SqlClient` is fully managed.
- The image's predefined non-root user (`$APP_UID`) is used instead of a hand-created account.

The database runs in its own container from the official SQL Server image, which is not chiselled and is not ours to slim.

### 4.13 Separated CI/CD Workflows

**Decision:** `ci.yml` on push and pull request; `docker.yml` on version tags, gated on a green build.

**Rationale:** Every change needs fast feedback. Images should only be produced for intentional releases. The gate matters: v2.0's tag workflow built and pushed without running any test, so a tag on a broken commit would publish a broken image.

---

## 5. Application Layer Design

### 5.1 Interfaces

```csharp
public interface IApplicationDbContext        // implemented by Infrastructure
{
    DbSet<Employee> Employees { get; }
    DbSet<Department> Departments { get; }
    DbSet<AttendanceRecord> AttendanceRecords { get; }
    DbSet<LeaveRequest> LeaveRequests { get; }
    DbSet<LeaveBalance> LeaveBalances { get; }
    DbSet<PublicHoliday> PublicHolidays { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<AuditEntry> AuditEntries { get; }
    DbSet<JobRun> JobRuns { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    DatabaseFacade Database { get; }
}

public interface ICurrentUser
{
    Guid? EmployeeId { get; }
    string? Email { get; }
    bool IsAdmin { get; }
    bool IsManager { get; }
    IReadOnlySet<Guid> ManagedDepartmentIds { get; }
}
```

Application services: `IEmployeeService`, `IDepartmentService`, `IAttendanceService`, `ILeaveService`, `ILeaveBalanceService`, `IReportDataService`, `INotificationService`, `IHolidayService`, `IAuditQueryService`, `IBusinessDayCalculator`, `IAttendanceStateResolver`.

Infrastructure-only interfaces: `IReportRenderer` (QuestPDF/CsvHelper), `INotificationPublisher`, `IIdentityAccountService` (password reset, unlock, role sync).

**Every method takes a `CancellationToken`.** Blazor Server disposes circuits when tabs close, and a report query that keeps running after its consumer is gone is wasted work.

### 5.2 Result Type

```csharp
public enum ErrorCode
{
    None, NotFound, Forbidden, Validation, Conflict,
    BusinessRule, ConcurrencyConflict
}

public readonly record struct Error(ErrorCode Code, string Message);
public sealed record Result(bool IsSuccess, Error? Error);
public sealed record Result<T>(bool IsSuccess, T? Value, Error? Error);
```

An error code rather than a bare string, so that the UI can distinguish a conflict from a validation failure without matching on message text, and so that messages remain free to change.

### 5.3 Validation

Each command has a FluentValidation validator covering field-level rules. **Business rules that require database state — probation, balance sufficiency, overlap — live in the service, not the validator**, because they must be re-checked inside the transaction that commits the change. A validator that queries the database gives a false sense of atomicity.

### 5.4 Query Safety

Sort column names and filter fields arriving from the data grid are mapped through a static allow-list per entity. An unrecognised name falls back to the default sort rather than being interpolated into an expression. Page size is clamped to a maximum of 100.

---

## 6. UI Architecture

### 6.1 Layout

```
┌─────────────────────────────────────────────────┐
│  Top App Bar                    🔔 [User Menu]  │
├──────────┬──────────────────────────────────────┤
│  Side    │         Main Content Area            │
│  Nav     │         (Routed pages render here)   │
│  Menu    │                                      │
└──────────┴──────────────────────────────────────┘
```

### 6.2 Render Modes

| Area | Mode | Reason |
|---|---|---|
| Account pages (login, password change) | Static SSR | `SignInManager` needs a writable response |
| Everything else | Interactive Server | Rich interaction, single connection |
| Error page, health | Static SSR | No interactivity required |

### 6.3 Navigation Visibility by Role

| Menu Item | Admin | Manager | Employee |
|---|---|---|---|
| Dashboard | ✓ | ✓ | ✓ |
| Employees | ✓ (all) | ✓ (dept) | ✗ |
| Departments | ✓ | ✗ | ✗ |
| Attendance | ✓ (all) | ✓ (dept) | ✓ (own) |
| Leave | ✓ (manage) | ✓ (dept view) | ✓ (own) |
| Reports | ✓ | ✓ (dept) | ✗ |
| Holidays | ✓ | ✗ | ✗ |
| Audit Log | ✓ | ✗ | ✗ |
| My Profile | ✓ | ✓ | ✓ |

Menu visibility mirrors policy but does not implement it. Every item corresponds to a server-enforced policy.

### 6.4 Component Patterns

- **MudDataGrid** with `ServerData` for all tabular data, backed by `PagedResult<T>`.
- **MudDialog** for confirmations and edit forms.
- **MudChart** for dashboard trends. Its capabilities are limited; if the attendance trend proves inadequate, the fallback is a dedicated charting library, decided during Phase 5 rather than assumed now.
- **MudDateRangePicker** for report filtering.
- **MudSnackbar** for feedback.
- **MudBadge** on the notification bell.

All rendering uses standard component parameters. `MarkupString` is not used for any value that originated from user input.

### 6.5 Circuit Configuration

| Setting | Value | Reason |
|---|---|---|
| `DetailedErrors` | false outside Development | Exception detail is not for browsers |
| `DisconnectedCircuitMaxRetained` | 50 | Bounded memory for reconnection |
| `DisconnectedCircuitRetentionPeriod` | 3 minutes | Survives brief network loss |
| `JSInteropDefaultCallTimeout` | 1 minute | Fails fast on a dead client |
| `MaximumReceiveMessageSize` | 64 KB | Nothing large travels inbound; files stream out |

---

## 7. Error Handling Strategy

| Layer | Approach |
|---|---|
| Domain | Throw domain exceptions for invariant violations that should never occur through the UI |
| Application | Return `Result`/`Result<T>` with an `ErrorCode` for expected failures. Exceptions are for bugs, results are for outcomes |
| Infrastructure | Translate provider exceptions — unique constraint violations, concurrency conflicts, busy timeouts — into the corresponding `ErrorCode` |
| Web | An error boundary catches unhandled exceptions, logs them with a correlation identifier, and shows the user that identifier and nothing else |

The distinction that matters: a duplicate clock-in is an outcome and returns a result; a null dependency is a bug and throws.

---

## 8. Container Architecture

### 8.1 Application Image

```
┌────────────────────────────────────────┐
│  aspnet:10.0-noble-chiseled            │
│                                        │
│  /app/                                 │
│  ├── EMS.Web.dll                       │
│  └── ...                               │
│                                        │
│  User: $APP_UID (non-root, no shell)   │
│  Port: 8080                            │
└────────────────────────────────────────┘
```

The application image holds no data. Moving the database into its own container removed the volume-permission problem that a file-based store creates, where the data **directory** must be writable by the runtime user and a named volume can shadow build-time ownership.

### 8.2 Compose Topology

```
ems-app                          ems-db
 ├── Port 5000 → 8080             ├── Port 1433 → 1433  (dev convenience only)
 ├── depends_on: ems-db healthy   ├── Volume ems-data → /var/opt/mssql
 ├── Environment from .env        ├── SA password from .env
 └── Healthcheck on /health       └── Healthcheck via sqlcmd

ems-data (named volume) └── SQL Server data and log files
```

Three details that carry weight:

- **`depends_on` with `condition: service_healthy`.** SQL Server takes 15–25 seconds to accept connections. Without the health condition the application starts first, fails to migrate, and exits. The retrying execution strategy (§4.1) covers the remaining window.
- **Publishing port 1433 is a development affordance**, so that Visual Studio's SQL Server Object Explorer can attach to the container. It does not belong in a deployed configuration.
- **`ASPNETCORE_ENVIRONMENT` is `Production` in the image.** The development Compose file overrides it and supplies the seed password. v2.0 did the same thing silently; here it is stated, because it enables detailed errors and seeding.

---

## 9. CI/CD Architecture

### 9.1 Workflows

```
Push / PR ──► ci.yml
              ├── Job: build-and-test
              │   ├── Restore (locked mode)
              │   ├── Build (warnings as errors)
              │   ├── Format verification
              │   ├── Unit tests + coverage
              │   ├── Integration tests + coverage
              │   ├── E2E tests (Playwright)
              │   ├── Vulnerable package scan
              │   └── Upload artifacts
              └── Job: codeql (parallel)

Tag v*.*.* ──► docker.yml
               ├── Requires green ci.yml on the tagged commit
               ├── Build multi-stage image
               ├── Scan image for vulnerabilities
               └── Push to ghcr.io
```

Every third-party action is pinned to a commit SHA. Each job declares least-privilege permissions. A concurrency group cancels superseded runs on the same ref.

### 9.2 Artifacts

| Artifact | Produced by | Retention |
|---|---|---|
| Test results (.trx) | build-and-test | 30 days |
| Coverage report (HTML + Cobertura) | build-and-test | 30 days |
| Playwright traces (on failure) | build-and-test | 30 days |
| Docker image | docker | Permanent in ghcr.io |
| CodeQL alerts | codeql | GitHub Security tab |

---

## 10. Configuration

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=EMS;Trusted_Connection=True;TrustServerCertificate=True"
  },
  "AppSettings": {
    "TimeZoneOffsetHours": 4,
    "WorkDayStartHour": 8,
    "WorkDayEndHour": 16,
    "SessionTimeoutMinutes": 30,
    "SecurityStampRevalidationMinutes": 30,
    "NotificationRetentionDays": 30,
    "ProbationMonths": 3,
    "MaxPageSize": 100,
    "DefaultLeaveEntitlements": {
      "Annual": 21,
      "Sick": 10,
      "Maternity": 90,
      "Compassionate": 5
    },
    "Lockout": {
      "MaxFailedAttempts": 5,
      "LockoutDurationMinutes": 15
    },
    "RateLimit": {
      "LoginAttemptsPerMinute": 10
    },
    "SeedData": {
      "Enabled": false,
      "EmployeeCount": 15,
      "AttendanceHistoryDays": 30,
      "RandomizerSeed": 20260812
    }
  }
}
```

The committed connection string targets LocalDB and contains no credential — `Trusted_Connection=True` uses the developer's Windows identity. The container's connection string, which does carry a password, is supplied entirely by environment variable and never appears in a committed file.

No secret appears here. `Seed__AdminPassword` comes from environment variable or user-secrets, and startup fails outside Development if it is missing.

Configuration is bound to strongly-typed options with validation at startup, so a malformed value fails immediately rather than at first use.
