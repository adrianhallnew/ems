# EMS — Employee Management System: Product Specification

> **Version:** 3.0
> **Date:** August 12, 2026
> **Status:** Approved for implementation
> **Supersedes:** v2.0 (August 12, 2026)

---

## 0. Changes from v2.0

This revision closes correctness, security, and completeness gaps found in a full audit of v2.0. Material changes:

| Area | Change |
|---|---|
| Authorisation | Role checks alone were insufficient. Added mandatory data scoping, deny-by-default, and a rule that no operation accepts a caller-supplied identity for "own" actions (§2.5). |
| Session lifecycle | Deactivation and session expiry now revoke live sessions via security stamp revalidation (§3.1.5). |
| Identity of record | `Employee.Role` and `Employee.Email` were duplicated against ASP.NET Identity. Identity is now the single source of truth (§3.1.6). |
| Attendance | `Absent` / `Weekend` / `Holiday` / `OnLeave` are now **derived at read time**, not stored. Removes an entire class of sync bugs (§3.3.7). |
| Leave balances | Reset is now **lazy-on-read**, not a scheduled job. A container that is off on an employee's hire anniversary can no longer skip their reset (§3.4.2). |
| Leave | Added admin balance adjustment (§3.4.7), partial restore on mid-leave cancellation (§3.4.5), and a self-approval prohibition (§3.4.6). |
| Holidays | Added Easter computus for the four variable Seychelles holidays (§3.7.4). |
| Account recovery | Added admin-initiated password reset and account unlock. v2.0 had no recovery path at all (§3.1.7). |
| Datastore | SQL Server replaces SQLite. LocalDB for local development, a SQL Server container for Docker — one EF Core provider, one migration set (§4.1, ADR-0009). |
| Money and duration | Salary is `decimal(18,2)`; worked time is `int` minutes (§4.2, ADR-0010). |
| Security NFRs | New §4.4 covering rate limiting, transport, headers, CSV injection, and secret handling. |
| Versions | All package versions verified against NuGet on 2026-08-12 and corrected (§4.1). |
| Observability | New §4.7. v2.0 specified audit but no application logging. |

Rationale for the non-obvious decisions lives in `Docs/decisions/`.

---

## 1. Overview

The Employee Management System (EMS) is an internal web application for managing employees, departments, attendance, and leave within a single-location organisation. It provides role-based access for Admins, Managers, and Employees, with a dashboard for real-time insights and exportable reports.

**Target scale:** Small-to-medium organisation (10–50 employees, 3–5 departments).
**Deployment:** Local development only (containerised via Docker). See ADR-0001 for the constraints this choice imposes and the conditions under which it must be revisited.

---

## 2. User Roles & Permissions

### 2.1 Admin

Full system access. The Admin is the central authority for all operational and configuration tasks.

| Capability | Details |
|---|---|
| Employee management | Create, read, update, deactivate (soft delete) any employee |
| Department management | Full CRUD on departments, assign/change department managers |
| Leave management | View all leave requests, approve or reject, cancel any leave, adjust balances |
| Attendance management | View all attendance records, correct entries (with mandatory reason/note), resolve missed clock-out flags |
| Reports | Generate and export all report types (PDF/CSV) with date range filtering |
| Public holidays | Add, edit, delete public holidays in the calendar |
| Dashboard | Full dashboard with all stats, charts, and pending items |
| User provisioning | Create employee accounts with temporary passwords |
| Account recovery | Reset any employee's password; unlock a locked-out account |
| Salary visibility | View and edit salary for all employees |
| Notifications | Receives: new leave requests, leave cancellations, missed clock-out flags, manager-vacated alerts |
| Audit log | View all audit trail entries |

### 2.2 Manager

Read-only access scoped to their assigned department. Managers cannot approve leave or edit employee profiles.

| Capability | Details |
|---|---|
| Employee viewing | View profiles of employees in their department (excluding salary) |
| Attendance viewing | View attendance records for their department |
| Leave viewing | View leave requests/balances for their department |
| Reports | Generate and export reports scoped to their department |
| Dashboard | Department-scoped stats and charts |
| Own profile | Self-service editing of own contact info, address, emergency contact |
| Own attendance | Clock In/Out for themselves |
| Own leave | Request, view, and cancel own leave (before start date) |

### 2.3 Employee

Self-service access to own data only.

| Capability | Details |
|---|---|
| Own profile | View full profile; edit own contact info (phone, email), address, and emergency contact |
| Own attendance | Clock In/Out; view own attendance history |
| Own leave | Submit leave requests, view balances and history, cancel approved leave (before start date) |
| Notifications | Receives: leave approved/rejected/cancelled-by-admin |
| Dashboard | Personal stats (attendance this month, leave balance summary) |

### 2.4 Manager Scope Definition

A Manager's scope is **the set of employees whose `DepartmentId` matches a department where that Manager is the assigned `ManagerId`**. A Manager with no assigned department has an empty scope and sees only their own self-service data.

A Manager is in scope of their own department for read purposes, including their own record.

### 2.5 Authorisation Enforcement Rules

These are requirements, not implementation notes. Every one of them is independently testable.

1. **Deny by default.** Any page or endpoint without an explicit authorisation decision is inaccessible to unauthenticated users. Anonymous access is an opt-in, granted only to the login page, the error page, and `/health`.
2. **Two enforcement layers are mandatory.** Route-level authorisation must be applied both at the HTTP endpoint (covering direct navigation and page refresh) and at the interactive router (covering navigation within an established session). Neither alone is sufficient.
3. **No caller-supplied identity for own-data operations.** Operations that act on "the current user's" data must derive the acting employee from the authenticated principal on the server. A client-supplied employee identifier must never be trusted for this purpose.
4. **Scope is applied in the data layer.** Department scoping for Managers is applied as a query predicate in the service, not by filtering in the UI. A Manager requesting an out-of-scope record by identifier receives "not found", not the record.
5. **UI hiding is not authorisation.** Hiding a menu item or a button is a usability measure. Every hidden action must also be refused by the server.
6. **Salary is projected, not hidden.** Employee data returned to a non-Admin must not contain the salary value at all. It is excluded from the projection, not blanked in the view.

---

## 3. Module Specifications

### 3.1 Employee Management

#### 3.1.1 Data Fields

| Field | Type | Editable By | Notes |
|---|---|---|---|
| First Name | String (required) | Admin | Max 100 |
| Last Name | String (required) | Admin | Max 100 |
| Email | String (required, unique) | Employee (self), Admin | Login username. See §3.1.6 |
| Phone | String (required) | Employee (self), Admin | Max 30 |
| Date of Birth | Date (required) | Admin | Must be a past date; employee at least 16 |
| Address | Text (required) | Employee (self), Admin | Full address (street, city, district) |
| Emergency Contact Name | String (required) | Employee (self), Admin | Max 100 |
| Emergency Contact Phone | String (required) | Employee (self), Admin | Max 30 |
| Salary | Decimal (required) | Admin | `decimal(18,2)` SCR. Visible to Admin only. See ADR-0010 |
| Job Title | String (required) | Admin | Free text (e.g., "Senior Accountant") |
| Contract Type | Enum (required) | Admin | Full-time, Part-time, Contract, Intern |
| Department | FK to Department (required) | Admin | One department per employee |
| Role | Enum (required) | Admin | Admin, Manager, Employee. Projected from Identity — see §3.1.6 |
| Hire Date | Date (required) | Admin | Used for probation calculation and leave balance reset |
| Status | Enum | System/Admin | Active, Inactive (soft delete) |
| Created At | DateTime | System | Auto-set on creation (UTC) |
| Updated At | DateTime | System | Auto-set on update (UTC) |

#### 3.1.2 Business Rules

- **One department per employee.** No multi-department membership.
- **Soft delete only.** Deactivating an employee sets Status to Inactive. All historical data (attendance, leave, audit) is preserved. Inactive employees cannot log in, and their live session is terminated (§3.1.5).
- **Unique email constraint.** No two employees (active or inactive) may share an email.
- **Self-service scope.** Employees may only edit: Email, Phone, Address, Emergency Contact Name, Emergency Contact Phone.
- **Probation period.** First 3 months from Hire Date. Probationary employees cannot submit leave requests. The system calculates probation status dynamically from Hire Date.
- **Last-admin guard.** The system must refuse any operation that would leave zero active employees in the Admin role. This covers deactivation, role change, and department reassignment. The refusal is a validation error with an explicit message, not an unhandled exception.

#### 3.1.3 Account Provisioning

1. Admin creates the employee record with all required fields.
2. Admin sets a temporary password during creation, or requests a system-generated one.
3. On first login, the employee is forced to change their password before reaching any other page.
4. Password requirements are defined in §3.1.4.

#### 3.1.4 Password Policy

| Rule | Value | Rationale |
|---|---|---|
| Minimum length | 12 characters | NIST SP 800-63B favours length over composition |
| Maximum length | 128 characters | Prevents hash-cost denial of service |
| Composition rules | None required | Composition rules measurably reduce entropy by pushing users to predictable patterns |
| Unique characters | Minimum 4 | Blocks trivial repeats such as `aaaaaaaaaaaa` |
| Blocklist | Reject the 1,000 most common passwords and any password containing the user's email local-part | Cheap, high-value filter |

This is a deliberate departure from ASP.NET Identity defaults (6 characters, four composition classes), which v2.0 both adopted and misdescribed.

#### 3.1.5 Session Security

| Control | Requirement |
|---|---|
| Session timeout | 30 minutes of inactivity, sliding |
| Timeout enforcement on live sessions | An expired session must be rejected even when the user holds an open interactive connection. Cookie expiry alone does not achieve this; the authentication state must be revalidated on a recurring interval |
| Revocation | Deactivating an employee, changing their role, or resetting their password must invalidate all existing sessions for that account within one revalidation interval (target: 30 minutes, and immediately on the next navigation) |
| Account lockout | 5 failed login attempts locks the account for 15 minutes |
| Login rate limiting | Independent of lockout, login attempts are rate limited per source address and per submitted email. This prevents an attacker from using lockout to deny service to every known user |
| Account enumeration | Login failure responses must be identical for unknown email, wrong password, and locked account |

#### 3.1.6 Identity as Source of Truth

ASP.NET Identity owns authentication data. The `Employee` entity owns business data. Where both need a value, Identity is authoritative:

- **Role.** Role membership lives in Identity roles. `Employee.Role` is a projection maintained by the application whenever a role changes; it exists for querying and reporting convenience only. Authorisation decisions read the Identity role claim, never `Employee.Role`.
- **Email.** The login username is the Identity user's email. Changing an employee's email must update Identity first (which enforces uniqueness and refreshes the security stamp), then update `Employee.Email`. Both writes occur in one transaction. An email change signs the user out of all sessions.
- **Any role change refreshes the security stamp,** which forces re-issue of the authentication cookie and prevents stale elevated privileges.

#### 3.1.7 Account Recovery

Email delivery is out of scope, so recovery is administrative:

- **Admin password reset.** An Admin may set a new temporary password for any employee. Doing so sets `MustChangePassword`, refreshes the security stamp, and terminates that employee's sessions.
- **Admin unlock.** An Admin may clear a lockout before the 15 minutes elapse.
- Both actions are audited (§3.8.2), and neither reveals the employee's existing password, which the system does not store in recoverable form.

---

### 3.2 Department Management

#### 3.2.1 Data Fields

| Field | Type | Notes |
|---|---|---|
| Name | String (required, unique) | e.g., "Finance", "Operations" |
| Description | Text (optional) | Brief description of department function |
| Manager | FK to Employee (optional) | Must be an active employee with Role = Manager or Admin |
| Created At | DateTime | Auto-set (UTC) |

#### 3.2.2 Business Rules

- **Manager assignment.** The reporting structure is department-level only — there is no per-employee manager FK.
- **Deletion protection.** A department cannot be deleted if any employee (active or inactive) is assigned to it. Admin must reassign employees first. Inactive employees are included in this check because their historical records still reference the department.
- **Manager deactivation.** If a department's assigned manager is deactivated, the Manager FK is set to null and all Admins are notified. The department continues to operate without a manager.
- **Self-management.** A department's manager must be an employee; they need not belong to that department.

---

### 3.3 Attendance Tracking

#### 3.3.1 Stored Data Fields

Only real events are stored. Derived states are described in §3.3.7.

| Field | Type | Notes |
|---|---|---|
| Employee | FK to Employee | — |
| Date | Date | The **SCT calendar date** of the clock-in. One record per employee per day |
| Clock In | DateTime (nullable) | UTC timestamp |
| Clock Out | DateTime (nullable) | UTC timestamp |
| Worked Minutes | Integer (nullable, computed) | Whole minutes between Clock Out and Clock In. See ADR-0010 |
| Is Flagged | Boolean | True if Clock Out is missing after end of day |
| Correction Note | Text (nullable) | Admin's reason when manually adjusting |
| Corrected By | FK to Employee (nullable) | Admin who made the correction |
| Corrected At | DateTime (nullable) | When the correction was made (UTC) |

#### 3.3.2 Work Schedule

- **Fixed company-wide:** 08:00–16:00 SCT (UTC+4), Monday to Friday.
- **Late** = Clock In after 08:00 SCT.
- **Early departure** = Clock Out before 16:00 SCT. This is a **derived display flag** on attendance views and the monthly report. It is not stored and does not affect any calculation.
- **Overtime is not tracked.** Clock Out times after 16:00 are recorded as raw timestamps but no overtime calculation is performed.

#### 3.3.3 The Day Boundary

All attendance dates are **SCT calendar dates**, derived by converting the UTC instant to UTC+4 and taking the date component. This is stated explicitly because the naive alternative — using the UTC date — assigns clock-ins between 20:00 and 24:00 UTC to the wrong day. Server local time is never used for any date decision; the container runs in UTC and the users do not.

#### 3.3.4 Clock In/Out Behaviour

1. Employee sees a "Clock In" button on their dashboard.
2. On click, the system records the current UTC instant as Clock In, on the current SCT date. The button changes to "Clock Out".
3. On Clock Out click, the system records the current UTC instant as Clock Out and computes Worked Minutes.
4. **Double-submit prevention.** The button is disabled client-side during the request. Server-side, a unique constraint on (Employee, Date) is the authoritative guard; the service must handle the constraint violation as a normal "already clocked in" result rather than surfacing a database error.
5. **Already clocked out.** Once both Clock In and Clock Out are recorded for the day, both buttons are disabled.
6. **Clock Out before Clock In** is rejected. Admin corrections are subject to the same rule.

#### 3.3.5 Missed Clock-Out Handling

- A nightly job sets `Is Flagged` on any record with a Clock In and no Clock Out for a date that has fully elapsed in SCT, and notifies all Admins.
- The job is **catch-up capable**: on each run it processes every unprocessed date since its last successful run, recorded in a watermark. The application is not assumed to be running continuously, so a job that only ever processes "yesterday" would silently skip days.
- Admin sees flagged records in the dashboard and attendance management.
- Admin sets the Clock Out time with a mandatory Correction Note. The flag clears on correction.

#### 3.3.6 Admin Corrections

- Admin may modify Clock In and Clock Out times for any attendance record, and may create a record for a date with none.
- A Correction Note is mandatory (free text explaining the reason).
- Corrections are logged in the audit trail (§3.8).
- A correction may not move a record to a different employee or a different date. Those cases require deleting and recreating, which is itself audited.

#### 3.3.7 Derived Attendance States

`Present`, `Late`, `Absent`, `Holiday`, `Weekend`, and `OnLeave` are **computed when attendance is read over a date range**, by projecting the range and joining stored records, public holidays, and approved leave. They are not stored. See ADR-0004.

Resolution order for a given employee and SCT date:

1. Date is before the employee's Hire Date, or the employee was Inactive on that date → **NotEmployed** (excluded from all counts)
2. Date is Saturday or Sunday → **Weekend**
3. Date matches a public holiday → **Holiday**
4. Date falls within an Approved leave request → **OnLeave**
5. An attendance record exists with a Clock In after 08:00 SCT → **Late**
6. An attendance record exists with a Clock In at or before 08:00 SCT → **Present**
7. No attendance record exists → **Absent**

The Clock In/Out control is unavailable on any date resolving to Weekend, Holiday, or OnLeave.

---

### 3.4 Leave Management

#### 3.4.1 Leave Types & Entitlements

| Leave Type | Days/Year | Notes |
|---|---|---|
| Annual | 21 | Standard annual leave |
| Sick | 10 | Medical leave |
| Maternity | 90 | Granted by Admin per qualifying event; not auto-renewed |
| Unpaid | No cap | Tracked but not deducted from any balance |
| Compassionate | 5 | Bereavement, family emergencies |

#### 3.4.2 Balance Management

- **Period.** A balance period runs from a hire anniversary to the day before the next one. Annual, Sick, and Compassionate each have one balance row per employee per period.
- **Reset is lazy.** The balance row for the current period is created on first access — before any balance read, validation, or mutation — by an idempotent operation. There is no scheduled reset job. See ADR-0006 for why: the application is not continuously running, so a timer-based reset would silently skip the anniversary of anyone whose reset date fell during downtime.
- **Maternity** is not auto-created. An Admin grants it explicitly (§3.4.7), which creates a balance row with an Admin-set entitlement and an explicit period.
- **Unpaid** has no balance row and is always available.
- **No carry-over.** Unused days do not transfer to the next period.
- **First period proration.** An employee hired mid-year receives the full entitlement for their first period. Their period starts at their Hire Date, so no proration is required.
- **Day counting.** Business days only. Weekends and public holidays within the leave period are excluded.

#### 3.4.3 Request Workflow

1. Employee navigates to Leave → New Request.
2. Employee selects Leave Type, Start Date, End Date, and an optional Reason.
3. **System validates**, in this order, returning the first failure:
   - Employee is Active.
   - Employee is not in the probation period (first 3 months from Hire Date).
   - Start Date is today or later. Backdated requests are rejected.
   - End Date is on or after Start Date.
   - The range does not span a balance reset boundary (hire anniversary). If it does, the employee is prompted to submit two separate requests.
   - The computed business-day count is at least 1. A range consisting only of weekends and holidays is rejected.
   - No overlap with any existing Pending or Approved leave for that employee.
   - Remaining balance ≥ requested business days (skipped for Unpaid).
4. Request is created with status Pending.
5. All Admins receive an in-app notification.
6. An Admin approves or rejects with an optional note.
7. The requesting employee receives an in-app notification of the decision.
8. **On approval**, the balance is decremented. The status change and the balance write occur in one transaction under an optimistic concurrency check, so two Admins approving simultaneously cannot overdraw a balance.

#### 3.4.4 Overlap Semantics

Two leave requests overlap when their inclusive date ranges intersect. Cancelled and Rejected requests are ignored. Because the overlap test is a read followed by a write, the submission path must re-verify under the same transaction that commits the request.

#### 3.4.5 Leave Cancellation

- **Employee-initiated.** An employee may cancel a Pending request at any time, and an Approved request only before its Start Date. Full balance restoration applies.
- **Admin-initiated.** An Admin may cancel any leave at any time.
  - Cancelled before the Start Date → full balance restoration.
  - Cancelled on or after the Start Date → **only the business days from the cancellation date forward are restored.** Days already taken are not returned. v2.0 restored the full amount, which credited employees for leave they had already consumed.
- Cancelled records are retained with status Cancelled for audit purposes.
- The affected employee is notified when an Admin cancels their leave.

#### 3.4.6 Approval Constraints

- An Admin may not approve, reject, or cancel their **own** leave request. Another Admin must act on it. If the organisation has exactly one Admin, that Admin's own requests remain Pending and the UI states why.
- Managers cannot approve leave, consistent with §2.2.

#### 3.4.7 Admin Balance Adjustment

An Admin may adjust any employee's balance for a leave type and period, and may grant Maternity leave. Every adjustment requires a mandatory note, is written in the same transaction as the balance change, and is audited. This is the mechanism for corrections, negotiated allowances, and the "managed manually by Admin" cases that v2.0 described without providing any means to perform.

#### 3.4.8 Leave Statuses

```
Pending  → Approved | Rejected | Cancelled
Approved → Cancelled
```

Rejected and Cancelled are terminal.

---

### 3.5 Dashboard

#### 3.5.1 Admin Dashboard

| Widget | Description |
|---|---|
| Total Headcount | Active employees count, with breakdown by department |
| Pending Leave Requests | Count with quick-action links to approve/reject |
| Today's Attendance | Present / Late / Absent / On Leave / Holiday counts, derived per §3.3.7 |
| Flagged Attendance | Count of unresolved missed clock-outs |
| Attendance Trend | Line chart — daily attendance rate over the last 30 days |
| Leave Trend | Bar chart — leave requests per type over the last 6 months |
| Department Distribution | Pie/doughnut chart — employees per department |

#### 3.5.2 Manager Dashboard

Same widgets as Admin, scoped to the Manager's department per §2.4. No pending-approval widget, since Managers cannot approve.

#### 3.5.3 Employee Dashboard

| Widget | Description |
|---|---|
| Clock In/Out | Primary action button with today's derived status |
| My Attendance This Month | Summary — days present, late, absent |
| My Leave Balances | Remaining days per leave type |
| Recent Notifications | Last 5 notifications |

---

### 3.6 Reports

All reports support date range filtering with preset options (This Month, Last Month, This Year, Last Year) and a custom date range picker. **Preset boundaries are computed in SCT**, consistent with §3.3.3. Export formats: PDF (QuestPDF) and CSV (CsvHelper).

#### 3.6.1 Monthly Attendance Summary

- Per-employee breakdown: days present, late, absent, on leave, holidays.
- Total and average worked hours per day, presented as hours from stored minutes.
- Flagged and corrected entries highlighted.
- Filterable by department.

#### 3.6.2 Leave Balances & Usage Report

- Per-employee: entitlement, used, remaining for each leave type.
- Requests breakdown: approved, rejected, cancelled, pending.
- Filterable by department and leave type.

#### 3.6.3 Department Headcount & Employee Directory

- Per-department: headcount, manager, list of employees.
- Employee details: name, job title, contract type, hire date, status.
- Salary is excluded from this report for all roles.

#### 3.6.4 Access Control

- **Admin:** All reports, all departments, all employees.
- **Manager:** All reports, scoped to their department per §2.4.
- **Employee:** No report access (uses dashboard for personal stats).

Scope is applied server-side when the report data is queried. A Manager cannot widen scope by manipulating the request.

#### 3.6.5 Export Safety

CSV exports must neutralise formula injection. Any exported cell whose value begins with `=`, `+`, `-`, `@`, tab, or carriage return is escaped so that a spreadsheet application treats it as text. This applies to every free-text field, including Reason, Correction Note, Review Note, Address, Job Title, and names.

---

### 3.7 Public Holiday Calendar

#### 3.7.1 Data Fields

| Field | Type | Notes |
|---|---|---|
| Name | String (required) | e.g., "National Day", "Liberation Day" |
| Date | Date (required, unique) | The holiday date |
| Rule | Enum | FixedDate or EasterRelative — see §3.7.4 |
| Easter Offset | Integer (nullable) | Days from Easter Sunday, when Rule is EasterRelative |
| Is System Generated | Boolean | True for a generated entry; false once an Admin edits it, which protects it from regeneration (§3.7.4) |

Recurrence needs no flag of its own: `Rule` already says how next year's date is derived, and every seeded holiday recurs.

`Date` is unique across the whole table. Two holidays cannot share a date; if two observances coincide, they are recorded as one entry with a combined name.

#### 3.7.2 Seed Data

Pre-seeded with Seychelles public holidays:

| Holiday | Rule |
|---|---|
| New Year's Day | Fixed — January 1 |
| New Year Holiday | Fixed — January 2 |
| Good Friday | Easter − 2 |
| Easter Saturday | Easter − 1 |
| Easter Monday | Easter + 1 |
| Labour Day | Fixed — May 1 |
| Liberation Day | Fixed — June 5 |
| Corpus Christi | Easter + 60 |
| National Day | Fixed — June 18 |
| Independence Day | Fixed — June 29 |
| Assumption Day | Fixed — August 15 |
| All Saints' Day | Fixed — November 1 |
| Immaculate Conception | Fixed — December 8 |
| Christmas Day | Fixed — December 25 |

#### 3.7.3 Management

- Admin can add, edit, and delete holidays.
- Holidays affect attendance state resolution (§3.3.7) and leave business-day counting (§3.4.2).
- Deleting a holiday does not retroactively alter already-approved leave day counts, which were fixed at submission time.

#### 3.7.4 Recurrence Generation

Holidays for a calendar year are generated on demand and idempotently: requesting holidays for a year that has not been generated triggers generation for that year first. Generation is also invoked at startup for the current and next year.

- **FixedDate** holidays are projected onto the target year.
- **EasterRelative** holidays are computed from Easter Sunday using the anonymous Gregorian computus, then offset. v2.0 marked four holidays "Variable (calculated)" with no mechanism to calculate them.
- Generation never overwrites a holiday an Admin has edited or deleted for that year.

---

### 3.8 Audit Trail

#### 3.8.1 Data Fields

| Field | Type | Notes |
|---|---|---|
| Entity Type | String | e.g., "Employee", "Attendance" |
| Entity Id | String | PK of the affected record |
| Action | Enum | Created, Updated, Deleted (soft), StatusChanged, SecurityEvent |
| Changed Fields | JSON | Before/after values for each changed field |
| Changed By | FK to Employee (nullable) | Null denotes a system actor |
| Actor Description | String | "System: NightlyAttendanceFlag", or the acting user's email |
| Changed At | DateTime | UTC timestamp |

`Changed By` is nullable because background jobs, the seeder, and startup migrations legitimately change data with no user present. v2.0 made this column required, which would have made those operations impossible.

#### 3.8.2 Scope

- All Employee profile field changes.
- Attendance corrections.
- Leave status changes (submitted, approved, rejected, cancelled) and balance adjustments.
- Employee deactivation and reactivation.
- Department changes.
- **Security events:** login failure, lockout, password change, admin password reset, admin unlock, and role change. v2.0 excluded Identity entirely, leaving no record of authentication activity.

#### 3.8.3 Redaction

Password hashes, security stamps, and authentication tokens are never written to the audit trail, even when present on a changed entity.

#### 3.8.4 Access

Admin only. Read-only — audit entries cannot be modified or deleted through the application.

---

### 3.9 In-App Notifications

#### 3.9.1 Notification Events

| Event | Recipient | Message |
|---|---|---|
| Leave request submitted | All Admins | "{Employee Name} has submitted a {Leave Type} leave request for {dates}" |
| Leave approved | Requesting employee | "Your {Leave Type} leave request for {dates} has been approved" |
| Leave rejected | Requesting employee | "Your {Leave Type} leave request for {dates} has been rejected. Reason: {note}" |
| Leave cancelled by employee | All Admins | "{Employee Name} has cancelled their {Leave Type} leave for {dates}" |
| Leave cancelled by admin | Affected employee | "Your {Leave Type} leave for {dates} has been cancelled by an administrator" |
| Missed clock-out flagged | All Admins | "{Employee Name} did not clock out on {date}" |
| Department manager vacated | All Admins | "{Department} no longer has an assigned manager" |
| Balance adjusted | Affected employee | "Your {Leave Type} balance has been adjusted by an administrator" |

Events addressed to "All Admins" fan out to one notification row per active Admin employee. `Notification` targets a single recipient; there is no role-addressed notification.

#### 3.9.2 Behaviour

- Notifications appear in a bell icon dropdown in the top navigation bar.
- An unread count badge is shown on the bell.
- **The badge updates without a page navigation.** A notification raised while the user is on any page appears within a few seconds, via an in-process publish/subscribe mechanism scoped to the running instance.
- Clicking a notification marks it read and navigates to the relevant page.
- **Auto-purge:** notifications older than 30 days are deleted by a background service.

---

## 4. Non-Functional Requirements

### 4.1 Technology Stack

Versions verified against nuget.org on 2026-08-12.

| Layer | Technology | Version | Purpose |
|---|---|---|---|
| Runtime | .NET | 10.0 (LTS) | Application runtime |
| Language | C# | 14 | Primary language |
| UI Framework | Blazor Web App, Interactive Server | .NET 10 | Interactive server-side rendering |
| UI Components | MudBlazor | 9.8.0 | Material Design component library |
| ORM | Entity Framework Core | 10.0.11 | Data access and migrations |
| Database (local) | SQL Server Express LocalDB | ships with Visual Studio | Development data store |
| Database (container) | SQL Server 2022 (`mcr.microsoft.com/mssql/server`) | 2022-latest | Containerised data store |
| Database provider | Microsoft.EntityFrameworkCore.SqlServer | 10.0.11 | One provider for both targets |
| Authentication | ASP.NET Core Identity | 10.0.11 | User management, roles, login |
| Validation | FluentValidation | 12.1.1 | Command validation |
| PDF Export | QuestPDF | 2026.7.2 | PDF report generation (Community licence) |
| CSV Export | CsvHelper | 33.1.0 | CSV report generation |
| Seed Data | Bogus | 35.6.5 | Fake data generation |

.NET 11 is in preview as of this date. EMS targets .NET 10 LTS and does not track previews.

Two packages are explicitly excluded: **MediatR** and **AutoMapper** both moved to commercial licensing. EMS uses plain service interfaces and hand-written projections.

### 4.2 Data Type Policy

See ADR-0010.

| Concept | Type | Display |
|---|---|---|
| Salary | `decimal(18,2)` | Formatted as SCR |
| Worked time | `int`, whole minutes | Rendered as hours and minutes |
| Leave days | `int`, whole business days | As-is |
| Concurrency tokens | `rowversion` (`byte[]`) | Not displayed |

Money is `decimal`, never a floating-point type. Durations are integer minutes rather than fractional hours, because 7.5 hours is a display concern and minutes are the unit the system actually measures.

### 4.3 Quality & Testing

| Requirement | Specification |
|---|---|
| Test framework | xUnit v3 (3.2.2) on Microsoft.Testing.Platform |
| Mocking | NSubstitute 6.2.0 |
| Assertions | Shouldly 4.3.0 — see ADR-0007 for why not FluentAssertions |
| Component testing | bUnit 2.9.0 |
| E2E testing | Playwright 1.62.0 (Chromium, headless) against a real Kestrel host |
| Time control | Microsoft.Extensions.TimeProvider.Testing 10.9.0 |
| Code coverage | Coverlet 10.0.1 — report-only, no enforced threshold |
| Static analysis | CodeQL, plus Roslyn analyzers at `latest-recommended` with warnings as errors |
| Dependency scanning | `dotnet list package --vulnerable --include-transitive` fails the build on any advisory |
| Code formatting | `dotnet format` with `.editorconfig` carrying explicit severities |

### 4.4 Security Requirements

| Control | Requirement |
|---|---|
| Transport | HTTPS enforced with redirection and HSTS outside Development |
| Auth cookie | HttpOnly, `SameSite=Strict`, Secure outside Development |
| Content Security Policy | Restrictive policy with `frame-ancestors 'none'`; no external script or style origins |
| Additional headers | `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer` |
| Third-party assets | None at runtime. All fonts, scripts, and styles are served from the application. v2.0 loaded fonts from a public CDN, which breaks offline use and any strict CSP |
| Login rate limiting | Per-IP and per-email partitioned limiter on the login endpoint |
| Secrets | No credential appears in source, configuration files, or documentation. The seed Admin password is supplied by environment variable or user-secrets. Startup fails outside Development if it is absent or still the default |
| CSV injection | Neutralised per §3.6.5 |
| Query parameters | Sort and filter column names arriving from the client are validated against a server-side allow-list. Page size is capped |
| Dependency provenance | Central package management with lock files and package source mapping |
| Container | Non-root user, minimal base image, no build tooling in the runtime layer |

### 4.5 Data Protection & Retention

The system stores personal data: date of birth, home address, emergency contacts, and salary.

| Requirement | Specification |
|---|---|
| At rest | Database files are unencrypted. Transparent Data Encryption is not available in LocalDB or Express editions. Accepted for the local-only deployment scope and must be revisited before any hosted deployment (ADR-0009) |
| Backup | `BACKUP DATABASE` to a mapped volume, with a documented restore procedure. Copying live `.mdf`/`.ldf` files is not a backup |
| In transit | Connections use encryption. Local development trusts the self-signed server certificate; a deployed environment must not |
| Audit retention | Indefinite. Audit entries are never purged |
| Notification retention | 30 days |
| Access | Salary and audit data are Admin-only, enforced server-side per §2.5 |

### 4.6 General

| Requirement | Specification |
|---|---|
| Architecture | Clean Architecture (multi-project solution) — see ADR-0003 for the boundary actually enforced |
| Deployment topology | Single application instance. Multi-instance is not in scope for v1.0, though the datastore no longer prevents it |
| Timezone | Store UTC, display SCT (UTC+4, no DST) |
| Currency | SCR (Seychellois Rupee) |
| Theme | Light mode only |
| Responsive | Desktop-only |
| Session | 30-minute inactivity timeout, revalidated (§3.1.5) |
| Lockout | 5 failed attempts, 15-minute lockout |

### 4.7 Observability

| Requirement | Specification |
|---|---|
| Logging | Structured logging via `ILogger`, with the acting user and request correlation on every entry |
| Log levels | Warning and above in Production; Information for application events |
| Sensitive data | Passwords, tokens, and salary values are never logged |
| Error handling | Unhandled exceptions produce a correlation identifier shown to the user and logged in full server-side. Exception detail is never rendered to the browser outside Development |
| Health | `/health` reports application and database status, anonymously accessible, with no detail beyond healthy/unhealthy |

### 4.8 Accessibility Baseline

Full WCAG conformance is not a v1.0 goal, but the following are required because retrofitting them is expensive:

- Every form input has an associated label.
- All functionality is reachable by keyboard, including dialogs and the data grid.
- Focus is visible and is moved into dialogs on open and restored on close.
- Colour is never the sole carrier of meaning; attendance and leave states carry a text or icon indicator alongside colour.
- Text contrast meets 4.5:1.

---

## 5. Seed Data Specification

### 5.1 Default Admin Account

| Field | Value |
|---|---|
| Email | `admin@ems.local` |
| Password | Supplied via `Seed__AdminPassword` environment variable or user-secrets. Never committed |
| Name | System Administrator |
| Role | Admin |
| MustChangePassword | true |

If the variable is absent in Development, a random password is generated and written to the startup log once. Outside Development, startup fails rather than defaulting.

### 5.2 Fake Data (Bogus)

Seeding is idempotent: it checks for existing data and does nothing if the database is already populated. The Bogus randomizer is seeded with a fixed value so that generated data is reproducible across runs and machines.

- **Departments:** 5 (Finance, Human Resources, Operations, IT, Marketing).
- **Employees:** 15 distributed across departments with Seychelles-contextual data.
- **Managers:** 1 Manager per department.
- **Attendance:** 30 days of historical clock-in/out events, including a small number of late arrivals and one missed clock-out.
- **Leave:** A mix of approved, rejected, pending, and cancelled requests, with matching balance rows.
- **Public holidays:** Generated for the current and next year.

Seeding is enabled only when `AppSettings:SeedData:Enabled` is true, which defaults to false.

---

## 6. Out of Scope

Explicitly excluded from v1.0:

- Email notifications (in-app only)
- Payroll calculations or salary slips
- Document uploads (contracts, ID copies, profile photos)
- Mobile-responsive layout
- Dark mode
- Multi-location / multi-timezone support
- Employee self-registration (Admin creates all accounts)
- Public API endpoints for external integration
- Overtime tracking and calculation
- Half-day leave requests
- Multi-department employee membership
- Carry-over of unused leave days
- Cloud deployment (local dev + Docker only)
- Two-factor authentication and passkeys — supported by the Identity stack, deliberately not enabled in v1.0
- Full WCAG 2.2 AA conformance (baseline only, §4.8)
