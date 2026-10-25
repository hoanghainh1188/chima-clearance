# Feature Specification: Chima Customs B2 — Vehicle Clearance Log Replacement

**Feature Branch**: `001-b2-clearance`
**Created**: 2026-05-03
**Status**: Draft
**Input**: User description: "Đây là dự án thay thế quy trình quản lý xe thông quan bằng Excel của Hải quan Cửa khẩu Chi Ma. Toàn bộ functional spec đã có sẵn trong specs/03-functional-spec.md (18 features từ F-01 đến F-18). Hãy đọc file đó và tạo spec.md theo format Spec Kit, ánh xạ 1-1 với các feature ID đã có. Giữ nguyên Gherkin acceptance criteria. Không phát minh thêm feature nào ngoài 18 feature đã liệt kê. Tham chiếu thêm: specs/01-overview.md cho bối cảnh, business rules cột A-P trong 'Business rules - Hướng dẫn điền sổ B2.md'."

> **Authoritative source**: this file restates and groups, in Spec Kit form, the eighteen
> features defined in [`specs/03-functional-spec.md`](../03-functional-spec.md). Each user
> story below is anchored to one Feature ID (F-01 … F-18). Gherkin acceptance scenarios are
> preserved verbatim from the source. No new features have been added. Where the source and
> this file disagree on wording, the source wins.
>
> **Priority convention**: this project predates Spec Kit and uses **P0 = must-ship**, **P1 =
> should-ship**, **P2 = nice-to-have** (see [`specs/03-functional-spec.md`](../03-functional-spec.md)
> Feature Index). Priorities below preserve the source labels rather than remapping to Spec
> Kit's P1-is-highest convention, to avoid silent drift.

---

## Clarifications

### Session 2026-05-03

- Q: How long should declaration / vehicle / audit-log data remain in the live SQLite database before being archived or deleted? → A: 3 years online, then archived annually to read-only files (Excel + PDF) in a sibling folder; archived data is not editable from the app.
- Q: How should an erroneously-entered declaration be cancelled or voided? → A: Admin-only cancellation, allowed any time before the declaration reaches `KVGSConfirmed`. Mandatory reason. Status flips to `Cancelled`; the row is soft-deleted (kept in DB and exports, rendered struck-through) for audit; no vehicle under a `Cancelled` declaration can be exited or re-used.
- Q: When two clients with different idempotency keys submit exit on the same vehicle near-simultaneously, what should happen? → A: First commit wins; the second client receives `VEHICLE_ALREADY_EXITED` (409) with a Vietnamese message naming the operator who succeeded and the recorded exit time. Same error code already used for the single-client re-mark race in F-08.
- Q: Who or what populates the on-duty-officer name (`OnDutyOfficerName`) on the F-11 daily header? → A: Free-text field on today's daily-header row, inline-editable by any authenticated user (Admin / Operator / Supervisor) directly from the ledger. Initially empty when the header is auto-created at 00:01; every set / change writes an AuditLog row. No shift-scheduling UI is added for Phase 1.
- Q: How can Admin recover encrypted backups if the install-time password is lost? → A: At install the system generates a one-time printable recovery code that Admin must record offline (paper / safe). Either the install password OR the recovery code decrypts any backup. Loss of one is recoverable from the other; loss of both makes backups unrecoverable (accepted risk; install flow must make the recovery-code save step explicit and confirmed).
- [UI] Re-sync 2026-05-03: `specs/05-ui-wireframes.md` was added after the initial `/speckit.specify` run. Spec updated in place — not regenerated — to (a) reference `§ 6` from "User Scenarios & Testing", (b) add a per-feature `UI:` line citing the corresponding wireframe via the `§ 10` cross-reference, (c) introduce a new "UI Acceptance Criteria" section copying `§ 9` Gherkin verbatim, and (d) introduce a new "Non-Functional Requirements" section binding the `§ 2–3` design tokens, the page-fidelity rule, the four list-view states, the status-indicator rule, the reusable Toast/Modal service rule, and the Vietnamese-copy-verbatim rule. All existing Gherkin scenarios preserved unchanged.
- [UI] Q: Should server-side role authorization mirror the sidebar visibility matrix in `specs/05-ui-wireframes.md § 4` exactly, or stay more permissive than the UI? → A: API mirrors the sidebar exactly. Operator is denied on KVGS / Reports / Export; Supervisor is denied on Entry / Exit; Admin is allowed everywhere. UI sidebar visibility and API authorization share one source of truth (codified as new FR-027). Adds 403-Forbidden Gherkin coverage for Operator-on-Reports/Export and Supervisor-on-Entry/Exit at test time.
- [UI] Q: F-17 (Smart Suggestions) is marked P2 (deferrable) in the original spec, but `specs/05-ui-wireframes.md § 6.3` shows ghost-text suggestions as a binding part of the Entry Form ("must work from Phase 1 even if simple"). Defer or promote? → A: Promote F-17 to **P1** with a deliberately small "simple v1" scope: top-1 suggestion only, derived from last-90-day pair-count statistics, no ML, no tuning UI. The Entry Form wireframe stays binding; the Phase-1 GA checklist now includes F-17 (alongside F-14, F-16, F-18). Implementation lives in S6 (Hardening) — no sprint resequencing required.
- [UI] Q: `specs/05-ui-wireframes.md § 6.7` (Export Excel) shows three checkboxes — daily-header rows / status colours / note column — but `spec.md` FR-015 says all three MUST be preserved in every export. Toggleable or always-on? → A: Always-on at runtime. The three options render as **read-only "Đã bật" badges** at the same screen position as the wireframe checkboxes (smallest wireframe deviation; copy unchanged). The legacy archive contract (FR-015, F-13 layout test) is preserved; UI still surfaces what the export will contain.
- [UI] Q: `specs/05-ui-wireframes.md § 5` defines a tri-state connectivity badge (🟢 Online / 🟡 Đang thử lại / 🔴 Mất kết nối), but `spec.md` FR-011 only addresses 🔴. Are writes permitted during 🟡 polling fallback? → A: Writes are **permitted** during 🟡 because the HTTP channel to the server is still up — only the SignalR push has degraded to polling. The yellow banner stays visible to make the degradation explicit; other clients converge within the polling interval (target ≤ 3 s). FR-011's "refuse writes" rule is **scoped to 🔴 only** and reworded accordingly; new FR-028 codifies the tri-state contract; F-10 gains one acceptance scenario for write-during-🟡 + ≤ 3 s convergence.

### Session 2026-09-27 — initial Admin password

- Q: Every install shipped the same seeded Admin password, reachable from the LAN until the
  first login. Should an upgrade replace a still-default password, and where is it shown?
  → A (owner): **Generate per install** (FR-017 amended, ADR-0009); upgrades **do** replace a
  password the Admin never changed, never one they chose; shown **on screen only**, no file;
  `B2Clearance.exe reset-initial-admin` re-issues it if lost before the first login.

### Session 2026-08-01 — business-rule review against the customer's own documents

- Q: FR-027 denies Operator on KVGS and denies Supervisor on Entry/Exit, so no single non-Admin
  role can work the exit gate. `Business rules — Hướng dẫn điền sổ B2` § B.2 has one cán bộ at
  cổng ra tick "Xe đã ra" (cột J), then "Xác nhận qua KVGS" (cột L), then write their own name
  into "Người xác nhận" (cột N), and § 3 lists exactly five columns that same officer may edit.
  Which is authoritative? → A: **The business document.** The Supervisor-only rule traces to
  INV-6, whose stated source is "Business rule cột L" — a citation that document does not
  support; and FR-027 itself came from the [UI] clarification "should the API mirror the sidebar
  exactly", i.e. it was derived from an internally-authored wireframe, not from the customer.
  KVGS confirmation is now open to every authenticated role (new `GateStaff` policy); Supervisor
  keeps it. Entry/Exit stay Operator+Admin. INV-6 keeps only its `AllExited` precondition, and
  the wireframe § 4 sidebar matrix shows KVGS for all three roles.

- Q: Cột C says "có nhiều dãy số thì mỗi dãy số sẽ tách thành từng dòng riêng biệt trong cùng 1
  ô" — can one parent row carry several declaration numbers, and is the cell editable after the
  row exists? → A (unit, 2026-08-01): **yes to both.** A declaration now owns an ordered list of
  numbers (`DeclarationNumbers` table, 1..20 per row). Ordinal 0 stays cached on
  `Declaration.DeclarationNumber` so ledger rows, search results and reports keep showing a single
  value. INV-1 uniqueness moves to the child table (unique index filtered on `IsCancelled`, which
  mirrors the parent so FR-022 re-entry still works). `POST /api/declarations/{id}/numbers` and
  `DELETE .../numbers/{number}` (GateOperator — cột C is the entry gate's column) edit the list
  afterwards; a row must keep at least one number, and the list is frozen once the declaration is
  cancelled or KVGS-confirmed. The Excel export writes the numbers on separate lines inside the
  one cell, as the legacy file did with Alt+Enter.

- Q: Cột E says a plate is auto-upper-cased (`12h05769` → `12H05769`) and the column may be
  pasted from the unit's internal system, where plates carry a dash. Reject `29C-12345`, or
  normalise it? → A (unit, 2026-08-01): **normalise**. `PlateNumber.Normalize` upper-cases and
  strips separators (space, NBSP, `-`, en-dash, `.`, `_`, `/`) before the format check, and both
  request validators call it so the API and the domain cannot disagree. The same rule is applied
  to cột F (`TrailerOrContainerNumber`) — same paste source, and F-17 pair statistics would
  otherwise split across `51R-09876` and `51R09876`. The Desktop entry form normalises the field
  in place on submit (it keeps its own copy of the rule: Constitution V forbids B2.Desktop
  referencing B2.Domain).

- Q: Business rules § 3 lists "Ghi chú" (cột O) among the columns the exit gate may fill, but
  `Vehicle.Note` is write-once at entry with no update path. → A: Add an audited note update
  available to gate staff, so the exit-gate officer can record what they see (xe hỏng, hàng có
  vấn đề). Audit stays append-only: each edit writes an `Update` AuditLog row.

- Q: `IX_Declarations_DeclarationNumber` is unique across **all** declarations, so a number on a
  declaration cancelled under FR-022 can never be entered again — the correction path for a
  mistyped entry dead-ends and a real customs number becomes unusable. → A: Scope INV-1 to
  non-cancelled declarations (filtered unique index). Business rule cột C only ever asked for a
  duplicate to be flagged ("cảnh báo bằng cách tô đỏ dãy số"); blocking a duplicate of a **live**
  declaration is kept as the stricter reading, but it no longer traps corrections.

---

## User Scenarios & Testing *(mandatory)*

> **UI wireframes**: detailed in [`specs/05-ui-wireframes.md § 6`](../05-ui-wireframes.md)
> (12 page wireframes — Login, Ledger, Entry Form, Exit Search, KVGS Confirm, Reports,
> Export Excel, Admin Users, Admin Master Data, Admin Backup/Restore, Admin Health & Logs,
> Admin Audit Log). Each per-feature user story below carries an explicit `UI:` line
> mapping it to the corresponding wireframe page per the cross-reference table at
> [`§ 10`](../05-ui-wireframes.md).

### User Story F-01 — User Authentication & Session (Priority: P0)

**UI**: see [`specs/05-ui-wireframes.md § 6.1`](../05-ui-wireframes.md) (Login).

A staff member at the customs gate logs into the desktop client so every subsequent action
they take (create declaration, mark exit, confirm KVGS) is attributable to them, and so that
unauthorized people cannot operate the gate.

**Why this priority**: nothing else in the system can be trusted without an identified
actor — every audit log row, every role check, and every operator-vs-supervisor flow
depends on a working login.

**Independent Test**: starting from a freshly installed system, the default Admin can log
in, be forced to change password on first use, then log in again with the new password and
reach the role-specific home screen.

**Acceptance Scenarios**:

```gherkin
Feature: Login

Scenario: Successful login as Operator
  Given user "nam" with role Operator exists and is active
  When they submit username "nam" and the correct password
  Then they receive a 200 response with a JWT and refresh token
  And the JWT contains claim "role" = "Operator"
  And the JWT lifetime is 8 hours

Scenario: Locked after failures
  Given user "nam" exists
  When they submit a wrong password 5 times within 10 minutes
  Then the account is locked for 15 minutes
  And the 6th attempt returns 423 Locked

Scenario: Auto-logout on idle
  Given a logged-in Operator
  When the desktop app detects no input for 30 minutes
  Then the session is cleared and the login page is shown
```

---

### User Story F-02 — User & Role Management (Priority: P0)

**UI**: see [`specs/05-ui-wireframes.md § 6.8`](../05-ui-wireframes.md) (Admin: Users).

The Admin creates and maintains the user accounts (Operator, Supervisor, Admin) that staff
use at the gate, including forcing a password change on first login and deactivating staff
who leave.

**Why this priority**: without role management, F-01 has nobody to authenticate; without
deactivation, the audit trail cannot be preserved when staff leave.

**Independent Test**: Admin creates a Supervisor account with a temporary password, the new
user logs in, is forced to change password, and afterwards an Operator-level call to a
user-management endpoint is rejected.

**Acceptance Scenarios**:

```gherkin
Scenario: Create user
  Given I am Admin
  When I create user "linh" with role "Supervisor" and a temporary password
  Then "linh" appears in the user list as Active
  And on first login "linh" is forced to change password

Scenario: Cannot delete users — only deactivate
  Given user "linh" has 50 audit log entries
  When I try to delete "linh"
  Then deletion is forbidden
  But deactivation is allowed and "linh" can no longer log in

Scenario: Role enforcement on API
  Given I am Operator
  When I call POST /api/users
  Then I receive 403 Forbidden
```

---

### User Story F-03 — Master Data: Processors (Người làm thủ tục) (Priority: P0)

**UI**: see [`specs/05-ui-wireframes.md § 6.9`](../05-ui-wireframes.md) (Admin: Master Data, Processors tab).

The Admin maintains the canonical list of declaration processors. Operators pick from this
list when entering a declaration; free text is rejected so historical reports stay clean.

**Why this priority**: this is the data-quality control that the legacy Excel did not have
(business rule cột B). Without it, name typos make per-processor reports meaningless.

**Independent Test**: Admin adds processor "Nguyễn Văn Nam"; Operator sees him in the
dropdown; Operator submitting "Nguyễn Văn Lam" (a misspelling) gets a clear validation
error.

**Acceptance Scenarios**:

```gherkin
Scenario: Add processor
  Given I am Admin
  When I add processor "Nguyễn Văn Nam"
  Then he appears in the dropdown when entering a declaration

Scenario: Reject unknown processor on entry
  Given processor "Nguyễn Văn Lam" does NOT exist
  When an Operator submits a declaration with processor "Nguyễn Văn Lam"
  Then the API returns 400 with code "MASTER_PROCESSOR_NOT_FOUND"
  And the desktop app shows: "Tên người làm thủ tục không hợp lệ"

Scenario: Deactivate without breaking history
  Given processor "Nam" has 200 historical declarations
  When I deactivate "Nam"
  Then he disappears from new-entry dropdowns
  But existing declarations still show "Nam"
```

---

### User Story F-04 — Master Data: Yards (Tên bãi) (Priority: P0)

**UI**: see [`specs/05-ui-wireframes.md § 6.9`](../05-ui-wireframes.md) (Admin: Master Data, Yards tab).

The Admin maintains the canonical list of yards (e.g., "QP — Bãi Quân Phục"). Operators
pick from this list during entry; yard codes are unique.

**Why this priority**: same data-quality argument as F-03 (business rule cột H); per-yard
reporting depends on it.

**Independent Test**: Admin adds yard with code "QP"; the yard appears in the entry-form
dropdown; a second yard with the same code is rejected.

**Acceptance Scenarios**:

```gherkin
Scenario: Yard code uniqueness
  Given yard with code "QP" exists
  When I add another yard with code "QP"
  Then I receive 409 Conflict

Scenario: Yard appears in entry form
  Given an active yard "QP — Bãi Quân Phục"
  When an Operator opens the entry form
  Then "QP" is in the yard dropdown
```

---

### User Story F-05 — Vehicle Entry (Entry Gate) (Priority: P0)

**UI**: see [`specs/05-ui-wireframes.md § 6.3`](../05-ui-wireframes.md) (Entry Form — Nhập xe vào).

The Operator at the entry gate creates a new declaration together with its first vehicle
in a single screen, replacing the Excel "dòng cha" pattern.

**Why this priority**: this is the primary write path of the system — without it, no other
flow has anything to operate on.

**Independent Test**: with one Processor and one Yard already in master data, the Operator
fills the entry form with valid values and a 12-digit declaration number, submits, and the
ledger immediately shows a new parent row with the vehicle as the first child row.

Form fields (per source spec):

| Field | Type | Validation |
| ----- | ---- | ---------- |
| Số tờ khai | text | Exactly 12 digits, unique |
| Người làm thủ tục | dropdown (master) | Required, must be active |
| Tên công ty | text | Required, max 200 |
| Tên hàng | text | Required, max 200 |
| Tên bãi | dropdown (master) | Required, must be active |
| Biển số xe | text | Required; auto-uppercased; pattern `^[0-9]{2}[A-Z]{1,2}[0-9]{4,5}$` |
| Số mooc/container | text | Optional; auto-uppercased |
| Ghi chú | textarea | Optional, max 500 |

**Acceptance Scenarios**:

```gherkin
Scenario: Happy path entry
  Given I am Operator at Entry Gate
  And processor "Nam" and yard "QP" exist
  When I submit a valid declaration with plate "12h05769"
  Then the declaration is created
  And the vehicle's PlateNumber is stored as "12H05769"
  And EntryTime equals the server clock
  And EnteredByUserId equals my user id

Scenario: Duplicate declaration number
  Given declaration "123456789012" already exists
  When I submit another declaration with the same number
  Then the API returns 409 with code "DECLARATION_NUMBER_DUPLICATE"
  And the UI highlights the field in red

Scenario: 12-digit validation
  When I submit declaration number "12345"
  Then the API returns 400 with code "DECLARATION_NUMBER_INVALID"

Scenario: Server timestamp (not client clock)
  Given my client clock is wrong (off by 1 hour)
  When I create an entry at 14:00 server time
  Then EntryTime stored is 14:00 (server time)
```

---

### User Story F-06 — Add Child Vehicle to Existing Declaration (Priority: P0)

**UI**: see [`specs/05-ui-wireframes.md § 6.3`](../05-ui-wireframes.md) (Entry Form, "Thêm xe vào tờ khai có sẵn" mode).

Some declarations cover multiple vehicles entering at different times. The Operator adds a
second (or third, …) child vehicle to an open declaration without re-entering the
declaration metadata.

**Why this priority**: matches the legacy "dòng cha / dòng con" pattern (business rule
cột C). Without it, multi-vehicle declarations would be unusable.

**Independent Test**: starting from a declaration created in F-05, the Operator adds a
second vehicle; the ledger now shows the parent row plus two child rows in the Sổ B2 layout.

**Acceptance Scenarios**:

```gherkin
Scenario: Add second vehicle to a declaration
  Given declaration "123456789012" exists with 1 vehicle
  When I add a vehicle with plate "29A12345"
  Then the declaration now has 2 vehicles
  And only the first vehicle's row shows the declaration metadata in the UI/Excel
  And subsequent vehicle rows show empty declaration columns (per Sổ B2 layout)

Scenario: Cannot add to KVGS-confirmed declaration
  Given declaration is KVGSConfirmed
  When I add a vehicle to it
  Then the API returns 409 with code "DECLARATION_LOCKED"
```

---

### User Story F-07 — Vehicle Search by Plate Suffix (Priority: P0)

**UI**: see [`specs/05-ui-wireframes.md § 6.2`](../05-ui-wireframes.md) (Ledger — search bar)
and [`§ 6.4`](../05-ui-wireframes.md) (Exit Search & Confirm).

Any user (typically the Exit Operator) finds a vehicle by typing the last 3 or 4 digits of
its plate; only currently-inside vehicles are matched, replicating the legacy "cột P"
behaviour.

**Why this priority**: this is the lookup that drives F-08; the gate cannot operate
without sub-second suffix search.

**Independent Test**: with one un-exited vehicle "12H05769" and one exited "11H05769", the
Operator searches "5769" and sees only the un-exited one.

**Acceptance Scenarios**:

```gherkin
Scenario: Search returns only un-exited vehicles by default
  Given vehicles with plates "12H05769" (un-exited) and "11H05769" (exited)
  When I search suffix "5769"
  Then only "12H05769" is returned

Scenario: Search 3 or 4 digits
  Given a vehicle "29A12345"
  Then both "2345" and "345" find it

Scenario: Multiple matches
  Given two un-exited vehicles ending in "5769"
  Then both are returned, ordered by EntryTime ascending
  And the UI shows them as a selectable list (no auto-pick)

Scenario: Performance
  Given 50,000 vehicles in DB
  When I search any suffix
  Then results return in < 200 ms p95
  And an index on PlateNumber substring (or generated last-4 column) backs the query
```

---

### User Story F-08 — Mark Vehicle Exit (Exit Gate) (Priority: P0)

**UI**: see [`specs/05-ui-wireframes.md § 6.4`](../05-ui-wireframes.md) (Exit Search & Confirm — Xác nhận xe ra).

The Operator at the exit gate marks a vehicle as exited. When all vehicles under a
declaration are exited, the declaration auto-promotes to "AllExited" and the parent row
turns green for everyone.

**Why this priority**: the exit half of the round-trip; without it, declarations never
become eligible for KVGS confirmation.

**Independent Test**: starting from F-05/F-06 state, the Operator marks each vehicle's
exit; after the last one, the parent row turns green in real time on a second client.

**Acceptance Scenarios**:

```gherkin
Scenario: Mark exit
  Given vehicle "12H05769" is inside
  When I mark its exit
  Then ExitMarked = true, ExitTime = server clock
  And the row text turns black (was red)
  And ExitConfirmedByUserId = my user id

Scenario: Auto-promote declaration to AllExited
  Given declaration with 3 vehicles, 2 already exited
  When I mark the 3rd exit
  Then declaration.Status flips to "AllExited"
  And the parent row turns green in all clients
  And a SignalR event "DeclarationReadyForKvgs" is broadcast

Scenario: Idempotent retry
  Given a Polly retry sends the same exit twice with the same Idempotency-Key
  Then the second call returns the same result as the first
  And only one AuditLog entry is written

Scenario: Cannot un-exit (Operator)
  Given a vehicle is already exited
  When an Operator tries to mark it again
  Then the API returns 409 with code "VEHICLE_ALREADY_EXITED"

Scenario: Admin un-exit override
  Given I am Admin
  When I call POST /api/vehicles/{id}/un-exit with reason "wrong vehicle"
  Then ExitMarked = false, ExitTime = null
  And AuditLog stores the reason
```

---

### User Story F-09 — KVGS Confirmation (Priority: P0)

**UI**: see [`specs/05-ui-wireframes.md § 6.5`](../05-ui-wireframes.md) (KVGS Confirm Page).

A Supervisor (or Admin) confirms that a declaration's vehicles have passed Khu Vực Giám
Sát; the declaration row reverts to normal colour and is locked from further edits.

**Why this priority**: closes the gate's business cycle; until KVGS is confirmed, the
declaration is still "in flight" from a customs perspective.

**Independent Test**: a declaration in AllExited state (from F-08) is confirmed by a
Supervisor; the row visibly reverts and any subsequent attempt to add a child vehicle is
rejected.

**Acceptance Scenarios**:

```gherkin
Scenario: Confirm KVGS
  Given declaration is in status "AllExited"
  When I (Supervisor) confirm KVGS
  Then status flips to "KVGSConfirmed"
  And KvgsConfirmedByUserId = my user id, KvgsConfirmedAt = server clock
  And the parent row reverts to normal color
  And the row becomes read-only for everyone (Operators cannot add vehicles)

Scenario: Operator cannot confirm
  Given I am Operator
  When I try to confirm KVGS
  Then the API returns 403

Scenario: Cannot confirm before AllExited
  Given declaration is in status "Open"
  When I try to confirm KVGS
  Then the API returns 409 with code "DECLARATION_NOT_READY_FOR_KVGS"
```

---

### User Story F-10 — List & Filter Declarations (Priority: P0)

**UI**: see [`specs/05-ui-wireframes.md § 6.2`](../05-ui-wireframes.md) (Ledger — Default Home).

Any user views the rolling ledger that replaces the Excel main sheet, with filters for
date range, status, plate, processor, yard, and officer, and live updates pushed when
other users change the data.

**Why this priority**: this is the screen operators look at all day; if it does not work,
the system is not usable even when all writes succeed.

**Independent Test**: opening the ledger after F-05/F-06/F-08 actions on another client
shows the changes within one second without manual refresh.

Filters (per source spec):

- Date range (default: today)
- Status (Open / AllExited / KVGSConfirmed / Cancelled / All) — Cancelled is excluded
  from the default view per FR-023; users opt in via the Status filter
- Plate (full or suffix)
- Processor (multi-select)
- Yard (multi-select)
- Officer (entered by / confirmed by)

**Acceptance Scenarios**:

```gherkin
Scenario: Default view shows today, newest first
  Given declarations exist across multiple days
  When I open the ledger
  Then I see today's declarations only
  And ordered by EntryTime desc

Scenario: Live updates via SignalR
  Given I am viewing the ledger
  When another user creates / exits / KVGS-confirms a declaration
  Then my view updates within 1 second without refresh
  And no full refetch happens (delta updates only)

Scenario: Writes permitted in 🟡 polling-fallback (UI clarification Q4)
  Given the SignalR connection has dropped and the client has fallen back to polling
  And the connectivity badge shows 🟡 "Đang thử lại" with the yellow banner visible
  When I submit a write (create declaration / mark exit / confirm KVGS)
  Then the write is accepted by the server
  And the row appears on every other connected client within 3 seconds via the polling delta
  And the yellow banner remains visible until SignalR reconnects (no toast spam)

Scenario: Pagination
  Given 1000 declarations match my filter
  Then they paginate at 50/page
  And jumping to page 20 returns in < 300 ms
```

---

### User Story F-11 — Daily Header Auto-Creation (Priority: P0)

**UI**: see [`specs/05-ui-wireframes.md § 6.2`](../05-ui-wireframes.md) (Ledger — daily header
row appears as the first row of each day's block).

A "daily header" row (showing date, on-duty officer, total vehicles for the day) is
inserted automatically at 00:01 every day, matching the legacy Excel rule "00h01 mỗi
ngày".

**Why this priority**: this row is what gives the day-by-day Excel export its visible
structure; without it the legacy form layout is broken.

**Independent Test**: at 00:01 on a fresh day a header row exists; restarting the service
at 06:30 after a 00:01 outage still produces today's header.

**Acceptance Scenarios**:

```gherkin
Scenario: Header created at 00:01
  Given today is a new day
  When the clock hits 00:01
  Then a DailyHeader record exists for today
  And it appears as the first row in the ledger / Excel export

Scenario: Catch-up on startup
  Given the server was off at 00:01 and started at 06:30
  When the service starts
  Then the missing DailyHeader is created immediately for today

Scenario: Idempotent
  When the job runs twice the same day
  Then only one DailyHeader exists for that date
```

---

### User Story F-12 — Daily Summary Auto-Generation (23:00) (Priority: P0)

**UI**: see [`specs/05-ui-wireframes.md § 6.2`](../05-ui-wireframes.md) (Ledger — total
vehicles cell on the daily header row; Admin "Recompute" surfaced via the row's context action).

At 23:00 each day the system populates the daily header's total vehicle count, matching
the legacy rule "tổng kết vào hồi 23h00". Admin can also re-run the summary on demand.

**Why this priority**: makes the daily header complete and gives leadership a same-day
total without manual counting.

**Independent Test**: on a day with N vehicle entries, at 23:00 the daily header's total
field equals N; an Admin "Recompute" button refreshes it and writes an audit row.

**Acceptance Scenarios**:

```gherkin
Scenario: Summary at 23:00
  Given declarations and vehicles exist for today
  When the clock hits 23:00
  Then DailyHeader.TotalVehicles = COUNT(vehicles entered today)
  And the row's "summary" cell is populated

Scenario: Re-run on demand
  Given an Admin clicks "Recompute today's summary"
  Then the summary recomputes immediately
  And an audit log entry records who triggered it
```

---

### User Story F-13 — Excel Export (Template-Based) (Priority: P0)

**UI**: see [`specs/05-ui-wireframes.md § 6.7`](../05-ui-wireframes.md) (Export Excel — Xuất Sổ B2).

Supervisor or Admin exports a date range to an Excel file based on the legacy "Mẫu sổ B2"
template, preserving column order, daily-header rows, and conditional colouring (red for
un-exited, green for AllExited).

**Why this priority**: this is the bridge to the customs unit's existing paper/Excel
archive workflow; until Phase 3 brings API integration, Excel is the hand-off format.

**Independent Test**: with one fully-populated day in the system, the Excel export
produces a file that opens in Office 2010 (or LibreOffice as a proxy) with rows and
colours matching the legacy form.

Behaviour (per source spec):

- Template: `Mẫu sổ B2.xlsx` (shipped with installer at `C:\ProgramData\B2\templates\`).
- Fill columns A–O for each row in the date range.
- Apply conditional formatting:
  - Red font for vehicles with `ExitMarked=false`
  - Green fill for parent rows where `Status=AllExited`
  - Reset to default for `KVGSConfirmed`
- Daily header rows are inserted at the top of each day's block.
- File name: `SoB2_{from:yyyyMMdd}_{to:yyyyMMdd}.xlsx`.
- **UI presentation of export options (UI clarification Q3, 2026-05-03)**: the three
  "Bao gồm:" items in [`specs/05-ui-wireframes.md § 6.7`](../05-ui-wireframes.md)
  (daily-header rows, status colour formatting, note column) are **always applied** at
  runtime per FR-015. They render as read-only **"Đã bật" badges** in place of the
  wireframe's checkbox controls; the surrounding copy and screen position are
  unchanged. No code path produces a Sổ B2 file with any of the three transformations
  disabled.

**Acceptance Scenarios**:

```gherkin
Scenario: Export 1 day
  Given today has 120 vehicles across 40 declarations
  When I export today
  Then the file has 1 daily header row + 40 declaration rows + child vehicle rows
  And the column count and order match the legacy Sổ B2

Scenario: Color preservation
  Given a vehicle that has not exited
  Then in the exported file, that row's text color is red

Scenario: Performance
  Given a 30-day export with ~3,500 declarations
  Then export completes in < 10 s on the spec hardware
```

---

### User Story F-14 — Reports (Priority: P1)

**UI**: see [`specs/05-ui-wireframes.md § 6.6`](../05-ui-wireframes.md) (Reports — Báo cáo).

Supervisor or Admin pulls aggregated reports: by Processor, by Yard, by Hour, by Officer,
and Pending. These replace the ad-hoc COUNTIFs that staff currently run on the Excel.

**Why this priority**: high operational value but not blocking the entry/exit/KVGS round
trip; can ship after the core flow is working.

**Independent Test**: with N exited vehicles in a known mix of processors/yards/hours, the
five reports return numbers that match a hand count.

| Report | Inputs | Output |
| ------ | ------ | ------ |
| By Processor | from, to | List `{processor, totalVehicles}` |
| By Yard | from, to | List `{yard, totalVehicles}` |
| By Hour | date | List `{hourBucket, exitedCount}` (e.g., 13:30–14:30 = 23 vehicles) |
| By Officer | from, to | List `{officer, vehiclesEntered, declarationsKvgsConfirmed}` |
| Pending | asOf | List `{vehicle, hoursInside}` for not-yet-exited vehicles |

**Acceptance Scenarios**:

```gherkin
Scenario: Hour buckets are configurable
  When I request /reports/by-hour with bucketMinutes=30
  Then results split into 30-minute buckets
  And default bucketMinutes = 60

Scenario: Pending report counts vehicles, not declarations
  Given declaration with 3 vehicles, 1 still inside
  Then the pending report shows 1 row for that declaration's pending vehicle
```

---

### User Story F-15 — Backup & Restore (Priority: P0)

**UI**: see [`specs/05-ui-wireframes.md § 6.10`](../05-ui-wireframes.md) (Admin: Backup & Restore).

Admin operates a continuous backup (replicating to USB and a LAN folder), can take an
on-demand encrypted snapshot, and can restore from a snapshot through a typed-confirmation
dialog.

**Why this priority**: with no IT team and the entry-gate PC being a single point of
failure (constraint C5), backup is the only recovery path. Must ship in MVP.

**Independent Test**: with 500 declarations in the system, take a snapshot; corrupt or
empty the database; restore the snapshot and verify exactly 500 declarations are present
again, plus an audit row records the restore.

**Acceptance Scenarios**:

```gherkin
Scenario: Continuous backup health
  Given Litestream is running
  When I open Admin → Health
  Then "Last replica sync" shows a timestamp within the last 2 minutes
  And both targets (USB, LAN) show OK or a clear error

Scenario: Restore round-trip
  Given a snapshot taken at 10:00 with 500 declarations
  And later the DB is corrupted
  When Admin restores that snapshot
  Then the system has exactly 500 declarations again
  And an audit entry records actor, timestamp, snapshot file name

Scenario: Restore confirmation
  When Admin clicks Restore
  Then a typed-confirmation dialog requires typing "RESTORE" exactly
  Else the action is cancelled
```

---

### User Story F-16 — Audit Log Viewer (Priority: P1)

**UI**: see [`specs/05-ui-wireframes.md § 6.12`](../05-ui-wireframes.md) (Admin: Audit Log).

Admin browses the audit log filtered by entity type/id (e.g., a specific Vehicle's
history) to investigate a question or a complaint. The audit log itself cannot be edited
or deleted from the UI or the API.

**Why this priority**: high value for trust and post-incident review, but not required to
operate the gate day-to-day; the audit rows themselves are written by every other feature
regardless of whether the viewer ships.

**Independent Test**: filter by a vehicle's plate and confirm every action taken on that
vehicle is listed in chronological order; attempt to delete an entry and find no surface
that allows it.

**Acceptance Scenarios**:

```gherkin
Scenario: Filter audit by entity
  Given I open Admin → Audit
  When I filter by EntityType="Vehicle" and PlateNumber="12H05769"
  Then I see all changes for that vehicle in chronological order

Scenario: Audit is read-only
  When I try to delete an audit entry
  Then no UI surface exists for deletion
  And the API has no such endpoint
```

---

### User Story F-17 — Smart Suggestions (Sticky Pairs) (Priority: P1)

> **Priority change 2026-05-03 (UI clarification Q2)**: Originally **P2** in the source
> spec ([`specs/03-functional-spec.md`](../03-functional-spec.md)). Promoted to **P1**
> here because [`specs/05-ui-wireframes.md § 6.3`](../05-ui-wireframes.md) makes the
> ghost-text suggestion zone a binding part of the Entry Form layout. Implementation
> scope is restricted to a **simple v1** (see "Phase 1 scope" box below) so the schedule
> impact stays inside Sprint S6 (Hardening). The deferral clause from the source spec
> ("may be deferred to Phase 2 with stakeholder sign-off") no longer applies for Phase 1.

**UI**: see [`specs/05-ui-wireframes.md § 6.3`](../05-ui-wireframes.md) (Entry Form — ghost-text
suggestion zone next to the plate / trailer / company / goods inputs, plus the
"💡 Gợi ý gần đây" footer hint).

When the Operator types a vehicle's plate, the system suggests the trailer / company /
goods most frequently paired with that plate in the last 90 days. The suggestion appears
as ghost text and is accepted with Tab.

**Phase 1 scope ("simple v1")** — binding upper bound on F-17 work for Phase 1:

- Pair statistics are computed from raw vehicle / declaration history of the **last 90
  days** only (rolling window). No separate aggregation table required; a `GROUP BY`
  query on the live tables is sufficient at the Phase-1 data volume.
- Surface **only the single most-frequent pair** per input field (top-1, no ranked list,
  no carousel). Tie-breaking: most recent occurrence wins.
- No machine learning, no embeddings, no tuning UI, no Admin-configurable thresholds.
- Suggestions are accepted with **Tab**; pressing any other key dismisses the ghost text.
- Suggestion latency budget: < 100 ms after the triggering keystroke (well under the
  general SC-004 budget). If the query exceeds the budget twice in a row, the
  suggestion zone hides for a 60 s cooldown (not the rest of the session — a transient
  LAN spike must not cost the operator suggestions until restart, #160) and an INFO log
  is emitted.
- Anything beyond the bullets above is explicitly **out of scope for Phase 1** and
  belongs to a Phase-2 spec.

**Why this priority**: the wireframe is binding (Constitution Principle VIII), so
shipping the Entry Form without functional ghost-text suggestions would be a layout
deviation. The simple-v1 scope keeps the schedule impact contained.

**Independent Test**: prime the database with 28 entries pairing plate "12H05769" with
trailer "12RM05769"; entering plate "12H05769" in a new form shows "12RM05769" as ghost
text and Tab accepts it.

**Acceptance Scenarios**:

```gherkin
Scenario: Suggest trailer for a known plate
  Given plate "12H05769" was paired with trailer "12RM05769" 28 times in the last 90 days
  When I enter plate "12H05769" in a new vehicle form
  Then "12RM05769" appears as a suggestion
  And pressing Tab accepts it
  And it remains editable
```

---

### User Story F-18 — In-App Log Viewer & Health Status (Priority: P1)

**UI**: see [`specs/05-ui-wireframes.md § 6.11`](../05-ui-wireframes.md) (Admin: Health & Logs,
tabbed); status indicator on the App Shell footer per [`§ 5`](../05-ui-wireframes.md).

Admin opens an in-app dashboard that surfaces service uptime, DB and disk usage, last
backup-replica time per target, open-too-long declarations, pending Quartz jobs, and
recent failed logins; and reads recent log lines paginated newest-first.

**Why this priority**: the only diagnostics surface available to a non-IT operator team;
without it, troubleshooting requires a vendor visit. High value but not on the entry/exit
critical path.

**Independent Test**: with C: drive having less than 1 GB free, the health dashboard
shows a Warn for "Disk space"; opening the log viewer after Serilog wrote 100 lines today
shows them paginated and filterable by level.

Health checks (per source spec):

- Service uptime
- DB file size & free disk
- Last Litestream replica time per target
- Open declarations older than 24 h (warn)
- Pending Quartz jobs
- Failed login attempts in last hour

**Acceptance Scenarios**:

```gherkin
Scenario: Disk space warning
  Given C: drive has < 1 GB free
  Then Admin → Health shows a Warn for "Disk space"

Scenario: Read recent log
  Given Serilog wrote 100 lines today
  When I open Admin → Logs
  Then I see them paginated, newest first
  And I can filter by level (Warning/Error)
```

---

### Cross-Feature Acceptance Criteria

These are system-level behaviours that every feature inherits. Preserved verbatim from
[`specs/03-functional-spec.md` § Cross-Feature Acceptance Criteria](../03-functional-spec.md):

```gherkin
Scenario: Every write produces an audit entry
  Given any successful POST/PUT/DELETE on /api
  Then exactly one AuditLog row is inserted with actor, action, entity, before, after

Scenario: Every page is keyboard-navigable
  Given I am on any page
  When I press Tab
  Then focus moves through all interactive elements in a logical order
  And the focus indicator is always visible

Scenario: Every error surfaces a user message + correlation id
  Given an unhandled error occurs
  Then the user sees "Đã xảy ra lỗi. Mã: {correlationId}"
  And the same correlation id appears in the server log
```

---

### UI Acceptance Criteria

These five scenarios are copied verbatim from
[`specs/05-ui-wireframes.md § 9`](../05-ui-wireframes.md) and apply to every page
implemented under `B2.Desktop`. They are tested in `B2.Desktop.Tests` per Constitution
Principle VIII.

```gherkin
Feature: WCAG 2.1 AA compliance

Scenario: Keyboard-only navigation
  Given I am on any page
  When I press Tab repeatedly
  Then focus moves through all interactive elements in the tab order documented in §6
  And the focus ring is visible at all times (Color.BorderFocus, 2px outline)

Scenario: Screen reader announces state changes
  Given I am on the Ledger
  When a vehicle exit is pushed via SignalR
  Then the row's status badge update is announced via aria-live="polite"
  And nothing is announced if the user is in a text input

Scenario: Color is never sole indicator
  Given any status (Open / AllExited / KVGSConfirmed)
  Then the row has BOTH a colored background AND an icon AND a text label

Scenario: Large text mode
  Given I select "Cỡ chữ: Lớn"
  Then all font sizes increase by 4 (e.g., body 16 → 20)
  And no text is clipped, no layout breaks

Scenario: Contrast ratios
  Then every text/background pair in the rendered UI meets ≥ 4.5:1 (normal) or ≥ 3:1 (large/UI)
  Verified by automated audit in CI (axe-style for MAUI)
```

---

### Edge Cases

These are cross-feature edge cases beyond the per-story Gherkin scenarios:

- **Server clock skewed**: client clocks differ from the entry-gate-PC clock; entry/exit
  timestamps must always come from the server (covered explicitly in F-05). Reports and
  daily summaries (F-12, F-14) rely on this.
- **Server (entry-gate PC) reboots mid-shift**: exit and supervisor PCs must show a clear
  "Mất kết nối" state and refuse local writes (constraint C8); reconnection resumes
  without manual action.
- **Network partition between gates**: same as above — system stops, does not buffer.
- **Concurrent edits to the same declaration**: e.g., Operator adds a child vehicle while
  Supervisor confirms KVGS. The KVGS path locks the declaration (F-06, F-09); the
  conflict surfaces as `DECLARATION_LOCKED` to the loser of the race.
- **Daily header / summary clash**: if the 23:00 summary runs while a 00:01 header for the
  next day is being inserted (e.g., clock drift), each job is idempotent and produces one
  row per date.
- **Backup runs during write peak**: continuous backup must not block writes; on-demand
  snapshot creation may briefly pause writes (≤ 30 s during restore only — F-15).
- **Default Admin has not changed password**: the system stays in a degraded state until
  the password is rotated (F-01, F-02).
- **Operator session times out mid-form**: 30-minute idle timeout (F-01) discards
  in-flight form data; the user is required to log in again before resuming.
- **User deactivated mid-session**: the next API call after deactivation returns 401; the
  desktop client returns to the login screen.
- **Unknown plate format**: regex validation rejects at the API; the UI shows a clear
  Vietnamese error and the field is highlighted.
- **Excel export with zero rows in range**: the file is still produced, with daily headers
  for each day in the range and zero declaration rows.
- **Restore while another user is logged in**: writes pause for ≤ 30 s; the other user
  sees a forced reconnect; their unsaved form data is lost.
- **Annual archive job**: runs once per year on a scheduled date (Admin-configurable
  default: 1 February). Reads everything older than 3 years from the live tables, writes
  Excel + PDF archives to the archive folder, verifies each archive can be opened, then
  deletes the archived rows in a single transaction. If verification fails, the live
  rows are NOT deleted and the run is logged as failed for Admin attention.
- **Lookup of archived records**: searches and ledger views never include archived data;
  the in-app search returns only live (≤ 3 years) results. To consult archived data,
  Admin opens the archive folder directly (read-only Excel/PDF).
- **Cancellation race vs exit**: Operator marks a vehicle's exit at the same moment Admin
  cancels its parent declaration. The first write to commit wins; the loser receives a
  conflict response (`DECLARATION_LOCKED` for the exit attempt, or `DECLARATION_NOT_OPEN`
  for the cancellation attempt) and surfaces a clear Vietnamese message.
- **Cancelled declaration appears in exports**: the Excel export shows the row with
  strike-through formatting and the cancellation reason in the Note column so the
  archive remains explainable. Suffix search and the standard reports omit it unless
  the "include cancelled" filter is used.
- **Cross-client exit race**: two operators on different PCs hit the exit endpoint for
  the same vehicle within milliseconds of each other (each with its own
  `Idempotency-Key`). The first commit records the exit; the second client gets a 409
  identifying the winner and sees a Vietnamese banner — for example, "Xe này đã được
  {operator} đánh dấu xuất lúc {hh:mm}" — and the row is refreshed in their ledger
  view via the SignalR broadcast.
- **Lost backup password**: Admin forgets the install-time password. Recovery flow:
  use the offline-stored recovery code from install to decrypt an existing snapshot
  (any target — USB or LAN). Once recovered, Admin sets a fresh install password via
  Admin → Backup; the recovery code itself remains valid until a new installer is run.
  If both the password AND the recovery code are lost, the backups are unrecoverable
  by design — Admin must rely on whatever Litestream-replicated WAL state happens to
  be on the target devices, which itself is also encrypted under the same scheme.

---

## Requirements *(mandatory)*

These cross-cutting Functional Requirements complement the per-feature Gherkin acceptance
scenarios above; they are the requirements that no single user story owns but every story
depends on.

### Functional Requirements

- **FR-001**: System MUST require authentication on every API endpoint except the login
  endpoint and the public health check (covers F-01, F-18).
- **FR-002**: System MUST enforce role-based authorization server-side for every write
  command; UI hiding is not sufficient (Constitution Principle VII; F-02, F-08, F-09).
- **FR-003**: System MUST write exactly one AuditLog row for every successful write
  (POST/PUT/DELETE) on the API surface, capturing actor, action, entity, before, and
  after states (Cross-Feature; F-16).
- **FR-004**: System MUST stamp every entry/exit/KVGS event with the server clock, never
  the client clock (F-05, F-08, F-09).
- **FR-005**: System MUST validate every declaration number to be exactly 12 digits and
  unique across the system (F-05).
- **FR-006**: System MUST auto-uppercase plate numbers and trailer/container numbers on
  input and persist the uppercased form (business rule cột E, F; F-05).
- **FR-007**: System MUST reject Processor and Yard values that are not present and
  active in master data; free-text values are not accepted (F-03, F-04, F-05).
- **FR-008**: System MUST broadcast declaration state changes (created, all-exited,
  KVGS-confirmed) to all connected clients within 1 second (F-08, F-09, F-10).
- **FR-009**: System MUST present every user-facing string in Vietnamese; no hard-coded
  English in user-visible surfaces (Constitution Principle VI).
- **FR-010**: System MUST be operable end-to-end via keyboard alone, with a visible focus
  indicator on every interactive element (Cross-Feature; Constitution Principle VI).
- **FR-011** *(scope clarified 2026-05-03 — UI Q4)*: When the desktop client cannot
  reach the server at all (the **🔴 "Mất kết nối"** state in `specs/05-ui-wireframes.md
  § 5`), the client MUST display "Mất kết nối" with a retry affordance and MUST refuse
  local writes (constraint C8; F-10). The write-refusal rule applies **only** to the
  🔴 state. Behaviour for the 🟢 Online and 🟡 polling-fallback states is governed by
  FR-028.
- **FR-012**: System MUST replicate the live database to at least two backup targets
  (USB and a LAN folder) on a continuous schedule and surface the most recent replication
  timestamp per target on the Admin Health screen (F-15, F-18).
- **FR-013**: System MUST require typed confirmation ("RESTORE") before performing a
  database restore (F-15).
- **FR-014**: System MUST forbid deletion of users and audit log rows; both are
  deactivated or retained respectively (F-02, F-16).
- **FR-015**: System MUST preserve the legacy Sổ B2 column order, daily-header pattern,
  and conditional colouring rules in every Excel export (F-13; business rule cột A–O).
- **FR-016**: System MUST present a correlation id with every unhandled-error message
  shown to the user, and MUST log the same correlation id on the server (Cross-Feature).
- **FR-017**: System MUST force the default Admin to change password on first login and
  MUST keep the system in a degraded state until that change is made (F-01). The initial
  Admin password MUST be generated per install by the installer, shown once on screen and
  never shared between installs; a password the Admin has chosen MUST never be replaced
  by an installer run (ADR-0009).
- **FR-018**: System MUST lock an account for 15 minutes after 5 failed logins within 10
  minutes, and MUST end any session after 30 minutes of inactivity (F-01).
- **FR-019**: System MUST be idempotent for retried write commands when the same
  Idempotency-Key is presented (F-08; supports Polly retries on transient LAN drops).
- **FR-020**: System MUST auto-promote a declaration to AllExited only when every child
  vehicle has been exited, and MUST require AllExited before KVGS confirmation is allowed
  (F-08, F-09).
- **FR-021**: System MUST keep declarations, vehicles, daily headers, and audit-log rows
  online in the live database for the most recent 3 years (rolling window). Data older
  than 3 years MUST be archived annually to read-only Excel and PDF files in a sibling
  folder of the live database, then removed from the live tables. Archived data MUST NOT
  be editable through the app and MUST NOT count toward the live-DB performance budgets
  in Success Criteria. Master data (Users, Processors, Yards) is exempt from the 3-year
  rolling window — it is retained as long as it remains in use.
- **FR-022**: System MUST allow an Admin (only) to cancel a declaration whose Status is
  `Open`, with a mandatory free-text reason. Cancellation MUST flip Status to
  `Cancelled`, write an AuditLog row capturing actor / timestamp / reason, and prevent
  any further writes against that declaration or its child vehicles (no exit-marking,
  no add-vehicle, no KVGS confirmation). The system MUST forbid cancellation when
  Status is `AllExited`, `KVGSConfirmed`, or already `Cancelled`. Operators and
  Supervisors MUST receive a 403 on the cancellation endpoint.
- **FR-023**: Cancelled declarations and their vehicles MUST remain visible in the
  ledger (F-10) and Excel export (F-13) rendered with strike-through formatting and a
  distinct "Đã hủy" badge, but MUST be excluded by default from suffix search (F-07)
  and from per-Processor / per-Yard / per-Hour / per-Officer reports (F-14). Reports
  MUST expose an explicit "include cancelled" filter for audit use.
- **FR-024**: When two clients submit an exit on the same vehicle near-simultaneously
  with different `Idempotency-Key` values (cross-client race, distinct from the same-key
  retry case in FR-019), the system MUST serialize the writes such that the first to
  commit wins and the second receives `VEHICLE_ALREADY_EXITED` (409). The 409 response
  body MUST include the username of the operator who recorded the exit and the
  server-clock ExitTime so the losing client can show a clear Vietnamese message
  identifying who already marked the vehicle out.
- **FR-025**: System MUST expose `DailyHeader.OnDutyOfficerName` as a free-text field
  inline-editable from today's row in the ledger by any authenticated user (Admin,
  Operator, or Supervisor). The field is initially empty when the row is auto-created
  at 00:01 (F-11). Every set or change MUST write an AuditLog row capturing actor,
  server timestamp, before-value, and after-value. The system MUST NOT introduce a
  separate shift-schedule UI or entity in Phase 1.
- **FR-026**: At install time the system MUST generate a single-use printable recovery
  code, present it on screen, require Admin to acknowledge they have recorded it
  offline before the installer proceeds, and use a key-wrap construction such that
  every backup snapshot can be decrypted with either the install-time password or the
  recovery code. The recovery code MUST be displayed exactly once during install and
  never persisted in the live database. The Admin Health screen (F-18) MUST show a
  "recovery code acknowledged at install" status item so missing acknowledgement is
  visible after the fact.
- **FR-027** *(added 2026-05-03 from UI clarification Q1)*: Server-side role
  authorization MUST mirror the sidebar visibility matrix in
  [`specs/05-ui-wireframes.md § 4`](../05-ui-wireframes.md). The single source of truth
  is the matrix below; UI sidebar hiding and API authorization both derive from it.
  Admin is allowed everywhere; the existing F-09 / F-02 / F-22 rules remain in force
  and are restated here for completeness.

  | Endpoint group (write or read) | Admin | Operator | Supervisor |
  | --- | :-: | :-: | :-: |
  | Auth: `POST /api/auth/login`, `POST /api/auth/change-password` | ✅ | ✅ | ✅ |
  | Entry / Exit: `POST /api/declarations`, `POST /api/declarations/{id}/vehicles`, `POST /api/vehicles/{id}/exit` | ✅ | ✅ | ❌ 403 |
  | KVGS: `POST /api/declarations/{id}/kvgs-confirm` | ✅ | ✅ | ✅ |
  | Reports: `GET /api/reports/*` | ✅ | ❌ 403 | ✅ |
  | Export: `POST /api/exports/so-b2`, `POST /api/exports/reports/*` | ✅ | ❌ 403 | ✅ |
  | Ghi chú (cột O): `PATCH /api/vehicles/{id}/note` | ✅ | ✅ | ✅ |
  | Ledger reads: `GET /api/declarations`, `GET /api/vehicles/*` | ✅ | ✅ | ✅ |
  | Vehicle suffix search: `GET /api/vehicles/search` | ✅ | ✅ | ✅ |
  | DailyHeader read + `PATCH /api/daily-headers/{date}/officer` | ✅ | ✅ | ✅ |
  | Master data writes: `POST/PUT/DELETE /api/processors`, `/api/yards` | ✅ | ❌ 403 | ❌ 403 |
  | User management: `POST/PUT /api/users`, password reset, deactivate | ✅ | ❌ 403 | ❌ 403 |
  | Admin override: `POST /api/vehicles/{id}/un-exit`, `POST /api/declarations/{id}/cancel` | ✅ | ❌ 403 | ❌ 403 |
  | Backup & Restore: `POST /api/admin/backups`, `POST /api/admin/restore` | ✅ | ❌ 403 | ❌ 403 |
  | Audit log read: `GET /api/admin/audit` | ✅ | ❌ 403 | ❌ 403 |
  | Health: `GET /api/admin/health`, `GET /api/admin/logs` | ✅ | (anon OK for `/health` only — `FR-001`) | (anon OK for `/health` only — `FR-001`) |

  Tests: every "❌ 403" cell MUST be covered by a `Should_Reject_<role>_on_<endpoint>`
  scenario tagged with the relevant Feature ID; the existing F-09 Operator-cannot-confirm
  scenario is one example of the pattern.

- **FR-028** *(added 2026-05-03 from UI clarification Q4)*: The desktop client MUST
  surface a **tri-state connectivity badge** matching `specs/05-ui-wireframes.md § 5`:

  | State | Trigger | Banner / badge | Writes (Entry / Exit / KVGS / etc.) | Live updates |
  | --- | --- | --- | --- | --- |
  | 🟢 Online | SignalR connection established | Green badge "Online" only; no banner | **Permitted** | SignalR push, ≤ 1 s |
  | 🟡 Đang thử lại | HTTP reachable, SignalR dropped → fell back to polling | Yellow badge + persistent yellow banner with last-success timestamp | **Permitted** (server is the source of truth; polling closes the convergence gap) | Polling, target ≤ 3 s, hard cap 5 s |
  | 🔴 Mất kết nối | Server unreachable on both SignalR and HTTP for > 5 s | Red badge + persistent red banner "Mất kết nối tới máy chủ" + retry control | **Refused** per FR-011 (constraint C8) | Stalled until reconnection |

  The state machine MUST transition 🟢 → 🟡 only after a SignalR reconnection attempt
  has failed twice consecutively, and 🟡 → 🔴 only after a polling round-trip has
  failed for > 5 s; transitions back to 🟢 are immediate on the first successful
  SignalR handshake. Every state change MUST emit a Serilog `Information` event. No
  toast or modal is shown on state change — the badge + banner are the only signals
  (avoids interrupting the operator mid-form).

### Key Entities

- **User** — a person who logs into the desktop client. Has a username, a hashed password,
  a role, an Active flag, lock-out state, and a "must change password on next login" flag.
- **Role** — one of `Admin`, `Operator`, `Supervisor`. Determines which API and UI
  surfaces a user can reach.
- **Processor (Người làm thủ tục)** — a person from the customs business who handles
  declarations. Master data; has a name and an Active flag. Referenced by Declaration.
- **Yard (Tên bãi)** — a yard inside the customs gate. Master data; has a unique code, a
  display name, and an Active flag. Referenced by Declaration.
- **Declaration (Tờ khai)** — a customs declaration covering one or more vehicles. Has a
  12-digit DeclarationNumber (unique), Processor, Company, GoodsName, Yard, Status (`Open`
  / `AllExited` / `KVGSConfirmed` / `Cancelled`), entered-by user, KVGS-confirmed-by user
  (nullable), KVGS-confirmed-at (nullable), cancelled-by user (nullable), cancelled-at
  (nullable), cancellation-reason (nullable, required when Status = `Cancelled`), and
  timestamps. `Cancelled` is reachable only from `Open` (never from `AllExited` or
  `KVGSConfirmed`); once `KVGSConfirmed` the declaration is locked.
- **Vehicle (Xe)** — one truck under a Declaration. Has PlateNumber (uppercased), an
  optional Trailer/Container number, EntryTime (server clock), ExitMarked flag, ExitTime
  (nullable, server clock), Note, the Operator who entered it, and the Operator who
  confirmed its exit (nullable).
- **DailyHeader** — one row per business day, holding date, on-duty officer name, and
  TotalVehicles (populated at 23:00 by F-12). Auto-created at 00:01 (F-11) with
  `OnDutyOfficerName` initially empty; the name is a free-text field, inline-editable
  on the ledger by any authenticated user, and every set / change is captured in the
  AuditLog. There is no shift-schedule entity in Phase 1.
- **AuditLog** — one row per successful write. Stores actor user id, action verb, entity
  type and id, before and after JSON snapshots, server timestamp, and an optional
  free-text reason (used by Admin overrides such as un-exit).
- **BackupSnapshot** — an on-demand encrypted archive of the database. Has a file name
  pattern `b2-YYYYMMDD-HHmm.bak.zip`, an actor, a timestamp, and a list of targets to
  which it has been written (USB, LAN, …). Encryption is AES-256 with a single random
  data key. The data key is wrapped twice — once with the install-time password, once
  with the install-time printable recovery code — so the snapshot is decryptable with
  either secret.
- **Archive** — read-only Excel + PDF rendition of declarations / vehicles / daily
  headers / audit-log rows older than 3 years, produced by the annual archive job.
  Lives in a sibling folder of the live database. Not loaded into the app at runtime;
  consulted manually by Admin when historical data is needed.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001 (eliminates root cause)**: zero shared-Excel-file concurrency conflicts after
  go-live (currently the top operational pain). Verified by absence of "lost row" or
  "merge conflict" reports in the first 90 days.
- **SC-002 (operability without IT)**: a non-IT operator can complete a full happy-path
  cycle (create declaration with 2 vehicles → mark both exits → KVGS confirm → export
  Excel) within 15 minutes of being shown the system for the first time. Verified by a
  3-user usability test (per `01-overview.md § 7`).
- **SC-003 (search responsiveness)**: 95 % of plate-suffix searches over a 50,000-vehicle
  history return in under 200 ms on the spec hardware.
- **SC-004 (entry/exit responsiveness)**: 95 % of single-user write actions (entry,
  exit, KVGS) complete in under 200 ms on the spec hardware.
- **SC-005 (cold start)**: the desktop client starts and is ready to log in within 5
  seconds on the spec hardware (i5-3570 / 4 GB).
- **SC-006 (concurrent users)**: the system sustains 5 concurrent users at the gate plus
  bursts of 10 without degraded responsiveness against SC-003 and SC-004.
- **SC-007 (live updates)**: when one client changes a declaration's state, all other
  connected clients reflect the change within 1 second without a manual refresh.
- **SC-008 (uptime)**: the server is available for at least 99 % of the gate's operating
  hours, measured monthly.
- **SC-009 (recovery)**: after a server crash the service is back online within 30
  seconds; restoring the latest backup completes in under 5 minutes.
- **SC-010 (data integrity through backup)**: a restored database equals the snapshotted
  database, with zero declaration count drift, verified during the release smoke drill.
- **SC-011 (Excel fidelity)**: an exported file opens without errors in Office 2010 (or
  LibreOffice as proxy) and matches the legacy Sổ B2 layout cell-for-cell on column
  order, daily headers, and conditional colours.
- **SC-012 (Excel performance)**: a 30-day export with about 3,500 declarations completes
  in under 10 seconds on the spec hardware.
- **SC-013 (auditability)**: every write command in the API is observable as exactly one
  AuditLog row, verified by a system-wide test sweep before each release.
- **SC-014 (accessibility)**: each release passes a WCAG 2.1 AA audit checklist with all
  text contrast at or above 4.5 : 1, no keyboard-unreachable controls, and base font size
  at or above 16.
- **SC-015 (Vietnamese-first)**: zero hard-coded user-facing strings outside the
  Vietnamese resource file, verified by a release-time scan.
- **SC-016 (retention window)**: live database holds the most recent 3 years of
  declarations, vehicles, daily headers, and audit-log rows. After the annual archive
  job runs, every record older than 3 years is present in the archive folder
  (Excel + PDF, read-only) and absent from the live tables. Verified by a yearly drill.

---

## Non-Functional Requirements

These are the binding non-functional constraints that the Success Criteria above
operationalise. They live as a separate section so reviewers and downstream artifacts
(plan, tasks, checklists) can reference them by name.

- **NFR-001 (Design tokens are binding)**: the design tokens — colors, typography, and
  spacing — defined in [`specs/05-ui-wireframes.md § 2–3`](../05-ui-wireframes.md) are
  binding for the Desktop layer. Every value MUST be declared once as a `StaticResource`
  (in `Resources/Styles/Colors.xaml` and `Resources/Styles/Typography.xaml`) and
  referenced from there. Hard-coded hex values, font sizes, or spacing constants outside
  those two resource dictionaries are forbidden. Enforced by Constitution Principle VIII.
- **NFR-002 (Wireframe page fidelity)**: every Desktop page MUST follow the layout, tab
  order, and keyboard shortcuts of its corresponding wireframe in
  [`specs/05-ui-wireframes.md § 6`](../05-ui-wireframes.md). Deviations require an ADR.
- **NFR-003 (Four list-view states)**: every list / table view MUST implement
  Loading (skeleton) / Empty (icon + action) / Error (red banner with retry +
  correlation id) / Offline (yellow banner with last-success timestamp) per
  [`§ 8`](../05-ui-wireframes.md).
- **NFR-004 (Status indicators are not color-only)**: every status indicator MUST
  combine icon + color + text label simultaneously per
  [`§ 1`](../05-ui-wireframes.md) D5 / WCAG 1.4.1.
- **NFR-005 (Reusable Toast & Modal services)**: Toast and Modal are shared services
  in `B2.Desktop/Services/`. Per-page reimplementations are forbidden.
- **NFR-006 (Vietnamese copy is final)**: every Vietnamese string in
  [`specs/05-ui-wireframes.md`](../05-ui-wireframes.md) is the final user-facing copy
  and MUST be transcribed verbatim into `Resources/Strings/AppResources.vi.resx`.

The other system-wide non-functional targets (cold-start, latency, concurrency, uptime,
recovery, accessibility, retention) remain expressed as measurable Success Criteria
SC-001 through SC-016 above and as the targets in
[`specs/01-overview.md § 7`](../01-overview.md).

---

## Assumptions

- The eighteen features listed in [`specs/03-functional-spec.md`](../03-functional-spec.md) are
  the complete and exclusive scope of Phase 1. Items in [`specs/01-overview.md § 8`](../01-overview.md)
  (legacy Excel migration, internal-system integration, LPR auto-fill, mobile apps,
  multi-site deployment) are explicitly out of scope here.
- Constraints C1–C8 from [`specs/01-overview.md § 2`](../01-overview.md) are immutable for
  Phase 1 and inform every requirement and success criterion above (LAN-only operation,
  low-spec hardware, no IT team, native desktop UX, single-host server, 24/7 service,
  five concurrent users, no offline-first on exit/supervisor PCs).
- The technology stack in [`specs/01-overview.md § 6`](../01-overview.md) is locked; this
  spec does not introduce additional libraries or runtimes. Per Constitution Principle II,
  any deviation needs an ADR.
- Some Gherkin acceptance scenarios reference implementation specifics (HTTP status codes,
  SignalR event names, Idempotency-Key behaviour, Polly retries, BCrypt, JWT). These are
  preserved verbatim from the source spec at stakeholder direction. The spec template's
  guidance to keep WHAT separate from HOW is satisfied at the user-story / requirement /
  success-criteria level above; the embedded Gherkin remains the authoritative AC and
  retains its original wording.
- Vietnamese is the primary user-facing language. English appears only in developer-only
  surfaces (server logs, source code, this spec).
- The legacy "Mẫu sổ B2.xlsx" template, "Mẫu sổ B2.pdf", and the column-by-column rules in
  [`docs/Business rules - Hướng dẫn điền sổ B2.md`](../../docs/Business%20rules%20-%20Hướng%20dẫn%20điền%20sổ%20B2.md)
  are authoritative for column order, formatting, and per-column behaviour (cột A–P).
- Operators have intermediate computer literacy; Supervisors and Admins are similar but
  with broader system responsibility. The system is keyboard-first and avoids web-app
  idioms.
- A "spec VM" (Win 10 64-bit, 2 vCPU, 4 GB, no pre-installed .NET runtime) is the
  reference environment for performance and operability success criteria
  ([`specs/04-implementation-plan.md § 8`](../04-implementation-plan.md)).
