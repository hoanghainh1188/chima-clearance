# Implementation Plan: Chima Customs B2 — Vehicle Clearance Log Replacement

**Branch**: `feat/001-b2-clearance` (logical feature `001-b2-clearance`) | **Date**: 2026-05-03
**Spec**: [`spec.md`](./spec.md)
**Input**: Feature specification from `specs/001-b2-clearance/spec.md`
**Constitution**: [`v1.1.0`](../../.specify/memory/constitution.md)
**Source authorities (binding)**: [`specs/01-overview.md`](../01-overview.md) §§ 2, 6, 7 ·
[`specs/02-architecture.md`](../02-architecture.md) §§ 1–10 ·
[`specs/04-implementation-plan.md`](../04-implementation-plan.md) §§ 1–14 ·
[`specs/05-ui-wireframes.md`](../05-ui-wireframes.md) §§ 1–11

---

## Summary

Replace the shared-Excel "Sổ B2" workflow at Customs Gate B2 with a **modular monolith
client/server system over LAN**: `B2.Server.Service` (Windows Service) hosts an
ASP.NET Core 8 Minimal API + SignalR hub on the entry-gate PC; up to five `B2.Desktop`
(.NET MAUI 8 Windows) clients consume it. Persistence is **SQLite (WAL mode)**,
continuously replicated by **Litestream** to USB + LAN folder. Eighteen features
(F-01..F-18) deliver Phase 1; F-17 is now P1 with a **simple v1** scope per
[`spec.md` Clarification Q2](./spec.md). The technical approach below restates and
binds the architecture already locked in `specs/02-architecture.md` — this plan
**does not** redesign anything, it only enumerates the artifacts and gate checks the
implementation phase must respect.

---

## Technical Context

**Language/Version**: C# 12 on **.NET 8 (LTS)** — `net8.0` for libraries,
`net8.0-windows10.0.19041.0` for `B2.Desktop`.
**Primary Dependencies** (locked, [`01-overview.md § 6`](../01-overview.md);
deviations require ADR per Constitution II):

- Desktop UI: **.NET MAUI 8** + **CommunityToolkit.Maui** + **CommunityToolkit.Mvvm**
  (source generators); custom resource dictionaries for theming. `WindowsPackageType=None`.
- API: **ASP.NET Core 8 Minimal API**.
- Background jobs: **Quartz.NET** hosted service.
- Realtime: **SignalR** over LAN, fallback to polling at ≤ 3 s (per `FR-028`).
- ORM: **EF Core 8** + **Microsoft.Data.Sqlite**.
- Validation: **FluentValidation**.
- Excel I/O: **ClosedXML** (template-based against `Mẫu sổ B2.xlsx`).
- Auth: **JWT RS256** + **BCrypt.Net-Next**; signing key sealed by **DPAPI**.
- Logging: **Serilog** (rolling file sink + in-memory sink for the in-app viewer).
- HTTP client: **Refit** + **Polly v8** (retry, exponential backoff).
- Continuous backup: **Litestream** daemon (child of `B2.Server.Service`).
- Installer: ~~**Velopack** (preferred) or **Inno Setup**~~ → **superseded by ADR-0007 / PR #103**:
  a plain CLI (`B2Clearance.exe install|uninstall`) driven by `deploy/install.ps1` (xcopy), with
  self-contained publish so the gate PCs need no runtime install and no update feed.
- Testing: **xUnit** + **FluentAssertions** + **NSubstitute** +
  `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory<Program>`) +
  in-memory `Microsoft.Data.Sqlite` (shared cache).
- Performance: **NBomber** for scheduled load runs against the spec VM.

**Storage**: SQLite at `C:\ProgramData\B2\db\b2.db`. PRAGMAs applied at startup:
`journal_mode=WAL`, `synchronous=NORMAL`, `temp_store=MEMORY`, `mmap_size=268435456`,
`busy_timeout=5000` (per [`04-implementation-plan.md § 5.1`](../04-implementation-plan.md)).

**Target Platform**: Windows 10 64-bit (21H2 or newer); Office 2010 only relevant for
the Excel-handoff smoke test.

**Project Type**: **desktop-app + lan-service** (single-host server hosts API + SignalR;
multiple desktop clients on the LAN).

**Performance Goals** (per [`01-overview.md § 7`](../01-overview.md) and
[`04-implementation-plan.md § 11`](../04-implementation-plan.md), restated as
`SC-001..SC-016`): cold start < 5 s; single-user write < 200 ms p95; suffix search over
50 k vehicles < 200 ms p95; 30-day Excel export (~3 500 declarations) < 10 s; service
RAM < 250 MB after 24 h; live-updates ≤ 1 s in 🟢, ≤ 3 s in 🟡 polling fallback.

**Constraints**: Constitution Principle I (C1–C8): LAN-only, no internet, low-spec
hardware (i5-3570 / 4 GB), no IT team, native desktop UX, single-host server with
continuous backup, Service runs 24/7 independent of user login, ~5 concurrent users,
no offline-first on exit/supervisor PCs.

**Scale/Scope**: 5 concurrent users (10 burst), ~248 declarations / day (per
wireframe sample), ~3 500 declarations / month, 3-year online retention then annual
archive (`FR-021`). 18 features, 12 wireframe pages, ~30 REST endpoints + 1 SignalR hub.

> **No `NEEDS CLARIFICATION` markers remain.** All ambiguities were resolved in two
> `/speckit.clarify` rounds (see `spec.md` → Clarifications). The new endpoint
> additions surfaced by `FR-022 / FR-025 / FR-026 / FR-027` are documented in
> [`research.md`](./research.md) with a recommended **ADR-0006** to formally extend
> the locked API surface.

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| # | Principle | Status | Evidence |
|---|---|---|---|
| I | Constraint-Driven Architecture (C1–C8) | ✅ PASS | All eight constraints honored (see Technical Context > Constraints). Single-host server (C5) backed by Litestream replication (C5 mitigation). |
| II | Locked Tech Stack & ADR Governance | ✅ PASS | Every dependency in Technical Context appears in `01-overview.md § 6`. Five required ADRs (0001–0005) listed in `04-implementation-plan.md § 9` will be authored in Sprint S0 (Bootstrap). One **new ADR-0006** is recommended for the API surface extensions in `research.md` — to be merged before any code in S2/S5/S6 that depends on them. |
| III | Code Quality (Nullable + TreatWarningsAsErrors) | ✅ PASS | All `.csproj` files set `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, `<LangVersion>latest</LangVersion>`. `dotnet format` enforced in CI per `04-implementation-plan.md § 7`. |
| IV | Test-First Discipline (≥ 80 % aggregate; per-layer thresholds) | ✅ PASS | Test projects scaffolded in S0: `B2.Domain.Tests` (xUnit + FluentAssertions, ≥ 90 % line), `B2.Application.Tests` (xUnit + NSubstitute, ≥ 80 %), `B2.Server.Api.Tests` (`WebApplicationFactory<Program>` + in-memory shared-cache SQLite, every endpoint + every Gherkin AC tagged `[Trait("Feature","F-XX")]`), `B2.Desktop.Tests` (ViewModel unit + accessibility AC from `05-ui-wireframes.md § 9`). NBomber performance suite per `04-implementation-plan.md § 6`. |
| V | Layered Dependency Rules | ✅ PASS | Project structure (below) mirrors `04-implementation-plan.md § 3.4`. `B2.Domain` references nothing outward. No project references `B2.Desktop`. `B2.Desktop` ↔ `B2.Server.Api` share types **only** via `B2.Shared.Contracts`. An `ArchUnitNET` test in `B2.Server.Api.Tests` will assert these rules. |
| VI | Vietnamese-First UX & Accessibility (WCAG 2.1 AA) | ✅ PASS | Localization strings live in `B2.Desktop/Resources/Strings/AppResources.vi.resx`. Color and typography tokens in `Resources/Styles/Colors.xaml` + `Typography.xaml` carry verified-contrast comments per `05-ui-wireframes.md § 2–3`. Keyboard parity tested per `§ 9`. Toast / Modal services in `B2.Desktop/Services/`. |
| VII | Security & Auditability | ✅ PASS | Every write endpoint guarded by ASP.NET Core authorization policy (`AdminOnly`, `OperatorOrAbove`, `SupervisorOrAbove`); `AuditMiddleware` writes one immutable `AuditLog` row per successful write. JWT RS256 with DPAPI-sealed signing key, BCrypt cost 10 (passwords ≥ 10 chars, 3-of-4 char classes), AES-256 backup archives, `gitleaks` in CI, default Admin forced password change on first login, lockout + idle timeout active. Self-signed cert deployed to `LocalMachine\Root` by installer. |
| VIII | UI Wireframes Binding | ✅ PASS | 12 wireframes (`§ 6`) → 12 XAML pages + 12 ViewModels + 12 Shell routes (table in [`research.md`](./research.md)). Tokens declared once in `Colors.xaml` + `Typography.xaml`; `TabIndex` enforces tab order; `Platforms/Windows/` registers keyboard accelerators; every list view implements the four states from `§ 8`; status indicators always combine icon + color + text label; Vietnamese copy from wireframes is transcribed verbatim. |

**Gate decision**: ✅ **All gates pass.** No principle is violated; **Complexity Tracking
section is therefore intentionally empty** (no justifications needed). One advisory:
ADR-0006 (API surface extensions) is the only ADR not already enumerated by the
implementation-plan source; it is recommended in `research.md` and should be merged
in Sprint S0 alongside ADR-0001..0005.

---

## Project Structure

### Documentation (this feature)

```text
specs/001-b2-clearance/
├── plan.md               # This file (/speckit.plan output)
├── research.md           # Phase 0 output — state machines, decisions, ADR-0006 recommendation
├── data-model.md         # Phase 1 output — entities, INV-1..INV-8, state machines, retention
├── contracts/
│   └── openapi.yaml      # Phase 1 output — API surface § 6 + FR-022/025/026/027 deltas
├── quickstart.md         # Phase 1 output — dev bootstrap + smoke checklist
├── checklists/
│   └── requirements.md   # Spec-quality checklist (from /speckit.specify)
└── tasks.md              # Phase 2 output — written by /speckit.tasks (NOT by /speckit.plan)
```

### Source Code (repository root) — bound by `04-implementation-plan.md § 3` and `02-architecture.md § 3`

```text
chima-clearance/
├── src/
│   ├── B2.Domain/                    # Entities, VOs (PlateNumber, ContainerNumber,
│   │                                 #   DeclarationNumber), Domain Services
│   │                                 #   (DailyHeader, KVGS state machine).
│   │                                 #   Zero outward refs (not even Microsoft.Extensions.*).
│   ├── B2.Application/               # Use cases (MediatR-style without MediatR — see
│   │                                 #   research.md), DTOs, FluentValidation validators,
│   │                                 #   Repository interfaces, Domain event dispatcher.
│   ├── B2.Infrastructure/            # EF Core 8, repositories, Quartz.NET jobs,
│   │                                 #   Litestream wrapper, ClosedXML adapter,
│   │                                 #   DPAPI key store, BCrypt + JWT services.
│   ├── B2.Server.Api/                # Minimal API endpoint groups, SignalR Hub
│   │                                 #   (/hubs/events), JWT middleware, Audit
│   │                                 #   middleware, ProblemDetails handler,
│   │                                 #   Idempotency-Key middleware.
│   ├── B2.Server.Service/            # Windows Service host (worker template); hosts
│   │                                 #   B2.Server.Api in-process; supervises Litestream
│   │                                 #   child process; configures Serilog.
│   ├── B2.Shared.Contracts/          # Request/response DTOs shared between
│   │                                 #   B2.Desktop and B2.Server.Api. No logic.
│   ├── B2.Desktop/                   # .NET MAUI 8, Windows target only.
│   │   ├── Views/                    #   12 XAML pages = 12 wireframes (§ 6.1–6.12)
│   │   │                             #   + AppShell.xaml.
│   │   ├── ViewModels/               #   1 ViewModel per page; CommunityToolkit.Mvvm
│   │   │                             #   source generators (`[ObservableProperty]`,
│   │   │                             #   `[RelayCommand]`).
│   │   ├── Services/                 #   IB2Api (Refit), AuthState, SignalRClient,
│   │   │                             #   ToastService, ModalService, ConnectivityState
│   │   │                             #   (FR-028 tri-state), LocalizationService.
│   │   ├── Resources/
│   │   │   ├── Styles/Colors.xaml         # Tokens from § 2 (StaticResource only).
│   │   │   ├── Styles/Typography.xaml     # Tokens from § 3.
│   │   │   ├── Strings/AppResources.vi.resx # Vietnamese copy from wireframes.
│   │   │   └── Fonts/                     # Segoe UI fallback (Inter optional).
│   │   ├── Platforms/Windows/        #   Keyboard accelerators (Alt+1..Alt+8 nav,
│   │   │                             #   Ctrl+Enter on Entry Form, F1 cheat sheet).
│   │   └── MauiProgram.cs            #   DI registration.
│   └── B2.Installer/                 # Offline installer CLI (ADR-0007; Velopack dropped).
│                                     #   Bundles: dotnet-runtime, self-signed cert,
│                                     #   Litestream binary, Sổ B2 template, recovery
│                                     #   code generator (FR-026).
├── tests/
│   ├── B2.Domain.Tests/              # ≥ 90 % line coverage; pure unit.
│   ├── B2.Application.Tests/         # ≥ 80 %; NSubstitute mocks.
│   ├── B2.Server.Api.Tests/          # WebApplicationFactory + in-memory SQLite
│   │                                 #   shared-cache. Every endpoint + every
│   │                                 #   Gherkin AC. ArchUnit dependency tests.
│   ├── B2.Desktop.Tests/             # ViewModel unit + accessibility AC.
│   └── B2.Performance.Tests/         # NBomber scenarios; runs nightly on spec VM.
├── docs/
│   ├── adr/                          # ADR-0001..0005 (S0) + ADR-0006 (S0/before S2).
│   ├── runbook.md                    # Vietnamese 5-page operator runbook (S6).
│   └── prompts.md, BOOTSTRAP.md      # (existing) Spec Kit prompts and bootstrap.
├── specs/                            # (this directory tree).
└── .github/workflows/
    ├── ci.yml                        # windows-2022, dotnet workload install maui,
    │                                 #   format / build / test / coverage / gitleaks.
    └── release.yml                   # Tag-triggered installer build.
```

**Structure Decision**: Modular monolith client/server, **eight production projects +
five test projects**, exactly as locked in `02-architecture.md § 3`. The MAUI app is a
**single-target Windows build for Phase 1** (cross-platform option preserved for the
future). The Windows Service hosts the API in-process (no separate Kestrel); Litestream
runs as a supervised child process under the same service.

---

## Phase 0 — Research

**Output**: [`research.md`](./research.md). Produced in this `/speckit.plan` invocation.

Contents (summary):

1. Restated state machines for Declaration (`Open → AllExited → KVGSConfirmed` with
   admin override) and Vehicle (`Inside → Outside` with admin override) per
   `02-architecture.md § 7`.
2. Decisions for cross-cutting concerns from `02-architecture.md § 8` —
   authentication, authorization, validation, audit log, logging, error handling,
   idempotency, optimistic concurrency, time, i18n, accessibility — each with
   "Decision / Rationale / Alternatives considered" structure.
3. **Tri-state connectivity machine** (`FR-028`) decision matrix — when each transition
   fires, retry / polling cadence, banner copy.
4. **Cancellation flow** (`FR-022/FR-023`) — admin-only, write to single endpoint, soft
   delete (strike-through in ledger / export), authorization matrix.
5. **Recovery code generation** (`FR-026`) — install-time printable code, key-wrap
   scheme so backups decrypt with either install password or recovery code.
6. **Daily-header officer name** (`FR-025`) — inline edit on ledger row; PATCH endpoint;
   per-edit AuditLog.
7. **Smart-Suggestions simple v1** (`F-17` Phase-1 scope) — top-1 pair from last-90-day
   `GROUP BY` query; latency budget < 100 ms; auto-disable on repeated overrun.
8. **Recommended ADR-0006**: formally extend the locked API surface
   (`02-architecture.md § 6`) with the four endpoint groups required by
   `FR-022 / FR-025 / FR-026 / FR-027`. Drafted in `research.md` for review;
   to be merged in Sprint S0 alongside ADR-0001..0005.

All `NEEDS CLARIFICATION` items resolved.

---

## Phase 1 — Design & Contracts

**Outputs** (all produced in this `/speckit.plan` invocation):

- [`data-model.md`](./data-model.md) — eight aggregates / entities, attributes,
  relationships, the eight invariants `INV-1..INV-8` with the layer that enforces
  them, two state machines (Declaration, Vehicle), retention/archive flow (`FR-021`),
  and the `BackupSnapshot` + `Archive` projections.
- [`contracts/openapi.yaml`](./contracts/openapi.yaml) — every endpoint from
  `02-architecture.md § 6` plus the `FR-022/025/026/027`-driven additions, marked
  `x-source: locked` or `x-source: pending-adr-0006`. JWT bearer security scheme;
  `ProblemDetails` (RFC 7807) error responses; `Idempotency-Key` header on
  `POST /api/declarations` and `POST /api/vehicles/{id}/exit`; role-policy mapping
  per `FR-027`.
- [`quickstart.md`](./quickstart.md) — developer bootstrap (clone → restore → run)
  plus the 10-step spec-VM smoke checklist (`04-implementation-plan.md § 8`).
- **Agent context update** — `.specify/scripts/bash/update-agent-context.sh claude`
  updates `CLAUDE.md` (or AGENTS.md) at the repo root with the locked stack
  delta from this plan; manual annotations between markers are preserved.

### Wireframe → Code mapping (binding for `B2.Desktop`)

| Wireframe (§ 6.X) | XAML / ViewModel | Shell route | Features |
|---|---|---|---|
| 6.1 Login | `Views/LoginPage.xaml` / `LoginViewModel` | `//login` | F-01 |
| 6.2 Ledger | `Views/LedgerPage.xaml` / `LedgerViewModel` | `//ledger` | F-07 (search), F-10, F-11 (header row), F-12 (summary cell) |
| 6.3 Entry Form | `Views/EntryPage.xaml` / `EntryViewModel` | `//entry` | F-05, F-06, F-17 (simple v1) |
| 6.4 Exit Search | `Views/ExitPage.xaml` / `ExitViewModel` | `//exit` | F-07, F-08 |
| 6.5 KVGS Confirm | `Views/KvgsPage.xaml` / `KvgsViewModel` | `//kvgs` | F-09 |
| 6.6 Reports | `Views/ReportsPage.xaml` / `ReportsViewModel` | `//reports` | F-14 |
| 6.7 Export Excel | `Views/ExportPage.xaml` / `ExportViewModel` | `//export` | F-13 (badges per Q3) |
| 6.8 Admin Users | `Views/Admin/UsersPage.xaml` / `UsersViewModel` | `//admin/users` | F-02 |
| 6.9 Admin Master Data | `Views/Admin/MasterDataPage.xaml` / `MasterDataViewModel` (tabbed) | `//admin/master` | F-03, F-04 |
| 6.10 Admin Backup/Restore | `Views/Admin/BackupPage.xaml` / `BackupViewModel` | `//admin/backup` | F-15, FR-026 |
| 6.11 Admin Health & Logs | `Views/Admin/HealthPage.xaml` / `HealthViewModel` (tabbed) | `//admin/health` | F-18 |
| 6.12 Admin Audit Log | `Views/Admin/AuditPage.xaml` / `AuditViewModel` | `//admin/audit` | F-16 |
| App Shell | `AppShell.xaml` / `AppShellViewModel` | (root) | F-01 logout, FR-028 connectivity badge |

### Background jobs (Quartz.NET, configured in `B2.Server.Service`)

| Job | Cron | Catch-up logic |
|---|---|---|
| `CreateDailyHeaderJob` | `0 1 0 * * ?` (00:01 daily) | On service startup, check today's header; if missing, insert it. Idempotent (`F-11` AC "Idempotent"). |
| `GenerateDailySummaryJob` | `0 0 23 * * ?` (23:00 daily) | On startup, check whether yesterday's summary was populated; if not, run for yesterday once. |
| `AnnualArchiveJob` | `0 0 1 1 2 ?` (01:00, 1 February, default — Admin-configurable per `FR-021`) | Reads everything older than 3 years, writes Excel + PDF archives, verifies, then deletes. Failure = no delete + Admin alert. |
| `IdempotencyKeyEvictionJob` | every 30 min | Delete idempotency keys older than 24 h (per `02-architecture.md § 8`). |

### Backup / Restore flow (Litestream + on-demand snapshot)

- **Continuous**: Litestream runs as a child process of `B2.Server.Service`. It
  replicates SQLite WAL frames every 60 s to two configured targets (`USB D:\B2-Backup\replica\`,
  `LAN \\entry-pc\B2-Backup\`). Health endpoint surfaces the last-replica timestamp per target.
- **On-demand snapshot**: Admin invokes `POST /api/admin/backup-now` (locked path).
  Server runs `sqlite3 .backup`, packages the resulting file as
  `b2-YYYYMMDD-HHmm.bak.zip`, encrypts with **AES-256** using a data key wrapped with
  *both* the install-time password *and* the printable recovery code (`FR-026`), and
  drops a copy at every backup target.
- **Restore**: Admin uploads a snapshot via `POST /api/admin/restore` (multipart). Service
  pauses writes, swaps the DB file, restarts Kestrel, broadcasts a SignalR "reconnect"
  event. Downtime budget ≤ 30 s (`SC-009`). Confirmation requires typing
  `"RESTORE"` (`FR-013`); the upload form accepts either the install password or the
  recovery code (`FR-026`).

### Constitution re-check (post-design)

Re-evaluated after Phase 1 outputs are written:

- ✅ I — `data-model.md` invariants and `contracts/openapi.yaml` paths preserve all C1–C8.
- ✅ II — No new dependencies introduced beyond the locked stack. ADR-0006 is the
  only addition and is for *endpoint extensions*, not a new library.
- ✅ III — Project layout enforces `Nullable` and `TreatWarningsAsErrors` via
  `Directory.Build.props` (S0 task).
- ✅ IV — Every API path in the contract is annotated with the Feature ID it serves;
  test naming convention `Should_<criterion>` + `[Trait("Feature","F-XX")]` is binding.
- ✅ V — Dependency rules unchanged; ArchUnit assertion documented in S0 plan.
- ✅ VI — Every endpoint that produces user-facing copy returns it via DTOs that the
  Desktop layer renders from `AppResources.vi.resx` (no Vietnamese in API responses
  beyond field values entered by users themselves, e.g., goods names).
- ✅ VII — Authorization policies in `contracts/openapi.yaml` mirror `FR-027` exactly.
- ✅ VIII — Wireframe → Code mapping table above is the binding 1:1 between `§ 6.1–6.12`
  and the Desktop project.

**Post-design gate**: ✅ all eight principles still satisfied. No new violations.

---

## Complexity Tracking

> **No constitutional violations to justify. This section is intentionally left
> empty.**

The plan introduces no library, layer, or pattern beyond the locked technology
table (`01-overview.md § 6`) and the locked architecture (`02-architecture.md`).
The single advisory item (ADR-0006 for API surface extensions) is a documentation
artifact, not a technology change.

---

## Sprint S4 progress (2026-05-05) — merged via PR #18 (`9f0d15b`)

**Done**: `T-S4-01`–`T-S4-15` shipped on `main`:

- Quartz scheduling wires daily header (`0 1 0 * * ?` Asia/Ho_Chi_Minh) and summary (`0 0 23 * * ?`) jobs.
- `DailyHeader` repository + handlers cover ensure-header, recompute-summary, and officer-name edit.
  Audit handlers branch by `isNew` so the append-only AuditLog records `Create` vs
  `Update`/`OfficerNameChanged` accurately.
- New endpoints: `POST /api/daily-headers/{date}/recompute` (Admin),
  `PATCH /api/daily-headers/{date}/officer` (any-authenticated, INV-11 today-only),
  `GET /api/daily-headers/?from=&to=` (officer-name overlay for ledger), and
  `GET /api/reports/{by-processor|by-yard|by-hour|by-officer|pending}` (`SupervisorOrAbove`).
- New SignalR broadcast `DailyHeaderOfficerChanged` on `EventsHub` so other clients
  converge on officer-name edits without a full reload.
- Reports query handlers + repository projections for F-14 are live and covered by API tests.
- Desktop ledger now includes inline officer edit (optimistic + revert-on-failure)
  and an Admin-only `Tính lại` button on each daily-header row. Rebuilt `ReportsPage`
  per wireframe § 6.6: date-range pickers, IncludeCancelled toggle, sortable column grids
  per section, `Sao chép` (TSV → clipboard), `Xuất Excel` (ClosedXML to
  `~/Documents/B2-Reports-yyyyMMdd-HHmmss.xlsx`, off the UI thread).
- Policy-alignment architecture test added for mapped `/api/*` endpoints (carry-over
  `T-S4-CO-01` strengthens this to verify specific FR-027 policies).
- `HttpAuditContext.Set` now no-ops outside the HTTP pipeline so Quartz cron jobs
  and the startup catch-up host don't crash the Windows Service on boot. Audit per
  FR-003 binds to "successful mutating requests"; cron-driven runs leave their trail
  in the data itself.

**Validation**: green on Windows MAUI CI (`build-and-test`) + Ubuntu NoMaui +
`gitleaks`. Local pre-push gates (`dotnet format B2.NoMaui.slnf`,
`dotnet build B2.NoMaui.slnf -c Release /p:TreatWarningsAsErrors=true`,
`dotnet test B2.NoMaui.slnf -c Release --no-build`) all pass on macOS.

**Carry-over** (`T-S4-CO-01..09`): items intentionally deferred from the PR-18
review pass — see `tasks.md` Phase 5 tail for the enumerated list.

---

## Phase 2 — Out of scope for this command

`/speckit.tasks` consumes this plan + `data-model.md` + `contracts/openapi.yaml` to
produce `tasks.md` (sprint S0–S6 schedule per `04-implementation-plan.md § 2`).
That command is **not** invoked here.
