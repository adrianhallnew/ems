# EMS — Employee Management System: Implementation Guide

> **Version:** 3.1
> **Date:** August 19, 2026
> **Phases:** 11 (Phase 0 through Phase 10)
> **Supersedes:** v2.0 (August 12, 2026)
>
> **v3.1 adds a "Deviations recorded during execution" table** to Phase 2 and Phase 3. Where a
> phase's instructions and that table disagree, the table is what the code does and why.

---

## 0. How to Use This Guide

Each phase states its goal, the **skills to invoke**, the **Context7 lookups to perform before writing code**, the work itself, and a deliverable checklist.

Two rules apply throughout:

1. **Perform the Context7 lookups first.** This stack moved between v2.0 and v3.0 of these documents — bUnit renamed its core type, FluentValidation and QuestPDF advanced major versions, and the Blazor Identity template layout is version-specific. Writing from memory produces code that looks right and does not compile.
2. **Invoke the listed skill before starting the phase**, not after getting stuck. The skills encode conventions this guide assumes.

### 0.1 Changes from v2.0

| Change | Reason |
|---|---|
| SQL Server replaces SQLite | LocalDB locally, a SQL Server container in Docker, one EF Core provider throughout (ADR-0009) |
| "Before you start" block per phase | Each phase now states exactly what must be installed and running before it begins |
| Phase 0 added | Package versions, build settings, and central package management must be settled before any code exists |
| Phase 6 added | Authentication and authorisation were scattered through v2.0's Phase 2 and Phase 5 and consequently under-specified |
| Phase 10 added | Observability and accessibility had no owner |
| Repositories removed | See ADR-0003 |
| E2E host approach rewritten | v2.0's Playwright fixture could not have worked — see §Phase 7 |
| Dockerfile rewritten | v2.0's restore step referenced projects it never copied |
| Skills and Context7 lookups added per phase | New in v3.0 |

---

## 1. Prerequisites

| Tool | Version | Purpose | Needed from |
|---|---|---|---|
| .NET SDK | 10.0.100+ | Runtime and CLI | Phase 0 |
| Visual Studio 2026 with **"Data storage and processing"** | — | Ships SQL Server Express LocalDB **and** SQL Server Object Explorer | Phase 0 |
| Git | 2.x | Version control | Phase 0 |
| `dotnet-ef` global tool | 10.x | Migrations | Phase 2 |
| Docker Desktop | 27.x+ | Testcontainers, then containerisation | Phase 7 |
| PowerShell | 7.x | Playwright browser installation | Phase 7 |

```bash
dotnet --version                    # 10.0.x
git --version                       # 2.x
sqllocaldb info                     # expect MSSQLLocalDB
docker --version                    # 27.x+  (not needed until Phase 7)
```

### 1.1 Database Setup

**There is no database file to create.** LocalDB starts on demand and EF Core creates the `EMS` database on the first migration (Phase 2). Nothing is provisioned by hand.

If `sqllocaldb info` lists nothing, the Visual Studio component **"Data storage and processing"** is missing — that single component supplies both LocalDB and SQL Server Object Explorer. If the instance is listed but connections time out:

```
sqllocaldb start MSSQLLocalDB
```

### 1.2 Inspecting the Database

`View → SQL Server Object Explorer` in Visual Studio. `(localdb)\MSSQLLocalDB` appears under the SQL Server node. From there: browse schema, **View Data** for an editable grid, and **New Query** with IntelliSense. SSMS is not required.

The Compose database container (Phase 8) publishes port 1433 so the same tool can attach to it at `localhost,1433` with the SA credentials. That published port is a development convenience and is called out again in Phase 8 as something that does not belong in a deployed configuration.

---

## Phase 0 — Foundation & Version Truth

**Goal:** A solution that builds with zero warnings, all package versions centrally pinned, and no secret anywhere in source.

### Before you start

.NET 10 SDK · Git · Visual Studio with "Data storage and processing". Verify `sqllocaldb info` lists `MSSQLLocalDB`. Docker is **not** needed yet.

### Skills

`setup-local-sdk` · `directory-build-organization` · `convert-to-cpm` · `msbuild-modernization` · `msbuild-antipatterns` · `git-workflow-strategy` · `secrets-management`

### Context7 lookups

| Library | What to look up |
|---|---|
| `/dotnet/aspnetcore.docs` | .NET 10 project defaults; `global.json` SDK pinning |
| NuGet | Confirm every version in spec §4.1 is still current before pinning |
| `/dotnet/entityframework.docs` | SQL Server provider package name and current version |

### 0.1 Scaffold

```bash
mkdir EMS && cd EMS
git init                          # verify this lands at the repo root, not in a subfolder
dotnet new gitignore
dotnet new globaljson --sdk-version 10.0.102 --roll-forward latestFeature

# --format sln is required: .NET 10 defaults to the newer .slnx format, and
# every reference in these documents, the Dockerfile, and CI names EMS.sln.
dotnet new sln -n EMS --format sln

dotnet tool install --global dotnet-ef

# The SDK ships no xunit v3 template - `dotnet new xunit` is v2.
dotnet new install xunit.v3.templates

dotnet new classlib -n EMS.Domain         -o src/EMS.Domain         --no-restore
dotnet new classlib -n EMS.Application    -o src/EMS.Application    --no-restore
dotnet new classlib -n EMS.Infrastructure -o src/EMS.Infrastructure --no-restore

# --use-local-db is REQUIRED. Without it the Identity template wires the
# generated DbContext to SQLite. See Phase 6 and ADR-0005/0009.
dotnet new blazor -n EMS.Web -o src/EMS.Web \
  --interactivity Server --auth Individual --use-local-db --no-restore

dotnet new xunit3 -n EMS.UnitTests        -o tests/EMS.UnitTests
dotnet new xunit3 -n EMS.IntegrationTests -o tests/EMS.IntegrationTests
dotnet new xunit3 -n EMS.E2E.Tests        -o tests/EMS.E2E.Tests

# The classlib template leaves an empty Class1.cs in each project. Delete them.
rm src/EMS.Domain/Class1.cs src/EMS.Application/Class1.cs src/EMS.Infrastructure/Class1.cs

dotnet sln add src/EMS.Domain src/EMS.Application src/EMS.Infrastructure src/EMS.Web
dotnet sln add tests/EMS.UnitTests tests/EMS.IntegrationTests tests/EMS.E2E.Tests

dotnet add src/EMS.Application    reference src/EMS.Domain
dotnet add src/EMS.Infrastructure reference src/EMS.Application
dotnet add src/EMS.Web            reference src/EMS.Infrastructure

dotnet add tests/EMS.UnitTests        reference src/EMS.Domain src/EMS.Application src/EMS.Web
dotnet add tests/EMS.IntegrationTests reference src/EMS.Application src/EMS.Infrastructure
dotnet add tests/EMS.E2E.Tests        reference src/EMS.Web
```

> The web project is scaffolded with `--auth Individual`. Generating it plain and adding Identity later means hand-writing the account pages, the redirect manager, and the revalidating authentication state provider — the exact components that are easiest to get wrong. See ADR-0005.

### 0.1a What the Templates Actually Produce

Verified by running them, not assumed. Each of these differs from what the template names suggest:

| Observation | Consequence |
|---|---|
| The xunit3 template targets **net8.0** | Remove `TargetFramework` from all three test `.csproj` and let `Directory.Build.props` own it. Left alone, project references to net10.0 source projects fail with "incompatible targeted frameworks" |
| Its package is **`xunit.v3.mtp-v2`**, not `xunit.v3` | Pin that name in `Directory.Packages.props` |
| It writes `"test": { "runner": "Microsoft.Testing.Platform" }` into `global.json` | Keep it — this is the MTP adoption spec §4.3 calls for, arriving for free |
| The Blazor template pins EF and Identity packages at an **older patch** than current | Central Package Management overrides them; strip the inline `Version` attributes |
| It places `ApplicationDbContext`, `ApplicationUser`, and the initial Identity migration under **`src/EMS.Web/Data/`** | Correct for the template, wrong for this architecture. Phase 2 moves them to `EMS.Infrastructure` |
| It generates `Components/Account/` with the redirect manager and revalidating provider | Exactly what ADR-0005 wanted. Review, keep, and delete the out-of-scope pages in Phase 6 |

**Two template files fail a `TreatWarningsAsErrors` build** and must be fixed before Phase 0 can close:

- `Components/Account/IdentityNoOpEmailSender.cs` — CA1859: the field is typed `IEmailSender` but only ever holds a `NoOpEmailSender`. Change the field to the concrete type.
- `Data/Migrations/*_CreateIdentitySchema.cs` — IDE0161: block-scoped namespace. Migrations are generated output; exempt them in `.editorconfig` rather than editing them.

That the analyser gate catches template code on the first build is the gate working, not a reason to weaken it.

### 0.2 Directory.Build.props

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <Deterministic>true</Deterministic>
    <ContinuousIntegrationBuild
      Condition="'$(GITHUB_ACTIONS)' == 'true'">true</ContinuousIntegrationBuild>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);CS1591</NoWarn>
  </PropertyGroup>
</Project>
```

`TreatWarningsAsErrors` is what makes Phase 1's "builds with zero warnings" an enforced statement rather than an aspiration.

### 0.3 Central Package Management

`Directory.Packages.props` holds every version. Project files reference packages without versions.

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="10.0.11" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.11" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.11" />
    <PackageVersion Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.0.11" />
    <PackageVersion Include="FluentValidation" Version="12.1.1" />
    <PackageVersion Include="FluentValidation.DependencyInjectionExtensions" Version="12.1.1" />
    <PackageVersion Include="QuestPDF" Version="2026.7.2" />
    <PackageVersion Include="CsvHelper" Version="33.1.0" />
    <PackageVersion Include="Bogus" Version="35.6.5" />
    <PackageVersion Include="MudBlazor" Version="9.8.0" />
    <!-- Name comes from the xunit3 template, which uses the MTP-v2 variant. -->
    <PackageVersion Include="xunit.v3.mtp-v2" Version="3.2.2" />
    <PackageVersion Include="NSubstitute" Version="6.2.0" />
    <PackageVersion Include="Shouldly" Version="4.3.0" />
    <PackageVersion Include="bunit" Version="2.9.0" />
    <PackageVersion Include="Microsoft.Playwright" Version="1.62.0" />
    <PackageVersion Include="Testcontainers.MsSql" Version="4.13.0" />
    <PackageVersion Include="Microsoft.Extensions.TimeProvider.Testing" Version="10.9.0" />
    <PackageVersion Include="coverlet.collector" Version="10.0.1" />
  </ItemGroup>
</Project>
```

**FluentAssertions is absent deliberately.** Its current line is commercially licensed. EMS uses Shouldly. See ADR-0007.

**MediatR and AutoMapper are absent deliberately.** Both moved to commercial licensing. The `Commands/` and `Queries/` folders are organisational only; they do not imply a mediator.

Enable lock files and package source mapping:

```xml
<!-- Directory.Build.props -->
<PropertyGroup>
  <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
</PropertyGroup>
```

```xml
<!-- nuget.config -->
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
```

### 0.4 .editorconfig with Severities

The critical detail: `dotnet format --verify-no-changes` only enforces rules that carry a severity. v2.0's `.editorconfig` set preferences without severities, so its CI format check verified whitespace and nothing else.

```ini
root = true

[*]
indent_style = space
indent_size = 4
end_of_line = crlf
charset = utf-8
trim_trailing_whitespace = true
insert_final_newline = true

[*.{cs,csx}]
csharp_using_directive_placement = outside_namespace:warning
dotnet_sort_system_directives_first = true
csharp_prefer_braces = true:warning
csharp_style_namespace_declarations = file_scoped:warning
dotnet_style_require_accessibility_modifiers = for_non_interface_members:warning

# Culture-dependent parsing and formatting is the same class of bug as
# reading server local time. See ADR-0008.
dotnet_diagnostic.CA1305.severity = warning

# SQL injection via string concatenation is never acceptable here.
dotnet_diagnostic.CA2100.severity = error

# Migrations are generated output wherever they live. The glob is deliberately
# not tied to one project: the Identity template puts its initial migration
# under EMS.Web, and Phase 2 moves it to EMS.Infrastructure.
[**/Migrations/*.cs]
generated_code = true
dotnet_analyzer_diagnostic.severity = none
dotnet_diagnostic.IDE0161.severity = none

[*.{json,yml,yaml,csproj,props,targets}]
indent_size = 2
```

**Naming rule ordering is load-bearing.** Rules are evaluated in file order and the first match wins. A single "private fields use `_camelCase`" rule flags every `private const` and `private static readonly` field, including the template's own correctly-named ones. Declare the PascalCase rules for `const` and `static readonly` *before* the underscore rule. The committed `.editorconfig` shows the full three-rule form.

**IDE1006 has no Fix All provider**, so `dotnet format` reports naming violations but cannot repair them. They are fixed by hand, or by correcting the rule when the rule is what is wrong.

**`end_of_line` is `crlf`, not `lf`.** The .NET templates generate CRLF on Windows, so `crlf` matches what is already committed. Setting `lf` instead would be defensible, but only if the whole tree is converted at once — otherwise every subsequently authored file fails `--verify-no-changes` with one ENDOFLINE error per line. Whichever is chosen, `dotnet format` (without `--verify-no-changes`) repairs it in one pass.

### 0.5 Secrets

```bash
cd src/EMS.Web
dotnet user-secrets init
dotnet user-secrets set "Seed:AdminPassword" "<a strong local password>"
```

No credential is committed. `.gitignore` covers `.env`, `*.db`, `*.db-wal`, `*.db-shm`.

### Deliverable Checklist

- [ ] `git init` ran at the repo root — check `.git` is beside `EMS.sln`, not inside a subfolder
- [ ] Solution builds with zero warnings under `TreatWarningsAsErrors`
- [ ] `Directory.Build.props`, `Directory.Packages.props`, `nuget.config`, `global.json` in place
- [ ] `dotnet restore --locked-mode` succeeds; all 7 `packages.lock.json` committed
- [ ] No package reference carries an inline version
- [ ] No test project overrides `TargetFramework`
- [ ] `.editorconfig` severities present; `dotnet format --verify-no-changes` exits 0
- [ ] Placeholder `Class1.cs` files deleted
- [ ] No secret in source; user-secrets configured

**Expect the first restore to take ten minutes or more.** It pulls MudBlazor, Playwright, Testcontainers, and bUnit against a cold cache and writes seven lock files. Subsequent restores are seconds. If you pipe the output through anything that buffers, you will see nothing until it finishes — watch for `packages.lock.json` files appearing instead.

---

## Phase 1 — Domain Layer

**Goal:** Entities, enums, and domain exceptions. Zero package references.

### Before you start

Nothing beyond Phase 0. No database connection is required — this phase writes plain C# classes.

### Skills

`domain-driven-design` · `architect-review` · `migrate-nullable-references`

### Context7 lookups

| Library | What to look up |
|---|---|
| `/dotnet/aspnetcore.docs` | `TimeProvider` API surface; `DateOnly`/`TimeOnly` usage |
| `/dotnet/entityframework.docs` | Which CLR types the SQL Server provider maps natively, and their default precision |

### 1.1 Enumerations

```csharp
public enum EmployeeRole { Admin, Manager, Employee }
public enum EmployeeStatus { Active, Inactive }
public enum ContractType { FullTime, PartTime, Contract, Intern }
public enum LeaveType { Annual, Sick, Maternity, Unpaid, Compassionate }
public enum LeaveStatus { Pending, Approved, Rejected, Cancelled }
public enum AuditAction { Created, Updated, Deleted, StatusChanged, SecurityEvent }
public enum HolidayRule { FixedDate, EasterRelative }

// Derived at read time only — never persisted. See ADR-0004.
public enum AttendanceState { NotEmployed, Weekend, Holiday, OnLeave, Present, Late, Absent }
```

`AttendanceState` lives in Domain but has no column. Its absence from the data model is the point.

### 1.2 Base Types

```csharp
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
}

public interface IAuditableEntity
{
    DateTime CreatedAt { get; set; }
    DateTime UpdatedAt { get; set; }
}
```

`Guid.CreateVersion7()` produces time-ordered identifiers. Random GUIDs as primary keys fragment the index on every insert; version 7 values append.

Timestamps are set by an interceptor (Phase 2), never by hand in a service. Hand-set timestamps get forgotten on exactly the code path nobody tested.

### 1.3 Entities

Create in foreign-key dependency order: `Department` → `Employee` → `AttendanceRecord` → `LeaveRequest` → `LeaveBalance` → `PublicHoliday` → `Notification` → `AuditEntry` → `JobRun`.

Follow `architecture.md` §2.2 exactly. The type choices that matter:

| Field | Type | Reason |
|---|---|---|
| `Employee.Salary` | `decimal` | Exact fixed-point money; mapped to `decimal(18,2)` in Phase 2 (ADR-0010) |
| `AttendanceRecord.WorkedMinutes` | `int?` | Minutes are what the system measures; fractional hours are a display concern |
| `RowVersion` on `LeaveBalance`, `LeaveRequest` | `byte[]` | Mapped to SQL Server `rowversion` in Phase 2 |
| All dates | `DateOnly` | Calendar dates carry no time and no zone |
| All instants | `DateTime` (UTC) | Converted for display only |
| `LeaveBalance.Remaining` | computed C# property | Not a column; derived from `Entitlement - Used` |

No floating-point type appears anywhere in the model. `double` and `float` are wrong for both money and durations.

### 1.4 Domain Behaviour

Keep entities mostly anemic, with two exceptions that belong on the entity because they are pure functions of its own state:

```csharp
public bool IsInProbation(DateOnly today, int probationMonths) =>
    today < HireDate.AddMonths(probationMonths);

public (DateOnly Start, DateOnly End) PeriodFor(DateOnly today) { /* hire anniversary window */ }
```

Both take the current date as a parameter rather than reading a clock, which makes them trivially testable and keeps Domain free of dependencies.

### Deliverable Checklist

- [ ] 9 entity classes matching `architecture.md` §2.2
- [ ] 8 enum types including `AttendanceState`
- [ ] `BaseEntity` with version-7 GUIDs, `IAuditableEntity`
- [ ] Domain exception types
- [ ] `EMS.Domain.csproj` has zero `PackageReference` elements
- [ ] No `double` or `float` anywhere in the project
- [ ] No `DateTime.Now` or `DateTime.Today` anywhere
- [ ] Builds with zero warnings

---

## Phase 2 — Infrastructure: Database

**Goal:** DbContext, configurations, interceptors, SQL Server setup, migrations, seeding.

### Before you start

- `sqllocaldb info` lists `MSSQLLocalDB`
- `dotnet tool install --global dotnet-ef` (this is the phase that first needs it)
- Nothing else. The `EMS` database does not exist yet and must not be created by hand — §2.6 creates it.

### Skills

`optimizing-ef-core-queries` · `secrets-management`

### Context7 lookups

| Library | What to look up |
|---|---|
| `/dotnet/entityframework.docs` | SQL Server provider: `IsRowVersion`, `HasPrecision`, `EnableRetryOnFailure`, execution strategies with explicit transactions |
| `/dotnet/entityframework.docs` | Global query filters; `SaveChangesInterceptor`; `IDbContextFactory` |

### 2.1 DbContext

```csharp
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
{
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<LeaveBalance> LeaveBalances => Set<LeaveBalance>();
    public DbSet<PublicHoliday> PublicHolidays => Set<PublicHoliday>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<JobRun> JobRuns => Set<JobRun>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);   // Identity configuration first
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
```

### 2.2 Registration

```csharp
services.AddDbContextFactory<ApplicationDbContext>((sp, options) =>
    options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure())
           .AddInterceptors(
               sp.GetRequiredService<AuditableEntityInterceptor>(),
               sp.GetRequiredService<AuditSaveChangesInterceptor>()));
```

**The order of those two interceptors is load-bearing**, and v3.0 of this guide had it backwards.
Interceptors run in registration order, and the audit interceptor serialises each entity as it finds
it. Register the audit interceptor first and every Created audit row records `CreatedAt` and
`UpdatedAt` as `0001-01-01`, because the stamping has not happened yet. The timestamps go on first.

**`lifetime: ServiceLifetime.Scoped` is required on that call.** The default is singleton, which resolves its dependencies from the root provider. The audit interceptor needs the scoped `ICurrentUser`, so the default fails with `Cannot resolve scoped service '...' from root provider` — including at design time, where it stops `dotnet ef migrations add` from finding the `DbContext` at all. Register `AuditableEntityInterceptor` as a singleton (it depends only on `TimeProvider`) and `AuditSaveChangesInterceptor` as scoped.

**A factory, not `AddDbContext`.** In Blazor Server a scoped service lives for the whole circuit — hours, not milliseconds. A scoped `DbContext` accumulates tracked entities, serves stale reads, throws "a second operation was started on this context" when two component events overlap, and holds a pooled connection open the whole time. Every service creates a context per operation and disposes it.

**`EnableRetryOnFailure` has a consequence** that surfaces in Phase 4: a retrying execution strategy refuses to run a user-initiated transaction unless the whole transaction is wrapped in the strategy. It is enabled here, and Phase 4 §4.2 shows the wrapper every explicit transaction must use.

### 2.3 Connection Strings

| Context | Value |
|---|---|
| Local | `Server=(localdb)\MSSQLLocalDB;Database=EMS;Trusted_Connection=True;TrustServerCertificate=True` |
| Container | `Server=db,1433;Database=EMS;User Id=sa;Password=<from environment>;TrustServerCertificate=True` |

Only the local string is committed, and it carries no credential — `Trusted_Connection=True` authenticates with the developer's Windows identity. The container string carries a password and is supplied entirely by environment variable.

`TrustServerCertificate=True` accepts a self-signed development certificate. It is a development affordance. A deployed environment presents a real certificate and validates it, and this document should be treated as wrong the moment it is copied into one.

### 2.4 Configurations

One class per entity in `Data/Configurations/`.

```csharp
// EmployeeConfiguration
builder.HasIndex(e => e.UserId).IsUnique();
builder.HasIndex(e => e.DepartmentId);
builder.HasIndex(e => e.Status);
builder.Property(e => e.ContractType).HasConversion<string>().HasMaxLength(20);
builder.Property(e => e.Role).HasConversion<string>().HasMaxLength(20);
builder.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
builder.Property(e => e.Salary).HasPrecision(18, 2);
builder.HasOne<Department>().WithMany()
       .HasForeignKey(e => e.DepartmentId)
       .OnDelete(DeleteBehavior.Restrict);

// Soft delete is the default; opt out explicitly where history is needed
builder.HasQueryFilter(e => e.Status == EmployeeStatus.Active);

// LeaveBalanceConfiguration
builder.HasIndex(b => new { b.EmployeeId, b.LeaveType, b.PeriodStart }).IsUnique();
builder.Property(b => b.RowVersion).IsRowVersion();

// AttendanceRecordConfiguration
builder.HasIndex(a => new { a.EmployeeId, a.Date }).IsUnique();
builder.HasIndex(a => new { a.Date, a.IsFlagged });
```

Three points:

- **Enums are stored as strings**, with an explicit `HasMaxLength`. Integer storage makes the database unreadable and breaks silently if anyone reorders an enum. Without the length, EF maps the column as `nvarchar(max)`, which cannot be indexed.
- **`HasPrecision(18, 2)`** rather than `HasColumnType("decimal(18,2)")` — the same result, expressed in provider-neutral terms.
- **`IsRowVersion()`** maps to SQL Server's `rowversion`, which the database maintains automatically on every update. Nothing depends on the application remembering to increment it.

Every string property gets an explicit `HasMaxLength`. The default is `nvarchar(max)`, which is unindexable and needlessly wide.

The single exception is `AuditEntry.ChangedFields`, which holds a JSON payload of unbounded shape and is never filtered or indexed on. It stays `nvarchar(max)` deliberately, and the configuration says so at the property.

### 2.5 Interceptors

**`AuditableEntityInterceptor`** sets `CreatedAt` and `UpdatedAt` on `IAuditableEntity` from `TimeProvider`.

**`AuditSaveChangesInterceptor`** writes `AuditEntry` rows in the same transaction:

```csharp
public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
    DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct)
{
    var context = eventData.Context!;
    var entries = context.ChangeTracker.Entries()
        .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
        .Where(e => e.Entity is Employee or AttendanceRecord or LeaveRequest
                              or LeaveBalance or Department);

    foreach (var entry in entries)
    {
        // Serialise before/after for changed properties only.
        // Skip any property on the redaction list.
        // Actor: _currentUser.EmployeeId may be null — that is expected
        //        for background jobs, the seeder, and startup migrations.
        context.Add(BuildAuditEntry(entry));
    }

    return base.SavingChangesAsync(eventData, result, ct);
}
```

Two requirements that v2.0 missed:

- `AuditEntry.ChangedById` is **nullable**. A required actor column would make every background write throw.
- A **redaction allow-list** prevents password hashes, security stamps, and tokens from reaching the JSON payload.

The interceptor never audits `AuditEntry` itself, or the loop is infinite.

### 2.6 Migrations

```bash
dotnet add src/EMS.Web package Microsoft.EntityFrameworkCore.Design

dotnet ef migrations add InitialCreate \
  --project src/EMS.Infrastructure \
  --startup-project src/EMS.Web \
  --output-dir Data/Migrations

dotnet ef database update --project src/EMS.Infrastructure --startup-project src/EMS.Web
```

**Migrations are applied at startup**, before seeding:

```csharp
await db.Database.MigrateAsync(ct);
```

v2.0's `Program.cs` never called this, so a container starting on a fresh volume would have found no schema.

**This is what creates the `EMS` database.** LocalDB starts on demand, EF Core issues the `CREATE DATABASE`, and the schema follows. Nothing is provisioned by hand — there is no file to create and no SSMS step.

Review the generated SQL before accepting any migration:

```bash
dotnet ef migrations script --idempotent --output migration.sql
```

Watch in particular for a migration that drops and recreates a column holding data, and for an index EF adds silently on a new foreign key.

### 2.7 Seeding

Idempotent, deterministic, and off by default.

```csharp
public static async Task SeedAsync(IServiceProvider sp, CancellationToken ct)
{
    // 0. Return immediately if Employees already has rows
    // 1. Roles: Admin, Manager, Employee
    // 2. Admin user — password from configuration; fail outside Development if absent
    // 3. Departments (5)
    // 4. Employees (15) via Bogus with a fixed Randomizer.Seed
    // 5. One Manager per department, assigned as Department.ManagerId
    // 6. Public holidays for the current and next year (via HolidayService)
    // 7. 30 days of attendance events, incl. late arrivals and one missed clock-out
    // 8. Leave requests across all four statuses, with matching balance rows
}
```

`Randomizer.Seed = new Random(20260812)` makes the dataset reproducible, which matters because integration tests assert against it.

### Deliverable Checklist

- [ ] `ApplicationDbContext` implements `IApplicationDbContext`
- [ ] Registered via `AddDbContextFactory`, not `AddDbContext`
- [ ] `EnableRetryOnFailure` configured
- [ ] `EMS` database created by `dotnet ef database update`, visible in SQL Server Object Explorer
- [ ] `Salary` mapped with `HasPrecision(18, 2)`
- [ ] Every string property has an explicit `HasMaxLength`
- [ ] Global query filter on `Employee`
- [ ] `IsRowVersion()` on `LeaveBalance` and `LeaveRequest`
- [ ] Both interceptors registered; audit actor nullable; redaction list applied
- [ ] `AddDbContextFactory` uses `ServiceLifetime.Scoped`
- [ ] `RequireConfirmedAccount = false` — email is out of scope, so a true value locks out every admin-provisioned account
- [ ] `PasswordPolicyValidator` registered: max length, breached-password blocklist, no email local-part in password
- [ ] `EmailFormatValidator` registered: Identity checks uniqueness but never that an email parses
- [ ] Initial migration created, reviewed, and applied at startup
- [ ] Seeder idempotent, deterministic, disabled by default

### Deviations recorded during execution

Everything below differs from this guide as originally written. Each is deliberate.

| Deviation | Reason |
|---|---|
| `ClientSetNull` replaces `SetNull` on the five secondary employee foreign keys | SQL Server error 1785 — several `ON DELETE SET NULL` paths into one parent table. ADR-0011 |
| EF Core's query-filter/required-navigation warning is suppressed in `AddInfrastructure` | The filter belongs on `Employee` alone; the dependents must stay readable for departed employees. ADR-0012 |
| A unique index was added on `Employees.Email` | Spec §3.1.2 requires it; the §2.4 index table omitted it |
| `.AddRoles<IdentityRole>()` added to the Identity registration | `RoleManager` is otherwise unregistered, and the seeder cannot provision roles without it |
| `ICurrentUser` gained `ActorDescription` | The audit interceptor needs an actor label when `EmployeeId` is null (spec §3.8.1) |
| `IApplicationDbContext` and `ICurrentUser` were written in Phase 2, not Phase 3 | The DbContext implements the first and the audit interceptor depends on the second. A `SystemCurrentUser` stub ships with them; Phase 4 replaces it with the claims-backed implementation |
| Seeding covers roles, admin, departments, employees and managers only | §2.7's remaining steps — holidays, 30 days of attendance, leave requests with matching balances — depend on `HolidayService` and the leave rules, which arrive in Phase 4. Seeding them by hand here would encode those rules twice |
| `RequireConfirmedAccount = false` shipped here; `PasswordPolicyValidator` and `EmailFormatValidator` did not | The flag is one line and blocks every admin-provisioned login without it. The two validators are auth hardening and belong with the rest of it in Phase 6 |
| Connection string is `Database=EMS` with `TrustServerCertificate=True` and no `MultipleActiveResultSets` | Matches `architecture.md` §4.1. MARS is pointless when every service creates one short-lived context per operation |
| Seeded employees share the admin's temporary password, all with `MustChangePassword = true` | Development-only data behind a flag that defaults to false. Spec §5.1 fixes the admin account's password source and says nothing about the generated employees |
| Employee names are Seychellois Creole and addresses use Mahé districts and street names | Spec §5.2 asks for "Seychelles-contextual data"; this is what that means concretely |
| `AuditEntry.ChangedFields` is the one string column with no `HasMaxLength` | A JSON payload of unbounded shape, never filtered or indexed on |
| The audit payload renders dates with the round-trip `"O"` format | The invariant default writes `01/01/1985`, which cannot be parsed back without guessing the field order |

---

## Phase 3 — Application Layer

**Goal:** Interfaces, DTOs, validators, and the calculation services.

### Before you start

Nothing beyond Phase 2. A working database helps for spot checks but is not required — this phase writes contracts and pure logic.

### Skills

`architect-review` · `test-driven-development`

### Context7 lookups

| Library | What to look up |
|---|---|
| FluentValidation | **v12** API — resolve via `resolve-library-id`. Confirm `AbstractValidator`, async validators, and DI registration. v11 examples will not all apply |
| `/dotnet/aspnetcore.docs` | `TimeProvider` in dependency injection |

### 3.1 Contracts

Per `architecture.md` §5.1. Two rules with no exceptions:

**Every method takes a `CancellationToken`.**

**No method accepts an employee identifier for an own-data operation.**

```csharp
public interface IAttendanceService
{
    Task<Result> ClockInAsync(CancellationToken ct);      // acting employee from ICurrentUser
    Task<Result> ClockOutAsync(CancellationToken ct);
    Task<AttendanceTodayDto?> GetTodayAsync(CancellationToken ct);
    Task<PagedResult<AttendanceDayDto>> GetRecordsAsync(AttendanceFilter filter, CancellationToken ct);
    Task<Result> CorrectRecordAsync(CorrectAttendanceCommand command, CancellationToken ct);
}
```

The signature is the control. `ClockInAsync(Guid employeeId)` invites a caller to pass someone else's identifier, and no amount of review reliably catches every call site. `ClockInAsync(CancellationToken)` makes the bug unrepresentable.

### 3.2 Result Types

```csharp
public enum ErrorCode { None, NotFound, Forbidden, Validation, Conflict, BusinessRule, ConcurrencyConflict }
public readonly record struct Error(ErrorCode Code, string Message);
public sealed record Result(bool IsSuccess, Error? Error)
{
    public static Result Success() => new(true, null);
    public static Result Fail(ErrorCode code, string message) => new(false, new Error(code, message));
}
public sealed record Result<T>(bool IsSuccess, T? Value, Error? Error);
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
```

### 3.3 Time

```csharp
public sealed class SctClock(TimeProvider time)
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(4);

    public DateTimeOffset UtcNow => time.GetUtcNow();
    public DateTimeOffset Now => time.GetUtcNow().ToOffset(Offset);
    public DateOnly Today => DateOnly.FromDateTime(Now.DateTime);
    public DateOnly DateOf(DateTime utcInstant) =>
        DateOnly.FromDateTime(new DateTimeOffset(utcInstant, TimeSpan.Zero).ToOffset(Offset).DateTime);
}
```

`DateTimeOffset` throughout, so no value ever carries an ambiguous `Kind`. `SctClock.Today` is the only source of "today" in the application, which is what makes the day-boundary rule in spec §3.3.3 enforceable rather than aspirational.

### 3.4 Validation

Field-level rules only. Anything requiring database state belongs in the service, inside the transaction.

```csharp
public class SubmitLeaveValidator : AbstractValidator<SubmitLeaveCommand>
{
    public SubmitLeaveValidator(SctClock clock)
    {
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate);
        RuleFor(x => x.StartDate).GreaterThanOrEqualTo(_ => clock.Today)
            .WithMessage("Leave cannot be backdated.");
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
```

Balance sufficiency and overlap are **not** here. A validator that queries the database creates a check-then-act gap between validation and commit.

### 3.5 Business Day Calculator

```csharp
public async Task<int> CountBusinessDaysAsync(DateOnly start, DateOnly end, CancellationToken ct)
{
    await using var db = await _factory.CreateDbContextAsync(ct);

    var holidays = await db.PublicHolidays
        .Where(h => h.Date >= start && h.Date <= end)
        .Select(h => h.Date)
        .ToHashSetAsync(ct);          // set, not list — this is probed once per day in the range

    var count = 0;
    for (var date = start; date <= end; date = date.AddDays(1))
    {
        if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
        if (holidays.Contains(date)) continue;
        count++;
    }
    return count;
}
```

### 3.6 Attendance State Resolver

The replacement for the stored status column (ADR-0004). Given an employee set and a date range, it issues three indexed queries — attendance records, holidays, approved leave — and projects one `AttendanceDayDto` per employee per date, applying the resolution order in spec §3.3.7.

Everything that reads attendance — dashboard, records grid, monthly report — goes through this one component, so the rules cannot drift between screens.

### 3.7 Easter Calculator

Pure function, no dependencies, in `Holidays/`:

```csharp
public static DateOnly EasterSunday(int year)  // anonymous Gregorian computus
```

Good Friday is Easter − 2, Easter Saturday − 1, Easter Monday + 1, Corpus Christi + 60. Test against known values for at least ten years, including a leap year.

### Deliverable Checklist

- [ ] All service interfaces defined, every method with `CancellationToken`
- [ ] No interface method takes an employee identifier for an own-data operation
- [ ] `Result`, `Result<T>`, `PagedResult<T>`, `ErrorCode`
- [ ] `SctClock` over `TimeProvider`; no other source of "now"
- [ ] Validators for every command, field-level only
- [ ] `BusinessDayCalculator`, `AttendanceStateResolver`, `EasterCalculator`
- [ ] Sort/filter allow-lists per entity; page size clamped
- [ ] `EMS.Application.csproj` references no provider package — no `Microsoft.EntityFrameworkCore.SqlServer`, no `Microsoft.Data.SqlClient`

### Deviations recorded during execution

| Deviation | Reason |
|---|---|
| Services take `IApplicationDbContextFactory`, not `IDbContextFactory<ApplicationDbContext>` as §3.5 shows | The sample as written cannot compile: naming the concrete context in Application breaks ADR-0003. Application declares the port, Infrastructure adapts EF Core's factory to it, and `IApplicationDbContext` became disposable so the `await using` shape survives. ADR-0013 |
| `AttendanceStateResolver` split into a pure `AttendanceStateRules` plus a query-issuing resolver | The seven-step resolution order of spec §3.3.7 is the part most worth testing, and the split makes it testable with no database. 17 unit tests now cover it, including every precedence pair |
| `BusinessDayCalculator` split the same way, into `BusinessDayRules` plus the holiday query | Same reason. An off-by-one here is a balance error, not a display error |
| `SctClock` reads `AppSettings.TimeZoneOffsetHours` rather than the private `const` in §3.3 | The validated options type already carries the value, so a second definition would be a second place to be wrong. It also gained `TimeOf` and a `DateTimeOffset` overload of `DateOf`, both needed by the state resolver |
| `HolidayGenerator` merges observances that land on the same date into one entry with a combined name | `PublicHoliday.Date` is uniquely indexed and spec §3.7.1 calls for exactly this. Corpus Christi is Easter + 60 and can fall on Liberation Day, so the case is reachable rather than theoretical |
| Employee reads use two projections — `EmployeeDetailDto` without salary, `EmployeeAdminDetailDto` with it | Spec §2.5.6 requires the value to be absent from a non-Admin projection, not blanked in one |
| A `PageRequest` base record carries paging and sorting for every filter | Not in the guide. It gives the clamp and the allow-list one shape to work against instead of six |
| `.editorconfig` exempts `Common/Models/Result.cs` from CA1000 and CA1716 | Both rules object to the contract in `architecture.md` §5.2 rather than to a defect: `Result<T>.Success` is a static member on a generic type, and `Error` collides with a Visual Basic keyword in a C#-only solution. Scoped to the single file |
| Three interface parameters are `startDate`/`endDate` rather than `from`/`to`/`end` | CA1716 flags all three as reserved keywords. Renaming beat suppressing |
| Feature folders hold their files flat rather than in `Commands/`, `Queries/` and `Validators/` subfolders | Seven files per folder do not need three subdirectories. The `architecture.md` §1 comment describes what each folder contains, which is still accurate |
| Unit tests ship with this phase | The three calculators are pure logic, so testing them costs little and every later phase builds on their correctness. 62 new tests, 75 in total |
| Security-event writing lives in `ISecurityEventWriter`, not on `IAuditQueryService` | The audit log is read-only to the application (spec §3.8.4). One narrow hand-written path exists for events Identity produces, and it gets its own port rather than a write method on a read service |
| An architecture test asserts the layer boundary | `architecture.md` §1.1 says the boundary "is testable: an architecture test asserts…". It now is one — `tests/EMS.UnitTests/Architecture/LayerBoundaryTests.cs` checks both the compiled references and `packages.lock.json` |
| `AppSettings` implements `IValidatableObject` to validate its nested sections | `ValidateDataAnnotations()` does not recurse into complex properties, so the `[Range]` attributes on `Lockout`, `RateLimit` and `SeedData` were decoration with no effect |

---

## Phase 4 — Services, Jobs & Reports

**Goal:** Implement the business services, the two background jobs, and report generation.

### Before you start

Nothing beyond Phase 2. The database must be migrated, since this phase is where queries first run for real.

### Skills

`analyzing-dotnet-performance` · `optimizing-ef-core-queries` · `owasp-security-check`

### Context7 lookups

| Library | What to look up |
|---|---|
| QuestPDF | **2026.x** API — resolve via `resolve-library-id`. The fluent API changed since the 2024 releases assumed by v2.0 |
| CsvHelper | `CsvConfiguration.InjectionOptions` — required for spec §3.6.5 |
| `/dotnet/aspnetcore.docs` | `BackgroundService`, `PeriodicTimer`, scope creation inside hosted services |
| `/dotnet/entityframework.docs` | **Execution strategies with user-initiated transactions** — required before writing §4.2 |
| `/dotnet/entityframework.docs` | `ExecuteDeleteAsync`; handling `DbUpdateConcurrencyException` |

### 4.1 Build Order

`SctClock` → `CurrentUser` → `BusinessDayCalculator` → `HolidayService` → `DepartmentService` → `EmployeeService` → `AttendanceStateResolver` → `AttendanceService` → `LeaveBalanceAccessor` → `LeaveService` → `NotificationService` → `ReportDataService` → `ReportRenderers`.

### 4.2 Every Explicit Transaction Needs the Execution Strategy

Phase 2 enabled `EnableRetryOnFailure`. A retrying execution strategy will not run a user-initiated transaction directly — it throws, because it cannot replay half a transaction on retry. The whole transaction must be handed to the strategy so a retry replays all of it:

```csharp
await using var db = await _factory.CreateDbContextAsync(ct);
var strategy = db.Database.CreateExecutionStrategy();

await strategy.ExecuteAsync(async () =>
{
    await using var tx = await db.Database.BeginTransactionAsync(ct);

    // … read, validate, mutate …

    await db.SaveChangesAsync(ct);
    await tx.CommitAsync(ct);
});
```

This applies to every flow below that opens a transaction — leave submission, approval, cancellation, balance adjustment — and to the email and role changes in Phase 6.

**The body must be idempotent**, because it can run more than once. Anything non-transactional inside it — sending a notification, writing a file — must move outside, after the commit, or a retry duplicates it.

### 4.3 Leave Submission

```
SubmitLeaveAsync(command)
├─ Validate fields (FluentValidation)
├─ Open transaction (inside the execution strategy, §4.2)
├─ Load employee; assert Active
├─ Probation check (HireDate + 3 months > SctClock.Today)
├─ Reset-boundary check (range spans the hire anniversary?)
├─ Business day count (>= 1)
├─ Overlap check against Pending + Approved  ← inside the transaction
├─ Ensure current balance period exists (idempotent, ADR-0006)
├─ Balance check (Remaining >= businessDays; skipped for Unpaid)
├─ Create LeaveRequest (Pending)
├─ Notify every active Admin
└─ Commit
```

The overlap and balance checks sit **inside** the transaction. Checking before opening one reintroduces the race the transaction exists to prevent.

### 4.4 Leave Approval

```
ApproveAsync(requestId, note)
├─ Reviewer = ICurrentUser.EmployeeId
├─ Reject if reviewer == request.EmployeeId    ← separation of duties, spec §3.4.6
├─ Open transaction
├─ Load request; assert Pending
├─ Load balance for the request's period
├─ Decrement Used; increment Version
├─ Set request Approved, reviewer, timestamp
├─ Notify the employee
├─ Commit
└─ On DbUpdateConcurrencyException → Result.Fail(ConcurrencyConflict, "…please retry")
```

The `rowversion` token is what prevents two Admins approving simultaneously from both reading the same starting balance. SQL Server maintains it, so the application never increments anything.

Notification **rows** are written inside the transaction, since they are ordinary database writes. The in-process publisher signal that updates the bell badge is fired **after commit** — it is not transactional, and a retry would send it twice.

### 4.5 Clock In

```
ClockInAsync()
├─ employeeId = ICurrentUser.EmployeeId (never a parameter)
├─ today = SctClock.Today
├─ Resolve today's state; reject Weekend, Holiday, OnLeave
├─ Insert AttendanceRecord (ClockIn = UtcNow)
└─ On unique index violation (SQL Server error 2601 / 2627)
   → Result.Fail(Conflict, "Already clocked in today")
```

The unique index is the authoritative guard. A prior `SELECT` narrows the window but never closes it, so the constraint violation must be handled as an ordinary outcome, not surfaced as a database error.

### 4.6 Cancellation with Partial Restore

```
CancelAsync(requestId, note)
├─ Determine actor: employee (own, before start only) or admin (any time)
├─ If cancelled before StartDate      → restore all BusinessDays
├─ If cancelled on/after StartDate    → restore business days from today to EndDate only
├─ Record RestoredDays on the request
└─ Notify the counterparty
```

Recording `RestoredDays` is what lets the audit trail explain a balance that does not reconcile against `BusinessDays`.

### 4.7 Background Jobs

Both derive from a shared catch-up base:

```csharp
public abstract class CatchUpJob(IDbContextFactory<ApplicationDbContext> factory,
                                TimeProvider time) : BackgroundService
{
    protected abstract string JobName { get; }
    protected abstract Task ProcessDateAsync(DateOnly date, ApplicationDbContext db, CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24), time);
        do
        {
            // Read watermark; process every date from there to yesterday (SCT);
            // advance watermark only on success. Idempotent per date.
        }
        while (await timer.WaitForNextTickAsync(ct));
    }
}
```

- **`MissedClockOutJob`** — flags records with a clock-in and no clock-out for a fully elapsed SCT date, and notifies every active Admin.
- **`NotificationPurgeJob`** — `ExecuteDeleteAsync` for notifications older than the retention period. Date-based rather than watermark-based, but shares the loop.

The watermark is what makes these correct on an application that is not running continuously. A job processing only "yesterday" silently skips every day the container was stopped.

### 4.8 Reports

Report generation is split:

- **`ReportDataService`** (Application) assembles rows and applies the caller's scope.
- **`IReportRenderer`** (Infrastructure) renders those rows to PDF or CSV, writing to a supplied `Stream`.

```csharp
Task RenderAttendancePdfAsync(AttendanceReportModel model, Stream output, CancellationToken ct);
Task RenderAttendanceCsvAsync(AttendanceReportModel model, Stream output, CancellationToken ct);
```

Writing to a stream rather than returning `byte[]` is what allows the delivery path in Phase 5 to work at all.

**CSV injection defence is mandatory:**

```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    InjectionOptions = InjectionOptions.Escape
};
```

Without it, an employee whose leave reason begins with `=` produces a spreadsheet formula that executes when an Admin opens the export.

**QuestPDF licence** is set once at startup, before any document is created:

```csharp
QuestPDF.Settings.License = LicenseType.Community;
```

### Deliverable Checklist

- [ ] All Application services implemented; scope applied in every query
- [ ] Every explicit transaction wrapped in `CreateExecutionStrategy().ExecuteAsync(...)`
- [ ] Non-transactional side effects moved after commit
- [ ] Leave overlap and balance checks inside the committing transaction
- [ ] Concurrency conflicts surfaced as `ConcurrencyConflict`, not exceptions
- [ ] Clock-in handles the unique constraint violation as a normal outcome
- [ ] Self-approval refused
- [ ] Partial restore on mid-leave cancellation, with `RestoredDays` recorded
- [ ] Two catch-up jobs with `JobRun` watermarks, both idempotent
- [ ] Report renderers write to `Stream`
- [ ] `InjectionOptions.Escape` set on every CSV writer
- [ ] Notification fan-out to all active Admins

---

## Phase 5 — Web Layer

**Goal:** All pages, layout, navigation, charts, and report delivery.

### Before you start

Nothing beyond Phase 2. Seeded data makes the grids and dashboards far easier to build — enable seeding locally before starting.

### Skills

`create-blazor-project` · `author-component` · `collect-user-input` · `coordinate-components` · `use-js-interop` · `fetch-and-send-data` · `ui-craft`

### Context7 lookups

| Library | What to look up |
|---|---|
| MudBlazor | **v9** — `MudDataGrid` `ServerData` signature, `MudChart` series types, provider components required in the layout |
| `/dotnet/aspnetcore.docs` | Render modes and `AssignedRenderMode`; `DotNetStreamReference` file downloads; `MapStaticAssets` |

### 5.1 MudBlazor Setup

```csharp
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
    config.SnackbarConfiguration.PreventDuplicates = true;
});
```

All four providers must be present in `MainLayout.razor`, or dialogs and snackbars silently fail to render:

```razor
<MudThemeProvider />
<MudPopoverProvider />
<MudDialogProvider />
<MudSnackbarProvider />
```

**Fonts are self-hosted.** Reference local font files from `wwwroot`, not a CDN. An external font origin breaks offline use and cannot coexist with the Content Security Policy from Phase 6.

### 5.2 Page Order

**Round 1 — Layout** (account pages come from the template; customised in Phase 6)
1. `MainLayout.razor` — AppBar, drawer, providers
2. `NavMenu.razor` — role-conditional items
3. `AccessDenied.razor`
4. `Error.razor` — correlation identifier only

**Round 2 — Core CRUD**
5. `Departments/List.razor`
6. `Employees/List.razor`
7. `Employees/Create.razor`
8. `Employees/Edit.razor`
9. `Employees/View.razor`
10. `MyProfile.razor`

**Round 3 — Attendance**
11. `Dashboard/EmployeeDashboard.razor` — clock control
12. `Attendance/MyRecords.razor`
13. `Attendance/AllRecords.razor`
14. `Attendance/CorrectionDialog.razor`

**Round 4 — Leave**
15. `Leave/RequestForm.razor`
16. `Leave/MyLeave.razor`
17. `Leave/ManageLeave.razor`
18. `Leave/LeaveDetails.razor`
19. `Leave/BalanceAdjustmentDialog.razor` — new in v3.0; spec §3.4.7

**Round 5 — Dashboards & Reports**
20. `Dashboard/AdminDashboard.razor`
21. `Dashboard/ManagerDashboard.razor`
22. `Reports/ReportHub.razor`
23. `Reports/AttendanceReport.razor`
24. `Reports/LeaveReport.razor`
25. `Reports/DirectoryReport.razor`

**Round 6 — Supporting**
26. `Holidays/HolidayCalendar.razor`
27. `Audit/AuditLog.razor`
28. `NotificationBell.razor`
29. `Account/ManageAccounts.razor` — admin password reset and unlock; spec §3.1.7

### 5.3 Clock Control

```razor
<MudButton Variant="Variant.Filled"
           Color="@ButtonColour"
           Disabled="@(_isProcessing || !_canAct)"
           OnClick="HandleClockAction"
           StartIcon="@Icons.Material.Filled.AccessTime"
           Size="Size.Large">
    @ButtonText
</MudButton>

@code {
    private bool _isProcessing;
    private bool _canAct;   // false on Weekend, Holiday, OnLeave, or once completed

    private async Task HandleClockAction()
    {
        if (_isProcessing) return;
        _isProcessing = true;
        try
        {
            var result = _today?.ClockIn is null
                ? await Attendance.ClockInAsync(CancellationToken.None)
                : await Attendance.ClockOutAsync(CancellationToken.None);

            if (!result.IsSuccess)
                Snackbar.Add(result.Error!.Value.Message, Severity.Warning);

            _today = await Attendance.GetTodayAsync(CancellationToken.None);
        }
        finally { _isProcessing = false; }
    }
}
```

The client-side guard is usability. The server-side unique constraint from Phase 4 is the actual protection.

### 5.4 Report Download

The framework pattern for files under 250 MB:

```javascript
// wwwroot/js/download.js
window.downloadFileFromStream = async (fileName, contentType, streamReference) => {
    const arrayBuffer = await streamReference.arrayBuffer();
    const blob = new Blob([arrayBuffer], { type: contentType });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName ?? '';
    anchor.click();
    anchor.remove();
    URL.revokeObjectURL(url);
};
```

```csharp
await using var stream = new MemoryStream();
await _renderer.RenderAttendancePdfAsync(model, stream, ct);
stream.Position = 0;

using var streamRef = new DotNetStreamReference(stream);
await JS.InvokeVoidAsync("downloadFileFromStream",
    "attendance.pdf", "application/pdf", streamRef);
```

**Do not pass a `byte[]` to JavaScript.** Blazor Server marshals it over the SignalR connection, whose default maximum message size is 32 KB. v2.0's `download.js` accepted a byte array and would have failed on any real report.

### 5.5 Data Grids

```razor
<MudDataGrid T="EmployeeListDto" ServerData="LoadAsync" Filterable="true" SortMode="SortMode.Single" />
```

`LoadAsync` maps grid state onto the service filter, passing the sort column through the Phase 3 allow-list and clamping page size. A sort column arriving from the client is never interpolated into an expression.

### 5.6 Notification Bell

Subscribes to the in-process publisher on initialisation, unsubscribes on disposal, and calls `InvokeAsync(StateHasChanged)` when a notification arrives — the callback runs off the renderer's synchronisation context, so a direct `StateHasChanged` would throw.

### Deliverable Checklist

- [ ] MudBlazor configured; all four providers present; fonts self-hosted
- [ ] 29 pages/components
- [ ] Role-conditional navigation
- [ ] Clock control with client guard and server-authoritative result handling
- [ ] Dashboard charts rendering against derived attendance state
- [ ] Report download via `DotNetStreamReference`; no `byte[]` interop
- [ ] Grid sort/filter routed through the allow-list
- [ ] Notification badge updates without navigation
- [ ] No `MarkupString` applied to user-supplied values

---

## Phase 6 — Authentication & Authorisation Hardening

**Goal:** Close every authorisation gap. This phase is separate because v2.0 spread it across two phases and consequently under-specified it.

### Before you start

Nothing beyond Phase 2. HTTPS testing uses the local development certificate — run `dotnet dev-certs https --trust` once if the browser complains.

### Skills

`configure-auth` · `owasp-top-10` · `owasp-security-check` · `api-rate-limiting` · `secrets-management`

### Context7 lookups

| Library | What to look up |
|---|---|
| `/dotnet/aspnetcore.docs` | Blazor Web App Identity template structure; `IdentityRevalidatingAuthenticationStateProvider`; `AddCascadingAuthenticationState`; `AuthorizeRouteView`; endpoint `RequireAuthorization` |
| `/dotnet/aspnetcore.docs` | Resource-based authorisation; `AddRateLimiter` partitioned limiters; middleware order; antiforgery |
| `/dotnet/aspnetcore.docs` | Security stamp validation interval; `SignInManager.RefreshSignInAsync` |

### 6.1 Two Enforcement Layers

Both are required. Neither is sufficient alone.

```csharp
// Layer 1 — direct HTTP requests: first load, refresh, deep link, bookmark
app.MapRazorComponents<App>()
   .RequireAuthorization()
   .AddInteractiveServerRenderMode();
```

```razor
@* Layer 2 — navigation inside an established interactive session *@
<Router AppAssembly="typeof(Program).Assembly">
    <Found Context="routeData">
        <AuthorizeRouteView RouteData="routeData" DefaultLayout="typeof(MainLayout)">
            <NotAuthorized><AccessDenied /></NotAuthorized>
        </AuthorizeRouteView>
    </Found>
</Router>
```

```csharp
builder.Services.AddCascadingAuthenticationState();
```

Endpoint authorisation does not run on client-side navigation within an open circuit. Router authorisation does not run before the circuit exists. Both paths are covered by E2E tests in Phase 7.

### 6.2 Deny by Default

```csharp
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy("CanManageEmployees", p => p.RequireRole("Admin"));
    // … remaining policies per architecture.md §3.3
});
```

Anonymous access is granted explicitly — login, error page, `/health` — and nowhere else. A new page with no attribute is closed, not open.

### 6.3 Session Revalidation

The template's `IdentityRevalidatingAuthenticationStateProvider` re-checks the security stamp on an interval:

```csharp
protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(30);
```

This single mechanism resolves three v2.0 defects:

- A deactivated employee's open session terminates instead of surviving indefinitely on a sliding cookie.
- A role change takes effect without waiting for the user to sign out.
- Session expiry applies to a live connection, where cookie expiry alone never fires.

Every operation that must invalidate sessions — deactivation, role change, email change, password reset — calls `UserManager.UpdateSecurityStampAsync`.

### 6.4 Forced Password Reset

Add `MustChangePassword` as a claim during principal creation, then:

```csharp
options.AddPolicy("PasswordNotExpired", p =>
    p.RequireAssertion(ctx => !ctx.User.HasClaim("must_change_password", "true")));
```

Applied as part of the fallback policy, with the password-change page exempt. Once the password changes, `RefreshSignInAsync` re-issues the principal without the claim.

v2.0 used a redirect inside component initialisation, which is bypassable and must be remembered on every new page.

### 6.5 Login Rate Limiting

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1)
            }));
});
```

Applied to the login endpoint. Account lockout alone is insufficient and partly counterproductive: without a rate limit, an attacker who knows the email addresses can lock every account in the organisation.

Login failures return an identical response for unknown email, wrong password, and locked account.

### 6.6 Identity Synchronisation

Email and role changes go through Identity first:

```
ChangeEmailAsync
├─ UserManager.SetEmailAsync + SetUserNameAsync   (enforces uniqueness)
├─ UpdateSecurityStampAsync                        (terminates sessions)
├─ Update Employee.Email
└─ One transaction

ChangeRoleAsync
├─ RemoveFromRolesAsync + AddToRoleAsync
├─ UpdateSecurityStampAsync
├─ Update Employee.Role projection
├─ Last-admin guard: refuse if this empties the Admin role
└─ One transaction
```

Updating only `Employee.Email` would leave the login username unchanged, locking the employee out of their own account.

Both flows open an explicit transaction, so both use the execution-strategy wrapper from Phase 4 §4.2.

### 6.7 Transport & Headers

```csharp
app.UseHttpsRedirection();
if (!app.Environment.IsDevelopment()) app.UseHsts();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = true;
});
```

A CSP middleware sets a restrictive policy with `frame-ancestors 'none'` and no external origins — which is only achievable because Phase 5 self-hosted the fonts. Add `X-Content-Type-Options: nosniff` and `Referrer-Policy: no-referrer`.

### 6.8 Middleware Order

```csharp
app.UseExceptionHandler("/error");
app.UseHsts();                  // non-Development
app.UseHttpsRedirection();
app.UseSecurityHeaders();
app.MapStaticAssets();          // .NET 9+ replacement for UseStaticFiles
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();           // after routing and auth
app.MapRazorComponents<App>()
   .RequireAuthorization()
   .AddInteractiveServerRenderMode();
app.MapAdditionalIdentityEndpoints();
app.MapHealthChecks("/health").AllowAnonymous();
```

Order is load-bearing. `UseAntiforgery` before `UseRouting` throws at startup; `UseAuthorization` before `UseAuthentication` silently denies everything.

### Deliverable Checklist

- [ ] Endpoint and router authorisation both active, both E2E-tested
- [ ] Fallback policy requires an authenticated user; anonymous grants enumerated
- [ ] Revalidation interval configured; deactivation ends a live session
- [ ] Every session-invalidating operation refreshes the security stamp
- [ ] Forced password reset enforced by policy, not redirect
- [ ] Rate limiter on login; identical failure responses
- [ ] Email and role changes go through Identity in one transaction
- [ ] Last-admin guard enforced on deactivation and role change
- [ ] HTTPS, HSTS, cookie flags, CSP, and security headers set
- [ ] Middleware order verified

---

## Phase 7 — Testing

**Goal:** Unit, integration, and E2E coverage across the three test projects.

### Before you start

- **Docker Desktop running.** Integration tests use Testcontainers, which starts a real SQL Server container per test class. This is the first phase that needs Docker.
- PowerShell 7, for the Playwright browser installer.
- First run pulls `mcr.microsoft.com/mssql/server:2022-latest` (~1.6 GB) and Chromium. Allow time and disk.
- ~4 GB free RAM while integration tests run.

### Skills

`scaffold-dotnet-test-project` · `test-driven-development` · `code-testing-agent` · `run-tests` · `test-anti-patterns` · `assertion-quality` · `test-gap-analysis` · `coverage-analysis` · `find-untested-sources`

### Context7 lookups

| Library | What to look up |
|---|---|
| bUnit | **v2 migration guide.** `TestContext` is now `BunitContext`; `RenderComponent<T>()` is now `Render<T>()`. v1 examples do not compile |
| bUnit | Registering services and `JSInterop.SetupModule` for MudBlazor |
| Shouldly | Assertion syntax — this is a new library for this codebase |
| Testcontainers | `MsSqlBuilder`, container lifetime, obtaining the connection string |
| `/dotnet/aspnetcore.docs` | `WebApplicationFactory` with a real Kestrel server |
| Microsoft.Playwright | .NET API; browser installation; trace capture |

### 7.1 Unit Tests

| Category | Example |
|---|---|
| Domain | `IsInProbation` is true at 2 months 29 days, false at 3 months 1 day |
| Easter | Computus matches known Easter dates for 2020–2030 including leap years |
| Business days | Mon–Fri containing one holiday counts 4 |
| State resolution | Approved leave on a weekday resolves `OnLeave`, not `Absent` |
| Time | A clock-in at 21:00 UTC records the **next** SCT date |
| Validators | Backdated leave rejected; end before start rejected |
| Services | Overlapping leave blocked (substituted context) |
| Components | Clock button disabled while a request is in flight |

Time-dependent tests use `FakeTimeProvider`:

```csharp
var time = new FakeTimeProvider(new DateTimeOffset(2026, 8, 12, 4, 0, 0, TimeSpan.Zero));
var clock = new SctClock(time);
clock.Today.ShouldBe(new DateOnly(2026, 8, 12));   // 08:00 SCT
```

Probation and anniversary tests are otherwise flaky by construction, passing or failing according to the calendar.

**bUnit v2:**

```csharp
public class ClockButtonTests : BunitContext      // NOT TestContext — renamed in v2
{
    [Fact]
    public void Disables_while_processing()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;      // MudBlazor makes JS calls

        var cut = Render<ClockButton>();           // NOT RenderComponent<T>()
        // …
    }
}
```

### 7.2 Integration Tests

```csharp
public abstract class IntegrationTestBase : IAsyncLifetime
{
    private readonly MsSqlContainer _db = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    protected ServiceProvider Provider = null!;

    public async ValueTask InitializeAsync()
    {
        await _db.StartAsync();

        var services = new ServiceCollection();
        services.AddDbContextFactory<ApplicationDbContext>(o =>
            o.UseSqlServer(_db.GetConnectionString()));
        // register Application + Infrastructure services
        Provider = services.BuildServiceProvider();

        await using var context = await Provider
            .GetRequiredService<IDbContextFactory<ApplicationDbContext>>()
            .CreateDbContextAsync();

        await context.Database.MigrateAsync();     // NOT EnsureCreatedAsync
    }

    public async ValueTask DisposeAsync()
    {
        await Provider.DisposeAsync();
        await _db.DisposeAsync();
    }
}
```

Three deliberate choices:

- **`MigrateAsync`, not `EnsureCreatedAsync`.** `EnsureCreated` bypasses migrations entirely, so a broken migration passes every test and fails on first deployment.
- **A real SQL Server, not an in-memory substitute.** `rowversion`, unique index violation codes, `decimal` precision, and query translation all behave differently — or do not exist — outside the real engine. Testing against a substitute tests the substitute.
- **Testcontainers rather than a shared LocalDB.** The container is identical locally and in CI, starts clean, and disposes itself. A shared instance accumulates state between runs and diverges from what CI does.

**Cost:** roughly 15–20 seconds of container startup per test class. Group related tests into one class rather than spreading them thin, and keep the fast feedback loop in the unit test project.

| Category | Example |
|---|---|
| Constraints | Duplicate `(EmployeeId, Date)` rejected with SQL Server error 2601/2627 |
| Precision | A salary of `1234.56` round-trips exactly |
| Query filters | Inactive employees excluded by default, included with `IgnoreQueryFilters` |
| Concurrency | Two concurrent approvals — one succeeds, one returns `ConcurrencyConflict` |
| Audit | Salary change records before and after; password hash absent from the payload |
| Balances | Lazy period materialisation is idempotent under concurrent access |
| Jobs | Catch-up processes three missed days after simulated downtime |
| Seeding | Runs clean, and a second run changes nothing |

### 7.3 E2E Tests

**v2.0's approach could not have worked.** `WebApplicationFactory` hosts an in-memory `TestServer` with no TCP listener, so Playwright has nothing to connect to, and its fixture referenced a `_baseUrl` that was never assigned.

The fixture must start a real Kestrel server:

```csharp
public sealed class AppFixture : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;
    public string BaseUrl { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b =>
            {
                b.UseEnvironment("Testing");
                b.UseKestrel(o => o.ListenLocalhost(0));   // real listener, OS-assigned port
            });

        _factory.StartServer();
        BaseUrl = _factory.ClientOptions.BaseAddress.ToString();

        var playwright = await Playwright.CreateAsync();
        Browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
    }
}
```

**Isolation.** Each test class gets a fresh database seeded from a known snapshot, and the E2E project runs in a single xUnit collection so tests do not race on shared state. Clock-in and leave-approval scenarios mutate state that later tests read; running them in parallel against one database is flaky by construction.

Capture Playwright traces on failure and upload them as CI artifacts.

| Scenario | Covers |
|---|---|
| First login → forced reset → dashboard | Spec §3.1.3 |
| Clock in → clock out → completed | Spec §3.3.4 |
| Weekend → clock control unavailable | Spec §3.3.7 |
| Overlapping leave rejected | Spec §3.4.3 |
| Admin cannot approve own leave | Spec §3.4.6 |
| Employee cannot see "Manage Leave" | Spec §2.5 rule 5 |
| **Employee navigates directly to `/employees` by URL → denied** | Endpoint authorisation |
| **Employee clicks through to a restricted route in-session → denied** | Router authorisation |
| **Manager requests an out-of-department employee by id → not found** | Spec §2.5 rule 4 |
| **Deactivated employee's open session ends** | Spec §3.1.5 |
| Report export downloads a non-empty PDF and CSV | Phase 5 delivery path |
| Account lockout after 5 failures | Spec §3.1.5 |

The four highlighted scenarios are the regression tests for v2.0's authorisation defects. They are not optional.

### 7.4 Running Tests

```bash
dotnet test EMS.sln

dotnet test tests/EMS.UnitTests
dotnet test tests/EMS.IntegrationTests
dotnet test tests/EMS.E2E.Tests

dotnet test tests/EMS.UnitTests --collect:"XPlat Code Coverage"

# Playwright browsers, first time only
pwsh tests/EMS.E2E.Tests/bin/Debug/net10.0/playwright.ps1 install chromium
```

### Deliverable Checklist

- [ ] Unit tests for domain rules, computus, business days, state resolution, validators
- [ ] All time-dependent tests use `FakeTimeProvider`
- [ ] bUnit tests use `BunitContext` and `Render<T>()`
- [ ] Integration tests run `MigrateAsync` against a Testcontainers SQL Server instance
- [ ] Concurrency, audit redaction, query filter, and catch-up tests present
- [ ] E2E fixture starts a real Kestrel listener
- [ ] Both authorisation layers covered by E2E tests
- [ ] E2E tests serialised with per-class database reset
- [ ] Traces captured on failure
- [ ] `dotnet test EMS.sln` green

---

## Phase 8 — Docker

**Goal:** A minimal, non-root application image and one-command local startup, with the database in its own container.

### Before you start

- Docker Desktop running, ~4 GB free RAM for the SQL Server container
- An `.env` file with `EMS_SA_PASSWORD` and `EMS_ADMIN_PASSWORD` — git-ignored, never committed
- The SA password must satisfy SQL Server's policy: 8+ characters from three of uppercase, lowercase, digits, symbols. A weak value makes the container exit during startup with a message that is easy to miss

### Skills

`docker-containerization` · `secrets-management`

### Context7 lookups

| Library | What to look up |
|---|---|
| `/dotnet/aspnetcore.docs` | .NET container images: chiselled variants, `APP_UID`, published output layout |
| Docker | `mssql/server` image environment variables; Compose healthcheck and `depends_on: condition: service_healthy` |

### 8.1 Dockerfile

```dockerfile
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props nuget.config global.json EMS.sln ./
COPY src/EMS.Domain/EMS.Domain.csproj                 src/EMS.Domain/
COPY src/EMS.Application/EMS.Application.csproj       src/EMS.Application/
COPY src/EMS.Infrastructure/EMS.Infrastructure.csproj src/EMS.Infrastructure/
COPY src/EMS.Web/EMS.Web.csproj                       src/EMS.Web/

# Restore only the web project's graph. The solution also references the test
# projects, whose .csproj files are deliberately not copied into the image —
# restoring the solution here is what broke the v2.0 Dockerfile.
RUN dotnet restore src/EMS.Web/EMS.Web.csproj --locked-mode

COPY src/ src/

RUN dotnet publish src/EMS.Web/EMS.Web.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# The chiselled image predefines a non-root user. Creating one by hand
# requires a shell and a package manager that this image deliberately lacks.
USER $APP_UID

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production

# The connection string carries a credential and is supplied at runtime, never baked in.

ENTRYPOINT ["dotnet", "EMS.Web.dll"]
```

Changes from v2.0 and why:

| v2.0 | v3.0 | Reason |
|---|---|---|
| `dotnet restore EMS.sln` | Restore the web project only | The solution references test projects never copied into the image — restore failed |
| `apt-get install libsqlite3-0` | Removed | `Microsoft.Data.SqlClient` is fully managed; there is no native client to install |
| `groupadd`/`useradd` | `USER $APP_UID` | The image predefines a non-root user; the chiselled image has no shell to create one |
| `curl` + in-image `HEALTHCHECK` | Healthcheck in Compose | Chiselled images carry no shell or curl |
| Full `aspnet:10.0` | `aspnet:10.0-noble-chiseled` | Substantially smaller attack surface |
| Database inside the app container | Separate `db` service | Removes the volume-permission problem entirely |

### 8.2 docker-compose.yml

```yaml
services:
  db:
    image: mcr.microsoft.com/mssql/server:2022-latest
    container_name: ems-db
    environment:
      - ACCEPT_EULA=Y
      - MSSQL_SA_PASSWORD=${EMS_SA_PASSWORD:?set EMS_SA_PASSWORD in .env}
      - MSSQL_PID=Developer
    ports: ["1433:1433"]          # development convenience only — see below
    volumes: ["ems-data:/var/opt/mssql"]
    healthcheck:
      test: ["CMD-SHELL", "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P \"$$MSSQL_SA_PASSWORD\" -C -Q 'SELECT 1' || exit 1"]
      interval: 10s
      timeout: 5s
      start_period: 30s
      retries: 10
    restart: unless-stopped

  ems:
    build: { context: ., dockerfile: Dockerfile }
    container_name: ems-app
    depends_on:
      db:
        condition: service_healthy
    ports: ["5000:8080"]
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - AppSettings__SeedData__Enabled=true
      - ConnectionStrings__DefaultConnection=Server=db,1433;Database=EMS;User Id=sa;Password=${EMS_SA_PASSWORD};TrustServerCertificate=True
      - Seed__AdminPassword=${EMS_ADMIN_PASSWORD:?set EMS_ADMIN_PASSWORD in .env}
    restart: unless-stopped

volumes:
  ems-data: { driver: local }
```

Four things worth understanding rather than copying:

- **`condition: service_healthy`.** SQL Server needs 15–25 seconds before it accepts connections. Without this the application starts first, fails to migrate, and exits. The retrying execution strategy from Phase 2 covers the remaining margin; this covers the bulk of it.
- **`${VAR:?message}`** makes Compose refuse to start when the variable is absent, rather than silently falling back to a default credential. `.env` is git-ignored.
- **Publishing 1433 is a development affordance**, so Visual Studio's SQL Server Object Explorer can attach to `localhost,1433`. It has no place in a deployed configuration, and it is the line to delete first if this file is ever used as a starting point for one.
- **The image ships `Production`**; this development file overrides it and enables seeding. Stated explicitly, because v2.0 did the same thing silently.

`MSSQL_PID=Developer` selects the free Developer edition, which is licensed for development and testing only.

### 8.3 .dockerignore

```
**/.git
**/.vs
**/.vscode
**/bin
**/obj
**/test-results
**/coverage-report
**/.env
**/*.mdf
**/*.ldf
Dockerfile
docker-compose*.yml
.dockerignore
tests/
*.md
!README.md
```

### Deliverable Checklist

- [ ] Image builds from a clean clone
- [ ] `docker compose up --build` reaches a healthy state on a machine with no prior volume
- [ ] The application waits for the database rather than crash-looping
- [ ] `/health` returns 200
- [ ] Data survives `docker compose down` and `up`
- [ ] Application container runs as non-root
- [ ] Compose refuses to start when either password variable is absent
- [ ] SQL Server Object Explorer connects to `localhost,1433`

---

## Phase 9 — CI/CD & Supply Chain

**Goal:** Fast, least-privilege pipelines with dependency and image scanning.

### Before you start

- A GitHub repository with Actions enabled
- Package write permission for ghcr.io (`packages: write`, granted per job)
- No database service block is needed in the workflow — Testcontainers starts its own, and the GitHub-hosted `ubuntu-latest` runner has a working Docker daemon

### Skills

`dependency-supply-chain-security` · `git-hooks-setup` · `semantic-versioning` · `verification-before-completion`

### Context7 lookups

| Library | What to look up |
|---|---|
| GitHub Actions | `setup-dotnet` caching; `concurrency`; job-level `permissions` |
| CodeQL | C# `build-mode: none` support |
| Docker | `build-push-action` with GitHub Actions cache; image scanning actions |

### 9.1 CI Workflow

```yaml
name: CI
on:
  push: { branches: [main] }
  pull_request: { branches: [main] }

concurrency:
  group: ci-${{ github.ref }}
  cancel-in-progress: true

permissions:
  contents: read

env:
  DOTNET_VERSION: '10.0.x'
  DOTNET_NOLOGO: true
  DOTNET_CLI_TELEMETRY_OPTOUT: true

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    timeout-minutes: 30
    steps:
      - uses: actions/checkout@<pinned-sha>
      - uses: actions/setup-dotnet@<pinned-sha>
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}
          cache: true
          cache-dependency-path: '**/packages.lock.json'

      - run: dotnet restore EMS.sln --locked-mode
      - run: dotnet build EMS.sln --no-restore --configuration Release
      - run: dotnet format EMS.sln --verify-no-changes --verbosity diagnostic

      - name: Vulnerable packages
        run: |
          dotnet list EMS.sln package --vulnerable --include-transitive 2>&1 | tee vuln.txt
          ! grep -q "has the following vulnerable packages" vuln.txt

      - name: Unit tests
        run: dotnet test tests/EMS.UnitTests --no-build --configuration Release
             --collect:"XPlat Code Coverage" --results-directory ./test-results/unit

      # Testcontainers starts its own SQL Server; no `services:` block required.
      # First run pulls the mssql image, so allow for it in the job timeout.
      - name: Integration tests
        run: dotnet test tests/EMS.IntegrationTests --no-build --configuration Release
             --collect:"XPlat Code Coverage" --results-directory ./test-results/integration

      - name: Install Playwright
        run: pwsh tests/EMS.E2E.Tests/bin/Release/net10.0/playwright.ps1 install chromium --with-deps

      - name: E2E tests
        run: dotnet test tests/EMS.E2E.Tests --no-build --configuration Release
             --results-directory ./test-results/e2e

      - uses: danielpalme/ReportGenerator-GitHub-Action@<pinned-sha>
        if: always()
        with:
          reports: 'test-results/**/coverage.cobertura.xml'
          targetdir: 'coverage-report'
          reporttypes: 'HtmlInline;Cobertura;Badges'

      - uses: actions/upload-artifact@<pinned-sha>
        if: always()
        with:
          name: test-results
          path: |
            test-results/
            coverage-report/
            tests/EMS.E2E.Tests/bin/Release/net10.0/playwright-traces/
          retention-days: 30

  codeql:
    runs-on: ubuntu-latest
    timeout-minutes: 30
    permissions:
      contents: read
      security-events: write
    steps:
      - uses: actions/checkout@<pinned-sha>
      - uses: github/codeql-action/init@<pinned-sha>
        with: { languages: csharp }
      - uses: actions/setup-dotnet@<pinned-sha>
        with: { dotnet-version: '10.0.x' }
      - run: dotnet build EMS.sln --configuration Release
      - uses: github/codeql-action/analyze@<pinned-sha>
        with: { category: "/language:csharp" }
```

Every third-party action is pinned to a commit SHA. Tags are mutable; a compromised tag reassignment silently changes what runs against the repository.

`if: always()` is on both the coverage and upload steps, so a failing E2E run still produces the artifacts needed to diagnose it.

### 9.2 Docker Workflow

```yaml
name: Docker
on:
  push: { tags: ['v*.*.*'] }

permissions:
  contents: read
  packages: write

jobs:
  verify:
    uses: ./.github/workflows/ci.yml

  docker:
    needs: verify          # never publish an untested image
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@<pinned-sha>
      - uses: docker/login-action@<pinned-sha>
        with:
          registry: ghcr.io
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}
      - uses: docker/metadata-action@<pinned-sha>
        id: meta
        with:
          images: ghcr.io/${{ github.repository }}
          tags: |
            type=semver,pattern={{version}}
            type=semver,pattern={{major}}.{{minor}}
            type=raw,value=latest
      - uses: docker/setup-buildx-action@<pinned-sha>
      - uses: docker/build-push-action@<pinned-sha>
        with:
          context: .
          push: true
          tags: ${{ steps.meta.outputs.tags }}
          labels: ${{ steps.meta.outputs.labels }}
          cache-from: type=gha
          cache-to: type=gha,mode=max
          provenance: true
      - name: Scan image
        uses: aquasecurity/trivy-action@<pinned-sha>
        with:
          image-ref: ghcr.io/${{ github.repository }}:latest
          severity: 'CRITICAL,HIGH'
          exit-code: '1'
```

`needs: verify` is the substantive change from v2.0, whose tag workflow built and pushed without running a single test.

### 9.3 Dependabot

```yaml
version: 2
updates:
  - package-ecosystem: nuget
    directory: "/"
    schedule: { interval: weekly }
    groups:
      microsoft: { patterns: ["Microsoft.*", "System.*"] }
  - package-ecosystem: github-actions
    directory: "/"
    schedule: { interval: weekly }
```

### 9.4 Branch Protection

`main` requires a passing `ci.yml` and one approving review. Without this, "CI on push to main" means failures land on main and are discovered afterwards.

### Deliverable Checklist

- [ ] `ci.yml` runs on push and pull request, green
- [ ] All actions pinned to commit SHAs
- [ ] Least-privilege permissions per job; concurrency group; timeouts
- [ ] `dotnet format --verify-no-changes` genuinely enforcing (severities from Phase 0)
- [ ] Vulnerable package scan fails the build on an advisory
- [ ] `docker.yml` gated on CI; image scanned before publish
- [ ] Dependabot configured
- [ ] Branch protection enabled on `main`
- [ ] README badges display

---

## Phase 10 — Observability & Accessibility

**Goal:** Diagnosable in production, usable by keyboard.

### Before you start

Nothing beyond Phase 0. The accessibility audit runs against the completed UI, so Phase 5 must be finished.

### Skills

`configuring-opentelemetry-dotnet` · `a11y-audit` · `ui-craft`

### Context7 lookups

| Library | What to look up |
|---|---|
| `/dotnet/aspnetcore.docs` | Logging configuration, log scopes, `ILogger` structured message templates |
| OpenTelemetry .NET | ASP.NET Core instrumentation, if adopted |

### 10.1 Logging

- Structured `ILogger` throughout. Message templates with named placeholders, never interpolated strings — interpolation destroys the structure that makes logs queryable.
- A log scope carries the acting employee identifier and a correlation identifier on every request.
- Warning and above in Production.
- Passwords, tokens, and salary values are never logged. A logging filter enforces this rather than relying on discipline.

### 10.2 Error Presentation

Unhandled exceptions produce a correlation identifier shown to the user and logged in full server-side. The browser never receives exception detail outside Development, and `DetailedErrors` is false for circuits.

### 10.3 Health

```csharp
builder.Services.AddHealthChecks().AddDbContextCheck<ApplicationDbContext>("database");
app.MapHealthChecks("/health").AllowAnonymous();
```

Anonymous but minimal — healthy or unhealthy, with no detail that describes the internals.

### 10.4 Accessibility Baseline

Against spec §4.8:

- Every input has an associated label. MudBlazor's `Label` parameter produces one; a placeholder does not.
- Dialogs trap focus, receive focus on open, and restore it on close.
- The data grid is keyboard-navigable, including sort and pagination controls.
- Attendance and leave states carry an icon or text alongside colour.
- Text contrast is at least 4.5:1 — verify the MudBlazor palette rather than assuming it.

Run `a11y-audit` over the completed UI and fix Level A findings before sign-off.

### Deliverable Checklist

- [ ] Structured logging with correlation and acting user
- [ ] Sensitive value filter active
- [ ] Error page shows a correlation identifier and nothing else
- [ ] `/health` responds anonymously with minimal detail
- [ ] Accessibility baseline met; Level A findings resolved

---

## 11. Program.cs Composition

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddInfrastructure(builder.Configuration);   // DbContextFactory, Identity, jobs
builder.Services.AddApplication();                           // services, validators, SctClock
builder.Services.AddWebLayer();                              // MudBlazor, auth policies, rate limiter

builder.Services.AddRazorComponents().AddInteractiveServerComponents(options =>
{
    options.DetailedErrors = builder.Environment.IsDevelopment();
    options.DisconnectedCircuitMaxRetained = 50;
    options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(3);
    options.JSInteropDefaultCallTimeout = TimeSpan.FromMinutes(1);
});
builder.Services.Configure<HubOptions>(o => o.MaximumReceiveMessageSize = 64 * 1024);

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHostedService<MissedClockOutJob>();
builder.Services.AddHostedService<NotificationPurgeJob>();
builder.Services.AddHealthChecks().AddDbContextCheck<ApplicationDbContext>("database");

var app = builder.Build();

QuestPDF.Settings.License = LicenseType.Community;

// Middleware order is load-bearing — see Phase 6.8
app.UseExceptionHandler("/error");
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseHttpsRedirection();
app.UseSecurityHeaders();
app.MapStaticAssets();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorComponents<App>()
   .RequireAuthorization()
   .AddInteractiveServerRenderMode();
app.MapAdditionalIdentityEndpoints();
app.MapReportDownloadEndpoints();
app.MapHealthChecks("/health").AllowAnonymous();

// Database initialisation
await using (var scope = app.Services.CreateAsyncScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
    await using var db = await factory.CreateDbContextAsync();

    await db.Database.MigrateAsync();     // absent in v2.0 — this is what creates the database
    await HolidayService.EnsureGeneratedAsync(scope.ServiceProvider, DateTime.UtcNow.Year);
    await DataSeeder.SeedAsync(scope.ServiceProvider, CancellationToken.None);
}

app.Run();

public partial class Program;   // required by WebApplicationFactory in E2E tests
```

`MigrateAsync` at startup is correct for a single instance and is a known hazard for several — concurrent migrations on the same database race. EMS is single-instance by design (ADR-0009). If that ever changes, migration moves to a deployment step and comes out of `Program.cs`.

---

## 12. End-to-End Scenarios

| # | Scenario | Expected |
|---|---|---|
| 1 | First login | Forced password reset, then dashboard |
| 2 | Create employee | Temp password issued; new employee forced to reset |
| 3 | Clock in late | 08:05 SCT resolves `Late`; clock out computes minutes |
| 4 | Missed clock-out | Nightly job flags; Admin corrects with a mandatory note |
| 5 | Leave happy path | Mon–Fri Annual = 5 days; approved; balance decremented |
| 6 | Leave overlap | Approved Mon–Wed blocks a Tue–Thu request |
| 7 | Leave over-balance | 2 remaining, 5 requested, blocked |
| 8 | Leave in probation | Hired under 3 months, blocked |
| 9 | Leave reset boundary | Range spanning the hire anniversary, blocked |
| 10 | Cancel before start | Full balance restored |
| 11 | **Admin cancels mid-leave** | **Only remaining days restored; `RestoredDays` recorded** |
| 12 | **Admin approves own leave** | **Refused** |
| 13 | Deactivation | Cannot log in; **open session ends**; records preserved |
| 14 | Department delete block | Department with employees cannot be deleted |
| 15 | Report export | PDF and CSV download, non-empty |
| 16 | **CSV injection** | **A reason beginning `=` exports escaped** |
| 17 | Notification flow | Submit → all Admins badged **without navigation** → click navigates |
| 18 | Audit trail | Salary edit records before and after; **no password hash present** |
| 19 | Account lockout | 5 failures locks for 15 minutes |
| 20 | **Admin unlock** | **Locked account released before expiry** |
| 21 | **Login rate limit** | **Rapid attempts throttled independently of lockout** |
| 22 | Session timeout | Idle session rejected **on an open connection**, not only on refresh |
| 23 | Weekend | Clock control unavailable |
| 24 | **Direct URL to a restricted page** | **Denied by endpoint authorisation** |
| 25 | **In-session navigation to a restricted page** | **Denied by router authorisation** |
| 26 | **Manager requests out-of-scope employee** | **Not found** |
| 27 | **Employee on approved leave** | **Attendance shows `OnLeave`, not `Absent`** |
| 28 | **Holiday generation** | **Good Friday correct for three different years** |
| 29 | **Catch-up job** | **Three days of downtime processed on next start** |
| 30 | Docker startup | `docker compose up` reaches healthy; app at localhost:5000 |
| 31 | Health check | `GET /health` returns 200 |

Highlighted scenarios are new in v3.0 and exist because the corresponding behaviour was absent, wrong, or unenforceable in v2.0.

---

## 13. Pitfalls

| Pitfall | Resolution |
|---|---|
| "The configured execution strategy does not support user-initiated transactions" | Wrap the transaction in `CreateExecutionStrategy().ExecuteAsync(...)` — Phase 4 §4.2 |
| App crash-loops under Compose | `depends_on: condition: service_healthy`; SQL Server needs 15–25 s |
| SQL Server container exits immediately | SA password fails the complexity policy, or `ACCEPT_EULA` is unset |
| `sqllocaldb info` lists nothing | Visual Studio component "Data storage and processing" not installed |
| LocalDB connection times out | `sqllocaldb start MSSQLLocalDB` — the instance starts on demand |
| String columns become `nvarchar(max)` | Explicit `HasMaxLength` on every string property; `max` columns cannot be indexed |
| "A second operation was started on this context" | `AddDbContextFactory`, one context per operation |
| Money loses precision | `decimal` with `HasPrecision(18, 2)`; never `double` or `float` |
| Login page does nothing | `SignInManager` needs static SSR; it cannot set a cookie from an interactive circuit |
| Session survives deactivation | Security stamp revalidation |
| Restricted page reachable by URL | Endpoint authorisation as well as router authorisation |
| Report download fails silently | `DotNetStreamReference`, not `byte[]` over the circuit |
| Attendance shows leave as absent | Derive state; never store it |
| Balance reset skipped | Lazy materialisation, not a timer |
| Background job skips days | `JobRun` watermark with catch-up |
| Two admins overdraw a balance | `rowversion` concurrency token on `LeaveBalance` |
| A retried transaction sends a notification twice | Non-transactional side effects go after the commit |
| Audit write fails in a background job | Nullable audit actor |
| CSV opens as a formula in Excel | `InjectionOptions.Escape` |
| Inactive employees appear in lists | Global query filter, explicit `IgnoreQueryFilters` |
| Time-dependent tests flaky | `FakeTimeProvider`; ban `DateTime.Now` |
| bUnit examples do not compile | v2 renamed `TestContext` to `BunitContext` and `RenderComponent<T>()` to `Render<T>()` |
| Playwright cannot connect | `WebApplicationFactory` needs a real Kestrel listener |
| Migrations untested until deployment | `MigrateAsync` in integration tests, not `EnsureCreated` |
| Docker restore fails | Restore the web project, not the solution |
| Integration tests hang or fail to start | Docker Desktop must be running — Testcontainers needs it |
| Format check passes but changes nothing | `.editorconfig` rules need explicit severities |
| MudBlazor renders nothing | All four providers in `MainLayout` |
| QuestPDF throws on first document | Set the licence before any document is created |

---

## 14. Running the Application

```bash
# Local — LocalDB, no containers
cd src/EMS.Web
dotnet user-secrets set "Seed:AdminPassword" "<strong local password>"
dotnet run                        # creates the EMS database on first run

# Hot reload
dotnet watch run

# Docker — application plus SQL Server
cat > .env <<'EOF'
EMS_SA_PASSWORD=<strong password, 8+ chars, mixed case, digit, symbol>
EMS_ADMIN_PASSWORD=<strong local password>
EOF
docker compose up --build -d      # http://localhost:5000
docker compose logs -f ems
docker compose down               # add -v to discard the database volume
```

Default admin: `admin@ems.local`, password from configuration, forced change on first login.

The two environments use separate databases. Data does not move between `dotnet run` and `docker compose up`, and neither is a backup of the other.

**Resetting local data:** drop the `EMS` database from SQL Server Object Explorer and run again. `dotnet ef database drop` does the same from the CLI.

---

## 15. Post-v1.0

- Email notifications
- Two-factor authentication and passkeys — supported by the Identity stack, deliberately deferred
- Document uploads
- Mobile-responsive layout and dark mode
- Payroll with salary slips
- REST API for external integration
- Half-day leave, overtime tracking, leave carry-over
- Multi-timezone support
- Multi-instance deployment — requires moving migration out of startup and replacing the in-process notification publisher (ADR-0009)
- Full WCAG 2.2 AA conformance
