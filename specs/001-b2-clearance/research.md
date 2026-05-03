# Phase 0 — Research

**Feature**: `001-b2-clearance` · **Date**: 2026-05-03 · **Plan**: [`plan.md`](./plan.md)

This document records the **decisions** that close every `NEEDS CLARIFICATION` slot in
the plan template. Decisions are restated from the binding source documents
(`specs/01-overview.md`, `specs/02-architecture.md`, `specs/04-implementation-plan.md`,
`specs/05-ui-wireframes.md`, `.specify/memory/constitution.md`) so downstream commands
(`/speckit.tasks`, `/speckit.implement`) have a single research surface.

---

## 1. State Machines

### 1.1 Declaration

```
                +------------------ admin un-exit (audited)
                |
   [start] --> Open ----- last child Vehicle.ExitMarked = true ----+
                ^                                                  |
                |                                                  v
                |                                              AllExited
                |                                                  |
                |                                                  | Supervisor or Admin
                |                                                  | confirms KVGS
                |                                                  v
                |                                            KVGSConfirmed (terminal)
                |
   admin cancel | (FR-022; only from Open; mandatory reason)
                v
            Cancelled (terminal — FR-023 visible-but-locked)
```

| Transition | Trigger | Authorization | Audit | Source |
| --- | --- | --- | --- | --- |
| `[start] → Open` | `POST /api/declarations` succeeds | Operator+ (FR-027) | Yes (Create) | F-05, INV-1..INV-3 |
| `Open → AllExited` | `Vehicle.ExitMarked` flips false→true on the last child (atomic recompute in same TX) | Implicit — happens inside the exit command (Operator+ on F-08) | Yes (Update) — single AuditLog row for the exit, declaration recompute is implicit | F-08, INV-5 |
| `AllExited → Open` | Admin un-exit override (`POST /api/vehicles/{id}/un-exit`) flips a child back to `Inside`; if the recompute shows a non-exited child, declaration reverts | Admin only | Yes (Update) with mandatory reason | F-08 ("Admin un-exit override"), INV-4 (ExitTime cleared) |
| `AllExited → KVGSConfirmed` | `POST /api/declarations/{id}/kvgs-confirm` succeeds | Supervisor or Admin (FR-027); INV-6 | Yes (Update) | F-09, INV-6 |
| `Open → Cancelled` | `POST /api/declarations/{id}/cancel` with mandatory reason | Admin only (FR-027); allowed only from `Open` (FR-022) | Yes (Update) capturing actor / timestamp / reason | FR-022 |
| `KVGSConfirmed → *` | none (terminal) | — | — | F-09 |
| `Cancelled → *` | none (terminal; visible in ledger / export with strike-through) | — | — | FR-023 |

### 1.2 Vehicle

```
   [start] --> Inside ---- ExitMarked = true, ExitTime = server clock ---> Outside
                ^                                                            |
                |                                                            |
                +-------- admin override (POST /vehicles/{id}/un-exit) <-----+
                          (audited reason; ExitTime cleared)
```

| Transition | Trigger | Authorization | Audit | Source |
| --- | --- | --- | --- | --- |
| `[start] → Inside` | Created with declaration (POST /api/declarations) or added via POST /api/declarations/{id}/vehicles | Operator+ | Yes (Create) | F-05, F-06, INV-2 |
| `Inside → Outside` | `POST /api/vehicles/{id}/exit` succeeds; `ExitTime = server clock`; declaration recompute fires | Operator+; idempotent on `Idempotency-Key` (FR-019); cross-client race serialized first-wins (FR-024) | Yes (Update) | F-08, INV-4 |
| `Outside → Inside` | `POST /api/vehicles/{id}/un-exit` (admin only); `ExitTime` cleared; declaration recompute reverts to `Open` if any child becomes `Inside` again | Admin only | Yes (Update) with mandatory reason | F-08 admin override |

**Decision**: encode both state machines in `B2.Domain` as private aggregate-root
methods (e.g., `Declaration.MarkChildExited(...)`, `Declaration.ConfirmKvgs(...)`,
`Vehicle.MarkExit(...)`). The application layer calls these methods through commands;
EF Core persists the resulting state. State transitions are unit-tested in
`B2.Domain.Tests` against the eight invariants `INV-1..INV-8`.

**Rationale**: keeps state-machine logic in the layer with zero outward dependencies
(Constitution V); makes invariants testable without spinning up a database.

**Alternatives considered**:
- *Database-enforced status column with CHECK constraint.* — Rejected: pushes
  business logic to Infrastructure; harder to unit-test; SQLite's CHECK doesn't
  cover cross-row recompute (e.g., "last child exited").
- *Stateless status calculation on every read.* — Rejected: still need to persist
  KVGS confirmation timestamps and audit; storing the status column avoids re-deriving
  it on every ledger query (covered by `IX_Declaration_Status` per
  `04-implementation-plan.md § 5.3`).

---

## 2. Cross-Cutting Decisions

Each decision restates a row from `02-architecture.md § 8` with explicit rationale and
the alternatives that were rejected.

### 2.1 Authentication

- **Decision**: JWT **RS256** with 8 h access tokens + 7 d refresh tokens. Signing
  key generated at install, persisted to `C:\ProgramData\B2\keys\` and sealed with
  Windows DPAPI (per-machine scope). Logout revokes the refresh token.
- **Rationale**: RS256 lets us rotate the public-key material on clients without
  resharing a secret; DPAPI sealing means the key never appears in plaintext on disk
  (Constitution VII).
- **Alternatives**: HS256 (rejected — symmetric secret leaks invalidate every client);
  Windows Auth / SSPI (rejected — requires AD which the customs unit doesn't run).

### 2.2 Authorization

- **Decision**: ASP.NET Core **policy-based** authorization. Policies registered in
  `B2.Server.Api/Auth/AuthorizationPolicies.cs`: `AdminOnly`, `OperatorOrAbove`,
  `SupervisorOrAbove`. Every endpoint declares its policy. The full role × endpoint
  matrix is in `spec.md` FR-027.
- **Rationale**: server-side enforcement satisfies Constitution VII ("UI hiding is
  not sufficient") and FR-002. Policy names map 1:1 to the wireframe sidebar visibility
  matrix (FR-027 Q1) so reviewers can audit by reading one table.
- **Alternatives**: claim-based per-endpoint role checks (rejected — verbose, drifts);
  ABAC (rejected — overkill for three roles).

### 2.3 Validation

- **Decision**: **FluentValidation** validators per command in
  `B2.Application/Validation/`. Common rules (12-digit declaration number, plate
  pattern, max-length strings) live in `Common/`.
- **Rationale**: fail-fast at the application boundary; rule definitions are unit-
  testable without a web host; complementary to the in-domain invariants (which guard
  against logic bugs even when validators are bypassed).
- **Alternatives**: Data Annotations (rejected — limited expressiveness for cross-
  field rules).

### 2.4 Audit Log

- **Decision**: ASP.NET Core middleware (`AuditMiddleware`) wraps every successful
  write (POST/PUT/DELETE/PATCH outside `/api/admin/health`) and appends one row to
  `AuditLog` capturing actor (JWT subject), action verb, entity type + id, before /
  after JSON snapshots, server timestamp, and an optional reason (cancellation,
  un-exit, restore). The table is **append-only**: no UPDATE / DELETE endpoint, no
  EF Core update tracking.
- **Rationale**: one chokepoint for FR-003 ("exactly one AuditLog row per successful
  write"); single integration test asserts the invariant for the whole API surface.
- **Alternatives**: Domain-event-driven audit (rejected — duplicates middleware
  concern; risk of missing audits when a command does multiple writes); EF Core
  ChangeTracker hooks (rejected — couples audit to ORM internals).

### 2.5 Logging

- **Decision**: **Serilog** with two sinks — rolling file at
  `C:\ProgramData\B2\logs\b2-yyyymmdd.log` (30-day retention) and an in-memory
  bounded queue (1 000 entries) read by the in-app log viewer (`F-18`). Levels:
  `Information` for command success, `Warning` for retried errors, `Error` for
  unhandled exceptions. Every log line carries the request `correlationId` (W3C
  Trace-Context header).
- **Rationale**: file sink for audit/forensics; in-memory sink lets non-IT operators
  see recent activity without leaving the app (Constitution I, C3).
- **Alternatives**: NLog (rejected — outside the locked stack); remote sinks
  (rejected — violates C1 / no internet).

### 2.6 Error Handling

- **Decision**: `ProblemDetails` (RFC 7807) for every error response. Domain
  exceptions map to **400 Bad Request** (validation), **404 Not Found** (missing
  entity), **409 Conflict** (state-machine violation, idempotency clash, optimistic
  concurrency). Unhandled exceptions return **500** with a fresh `correlationId` shown
  to the user as "Đã xảy ra lỗi. Mã: {correlationId}" (FR-016).
- **Rationale**: standard format → simpler client (Refit can deserialize the same
  schema for every endpoint); correlation id is the trail for in-app log support.
- **Alternatives**: ad-hoc JSON error envelopes (rejected — every endpoint reinvents
  the shape).

### 2.7 Idempotency

- **Decision**: middleware reads the `Idempotency-Key` request header on
  `POST /api/declarations` and `POST /api/vehicles/{id}/exit` (other endpoints are
  naturally idempotent or guarded by optimistic concurrency). Server stores
  `(key, requestHash, responseStatus, responseBodyHash, createdAt)` for 24 h; replay
  with the same key returns the cached response without re-running the command. A
  `IdempotencyKeyEvictionJob` (Quartz, every 30 min) deletes keys older than 24 h.
- **Rationale**: lets the Desktop's Polly retries (3 attempts, exponential) safely
  reissue commands when a transient LAN drop loses the response (FR-019). A
  *different-key* race between two clients on the same vehicle is **not** an
  idempotency case — see § 2.8 below.
- **Alternatives**: client-side dedupe via local cache (rejected — fails when a
  different desktop process is the retrier).

### 2.8 Concurrency (Optimistic + Cross-Client Race)

- **Decision (optimistic)**: SQLite stores a `RowVersion BLOB` on `Declaration` and
  `Vehicle`, populated by a `BEFORE UPDATE` trigger (`randomblob(8)`). EF Core
  configures it as a concurrency token. Conflict → 409 with `ProblemDetails.type =
  "https://b2/errors/concurrency"`; Refit client refetches, the user retries.
- **Decision (cross-client race on exit, FR-024)**: when two desktops submit
  `POST /api/vehicles/{id}/exit` with **different** `Idempotency-Key` values within
  milliseconds of each other, the server serializes via the row's update lock; the
  loser receives **409 `VEHICLE_ALREADY_EXITED`** with a body containing the winner's
  username and the recorded `ExitTime` so the loser shows
  *"Xe này đã được {operator} đánh dấu xuất lúc {hh:mm}"*.
- **Rationale**: SQLite WAL handles the serialization; the application layer simply
  re-reads after the conflict and emits the correct ProblemDetails.
- **Alternatives**: pessimistic row locks (rejected — no SQLite primitive without
  serializing the whole DB); SignalR-based pre-claim (rejected — complex, race-prone).

### 2.9 Time

- **Decision**: every server-side timestamp stored as **UTC** (`DateTimeOffset`
  serialized to ISO-8601 in DTOs). Desktop renders in `Asia/Ho_Chi_Minh` time using
  `TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time")`. Display format is
  `dd/MM/yyyy HH:mm` (legacy Sổ B2 convention). NEVER trust the client clock —
  EntryTime / ExitTime / KvgsConfirmedAt are always set server-side (F-05 AC "Server
  timestamp").
- **Rationale**: prevents client-clock drift from corrupting reports / audit
  (FR-004); UTC storage avoids DST math (Vietnam is UTC+7 fixed, but UTC remains the
  long-term-stable choice).
- **Alternatives**: store local-time TEXT (rejected — drift, comparisons fail).

### 2.10 i18n

- **Decision**: Vietnamese only for Phase 1. Strings live in
  `B2.Desktop/Resources/Strings/AppResources.vi.resx`. Server returns Vietnamese
  user-facing copy only when the value originated as user input (e.g., a goods
  description); error codes are stable English identifiers (`MASTER_PROCESSOR_NOT_FOUND`,
  `DECLARATION_NUMBER_DUPLICATE`, `VEHICLE_ALREADY_EXITED`, `DECLARATION_LOCKED`,
  `DECLARATION_NOT_READY_FOR_KVGS`, `DECLARATION_NOT_OPEN`, `IDEMPOTENCY_KEY_REPLAY`)
  that the desktop maps to `AppResources.vi.resx` keys. Logs and developer-only
  messages stay in English.
- **Rationale**: separating identifiers from copy lets us evolve copy without breaking
  tests; satisfies Constitution VI without bloating server code with Vietnamese
  literals.
- **Alternatives**: server-resolved Vietnamese error messages (rejected — couples
  server to copy decisions; harder to test).

### 2.11 Accessibility (WCAG 2.1 AA)

- **Decision**: design tokens declared once in
  `B2.Desktop/Resources/Styles/Colors.xaml` and `Typography.xaml` per
  `05-ui-wireframes.md § 2–3`; every contrast pair carries a verification comment
  (e.g., `<!-- contrast 7.2:1 -->`). `TabIndex` enforces tab order per `§ 6` per page.
  Keyboard accelerators registered in `Platforms/Windows/`. `aria-live` regions used
  for screen-reader announcements per `§ 9`. Five accessibility scenarios from `§ 9`
  become Gherkin tests in `B2.Desktop.Tests`.
- **Rationale**: Constitution VI + VIII; one source of truth for tokens means an
  audit only re-verifies one file.
- **Alternatives**: per-page hex constants (rejected — Constitution VIII forbids).

---

## 3. Tri-State Connectivity Machine (FR-028)

Decision matrix derived from `spec.md` FR-028 + `05-ui-wireframes.md § 5`:

| From → To | Trigger condition | Action / Banner | Writes? |
| --- | --- | --- | --- |
| 🟢 Online → 🟡 Đang thử lại | Two consecutive SignalR reconnect failures (Polly retry, exponential) | Switch to HTTP polling at `GET /api/declarations?since={lastSyncedAt}` every 3 s; show yellow banner + last-success timestamp. | ✅ Yes (HTTP path is up). |
| 🟡 Đang thử lại → 🔴 Mất kết nối | Polling round-trip fails for > 5 s (timeout or HTTP 5xx) | Show red banner + retry button; refuse local writes per FR-011. | ❌ No (refuse + show 503-style local error). |
| 🔴 Mất kết nối → 🟢 Online | First successful SignalR handshake after the outage | Hide all banners; resume push delta. | ✅ Yes. |
| 🟡 Đang thử lại → 🟢 Online | First successful SignalR handshake (without going through 🔴) | Hide yellow banner; resume push delta; **drain pending writes is N/A** (writes were never queued — they went through during 🟡). | ✅ Yes (was already yes). |
| 🟢 Online → 🔴 Mất kết nối | Direct: server unreachable on both transports for > 5 s (rare; SignalR usually degrades to 🟡 first) | Same red banner + write refusal as above. | ❌ No. |

**Decision**: implement as `ConnectivityState` service in `B2.Desktop/Services/`,
exposed as `IObservable<ConnectivityStatus>`; `AppShellViewModel` and every list
ViewModel subscribe. Polling cadence is hard-coded to 3 s (target) / 5 s (timeout)
in v1; configurable via `appsettings.json` if operator pain emerges.

**Rationale**: matches the spec's tri-state contract; never queues writes locally
(C8: "no offline-first on exit/supervisor PCs"); banners + badge are the only signals
(no toast spam on every state change).

**Alternatives**: a single boolean `IsOnline` (rejected — collapses 🟡 and 🟢, losing
the operator-visible degradation cue and the policy that 🟡 still allows writes).

---

## 4. Cancellation Flow (FR-022 / FR-023)

- **Decision**: new endpoint `POST /api/declarations/{id}/cancel` (Admin only, requires
  body `{ "reason": "..." }`). Domain method `Declaration.Cancel(reason, actorId)`
  guards "only from `Open`" and writes the reason into the declaration row (new
  columns `CancelledByUserId`, `CancelledAt`, `CancellationReason`). Soft delete: the
  row stays in the table, status flips to `Cancelled`, and is rendered with strike-
  through in ledger (F-10) and Excel export (F-13). Suffix search (F-07) and the
  per-Processor / per-Yard / per-Hour / per-Officer reports (F-14) **omit** cancelled
  rows by default; an explicit `includeCancelled=true` query parameter opts in.
- **Authorization**: Admin only (FR-027). Operators / Supervisors get 403.
- **Audit**: one row capturing actor / timestamp / reason / before / after.
- **Endpoint status**: not in the locked API surface (`02-architecture.md § 6`); see
  ADR-0006 below.

**Alternatives**: hard delete (rejected — destroys history; FR-023 wants visible-but-
locked rows); admin "edit declaration" surface (rejected — too broad; cancellation is
the only mutation we want to allow on `Open`).

---

## 5. Recovery Code (FR-026)

- **Decision**: at install, generate a single 24-character base32 recovery code (e.g.,
  `JBSW-Y3DP-EHPK-3PXP-GEBA-CQ`). Display once on screen; require Admin to type the
  code into a confirmation field before installer continues (so they prove they wrote
  it down). Use a **key-wrap** scheme: a random 256-bit data-key (DK) encrypts every
  backup snapshot; DK is wrapped twice — once with `KDF(install-password)`, once with
  `KDF(recovery-code)` — and both wrapped blobs are stored in the snapshot header.
  Restore tries both wrappings; whichever unwraps DK proceeds.
- **KDF**: PBKDF2-HMAC-SHA-256, 600 000 iterations (OWASP 2023 recommendation),
  16-byte random salt per snapshot.
- **Storage**: the recovery code is **never persisted** in the live database. The
  install flow stores only the wrapped DK. Loss of *both* secrets makes existing
  snapshots unrecoverable (accepted risk per spec Clarification round 1).
- **Health surfacing**: `GET /api/admin/health` includes
  `recoveryCodeAcknowledgedAtInstall: true|false` so missing-acknowledgement is
  visible after the fact (FR-026).
- **Endpoint status**: existing locked endpoints `POST /api/admin/backup-now` and
  `POST /api/admin/restore` accommodate the new payload field (`backupSecret` accepts
  either secret); no new endpoint needed; restore flow may need a small contract update
  (multipart form gains an optional `recoveryCode` field). See ADR-0006.

**Alternatives**: store the recovery code encrypted at rest (rejected — defeats the
"offline paper" requirement; an attacker who reads the live DB shouldn't get the
backup-decryption capability).

---

## 6. Daily-Header Officer Name (FR-025)

- **Decision**: new endpoint
  `PATCH /api/daily-headers/{date}/officer { "name": "..." }` (any authenticated user,
  per FR-027). Domain rule: only today's header is editable; future and historical
  headers reject with 409. Server timestamps every change; one AuditLog row per change
  capturing before / after.
- **UI**: inline editable cell on the ledger's daily-header row (`§ 6.2`).
  ViewModel exposes `EditableCell<string>` with optimistic update + revert-on-failure.
- **Endpoint status**: not in `02-architecture.md § 6`. See ADR-0006.

**Alternatives**: dedicated "shift schedule" entity (rejected — FR-025 explicitly
forbids in Phase 1); operator-only lock (rejected — multi-shift gates need any-role
edit per the Q&A in spec Clarifications).

---

## 7. F-17 Smart Suggestions — Phase 1 "simple v1"

Implements the Phase-1 scope box added to F-17 in `spec.md`:

- **Source**: live `Vehicle` rows for the last 90 days (`EntryTime > now() - 90 days`).
  No separate aggregation table.
- **Algorithm**: top-1 per (input field, key) — for the trailer input given the just-
  entered plate, run
  `SELECT TrailerOrContainerNumber, COUNT(*) c FROM Vehicle
   WHERE PlateNumber = @plate AND EntryTime > @cutoff
   GROUP BY TrailerOrContainerNumber ORDER BY c DESC, MAX(EntryTime) DESC LIMIT 1`.
  Tie-break: most recent occurrence wins.
- **Latency budget**: < 100 ms p95. Indexed on `(PlateNumber, EntryTime)` and
  `(CompanyName, EntryTime)`. If the budget is missed twice in a row in a single
  desktop session, the suggestion zone hides and an INFO log fires.
- **No ML, no embeddings, no tuning UI, no Admin surface.** Anything beyond is a
  Phase-2 spec.
- **API path**: piggyback on a new lightweight endpoint
  `GET /api/vehicles/suggest?plate=…&field=trailer|company|goods` (any authenticated
  user). See ADR-0006.

**Alternatives**: pre-aggregated `PairStatistics` table refreshed by a Quartz job
(rejected — adds infra complexity for a P1 simple-v1 feature). Client-side cache of
recent pairs (rejected — won't surface cross-operator history).

---

## 8. Recommended ADR-0006 — API Surface Extensions

> **Why**: the spec's `FR-022 / FR-025 / FR-026 / FR-027` reference endpoints not in
> the locked API surface (`02-architecture.md § 6`). The `prompts.md:213` directive
> explicitly forbids inventing new endpoints **without** an ADR. This research entry
> drafts the ADR; it must be merged in **Sprint S0** alongside ADR-0001..0005.

```markdown
# ADR-0006: API surface extensions for FR-022 / FR-025 / FR-026 / FR-027 / F-17

Status: Proposed
Date: 2026-05-03

## Context

The locked API surface in `specs/02-architecture.md § 6` predates the Specify /
Clarify rounds that added FR-022 (declaration cancellation), FR-025 (daily-header
officer name), FR-026 (recovery-code-aware backup payload), FR-027 (full role
authorization matrix), and the F-17 simple-v1 suggestion endpoint. The implementation
needs server endpoints to satisfy these requirements; ignoring the spec or breaking
the API-stability promise both create churn.

## Decision

Extend `02-architecture.md § 6` with the following endpoints. All keep the path
prefix, JWT bearer auth, ProblemDetails error shape, and Idempotency-Key behaviour
of the existing surface. None alter or remove an existing endpoint.

| Method | Path | Auth | Source |
| --- | --- | --- | --- |
| POST | `/api/auth/change-password` | any (self) | F-01 first-login flow + FR-017 |
| PATCH | `/api/daily-headers/{date}/officer` | any | FR-025 |
| POST | `/api/declarations/{id}/cancel` | Admin | FR-022 / FR-023 |
| POST | `/api/vehicles/{id}/un-exit` | Admin | F-08 admin override (already implied by spec) |
| GET | `/api/vehicles/suggest?plate=…&field=…` | any | F-17 simple v1 |
| GET | `/api/admin/logs` | Admin | F-18 in-app log viewer |
| (extension) `POST /api/admin/restore` body | accepts `recoveryCode` as alternative to `password` | Admin | FR-026 |
| (extension) `POST /api/admin/backup-now` body | accepts an optional `targets` array; default = all configured targets | Admin | F-15 |

The full role × endpoint matrix is `spec.md` FR-027 — this ADR makes that matrix the
authoritative one and supersedes the per-row `Auth` column in
`02-architecture.md § 6`.

## Consequences

- `contracts/openapi.yaml` is the new single source of truth for API surface.
- Future spec rounds can add endpoints by amending FR-027 + this ADR; no
  duplication in `02-architecture.md`.
- Client (Refit) interface gains seven typed methods + two field additions.

## Alternatives considered

- *Refuse the extensions and push back on FR-022 / FR-025 / FR-026.* Rejected:
  those FRs were stakeholder-approved during /speckit.clarify; reverting them
  would re-open closed clarifications.
- *Wedge the new behaviour into existing endpoints* (e.g., cancellation as a PUT
  on the declaration). Rejected: muddies the audit trail and breaks the principle
  that one endpoint = one command.
```

> Place the file at `docs/adr/ADR-0006-api-surface-extensions.md` during S0.

---

## 9. Constitution Mapping (each principle → research evidence)

| Principle | Where addressed in this document |
| --- | --- |
| I  Constraint-Driven Architecture (C1–C8) | § 2.1 (no remote sinks), § 2.5 (no remote logs), § 2.7 (idempotency for retries), § 3 (tri-state never queues local writes — C8) |
| II Locked Tech Stack & ADR Governance | § 2 cross-cuttings reuse only the locked stack; § 8 drafts ADR-0006 (only addition) |
| III Code Quality | § 1 places state machines in Domain (no outward refs); § 2.6 standard ProblemDetails (no per-endpoint shapes) |
| IV Test-First Discipline | § 1 invariants are unit-testable; § 2.4 single integration test for FR-003; § 7 latency budget is testable |
| V Layered Dependency Rules | § 1 state machines in Domain; § 2.4 Audit middleware in Server.Api; § 2.7 Idempotency middleware in Server.Api; Domain stays pure |
| VI Vietnamese-First UX & Accessibility | § 2.10 split between English error codes and Vietnamese copy; § 2.11 token enforcement |
| VII Security & Auditability | § 2.1 (DPAPI), § 2.4 (immutable audit), § 5 (recovery code never persisted), § 2.8 (FR-024 race) |
| VIII UI Wireframes Binding | Plan §§ Wireframe→Code mapping; this doc § 3 (banner + badge per § 5) |

---

## 10. Open Items (none blocking)

None. All `NEEDS CLARIFICATION` from the plan template are resolved above. ADR-0006
is the only **artifact** that must be authored and merged before it is referenced
from any code; that authoring is a Sprint S0 task.
