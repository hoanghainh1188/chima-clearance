# Chima Customs B2 — Vehicle Clearance Log

Replacement for the shared-Excel "Sổ B2" workflow at Customs Gate B2 (Cửa khẩu
Chi Ma). LAN-only modular monolith with a Windows Service hosting an ASP.NET
Core 8 Minimal API + SignalR hub, consumed by .NET MAUI 8 Windows desktop
clients. SQLite + Litestream for persistence and continuous backup.

> **Authoritative documentation** lives under [`specs/`](./specs/). Read in this
> order:
>
> 1. [`specs/01-overview.md`](./specs/01-overview.md) — context, constraints, locked tech stack
> 2. [`specs/02-architecture.md`](./specs/02-architecture.md) — components, sequences, ERD
> 3. [`specs/03-functional-spec.md`](./specs/03-functional-spec.md) — 18 features, Gherkin AC
> 4. [`specs/04-implementation-plan.md`](./specs/04-implementation-plan.md) — sprint plan, DoD
> 5. [`specs/05-ui-wireframes.md`](./specs/05-ui-wireframes.md) — 12 wireframes, design tokens
> 6. [`specs/001-b2-clearance/`](./specs/001-b2-clearance/) — Spec Kit feature folder
>    (spec.md, plan.md, research.md, data-model.md, contracts/openapi.yaml, tasks.md, analyze-report.md)
> 7. [`.specify/memory/constitution.md`](./.specify/memory/constitution.md) — non-negotiable rules (v1.1.0)
> 8. [`docs/adr/`](./docs/adr/) — Architecture Decision Records (ADR-0001..0006 locked;
>    ADR-0007 evaluated a Go-backend port — rejected, stack stays .NET; ADR-0008 dev-only
>    PDF export tool for the operator docs)

---

## Repository layout

```text
chima-clearance/
├── B2.sln                          # full solution (incl. MAUI Desktop)
├── B2.NoMaui.slnf                  # solution filter excluding B2.Desktop
├── Directory.Build.props           # Constitution III code-quality props (applied to all projects)
├── .editorconfig                   # `dotnet format` source of truth
├── global.json                     # pin .NET 8 SDK
├── src/
│   ├── B2.Domain/                  # Entities, Value Objects, Domain Services. ZERO outward refs.
│   ├── B2.Application/             # Use cases, DTOs, FluentValidation, repository interfaces.
│   ├── B2.Infrastructure/          # EF Core, repositories, Quartz jobs, Litestream wrapper, ClosedXML.
│   ├── B2.Server.Api/              # Minimal API + SignalR Hub + middleware.
│   ├── B2.Server.Service/          # Windows Service host (worker template) hosts B2.Server.Api.
│   ├── B2.Shared.Contracts/        # Server↔Desktop DTOs (no logic).
│   ├── B2.Desktop/                 # .NET MAUI 8 (Windows target only Phase 1). Built only on Windows runners with the MAUI workload.
│   └── B2.Installer/               # Standalone offline installer CLI (B2Clearance.exe install|uninstall).
├── tests/
│   ├── B2.Domain.Tests/            # ≥ 90 % line coverage target.
│   ├── B2.Application.Tests/       # ≥ 80 %.
│   ├── B2.Server.Api.Tests/        # WebApplicationFactory + in-memory SQLite + ArchUnit layer rules.
│   ├── B2.Desktop.Tests/           # ViewModel unit + accessibility AC.
│   └── B2.Performance.Tests/       # NBomber scenarios; nightly on the spec VM.
├── docs/
│   ├── adr/                        # ADR-0001..0006 (see § 9 of impl plan) + ADR-0007 (Go-port eval) + ADR-0008 (docs PDF tool).
│   ├── huong-dan-cai-dat.md        # Vietnamese step-by-step install guide (gate operators).
│   ├── runbook.md                  # Vietnamese operator runbook (S6 deliverable).
│   ├── prompts.md                  # Spec Kit prompts (per-skill directives).
│   └── BOOTSTRAP.md                # Workflow bootstrap notes.
├── specs/                          # ALL spec authority lives here.
└── .github/workflows/
    └── ci.yml                      # windows-2022 (full) + ubuntu-latest (NoMaui quick check) + gitleaks.
```

---

## Build & test (local)

### Prerequisites

| Tool | Version | Notes |
| --- | --- | --- |
| .NET SDK | **8.0.x** (LTS) | `global.json` pins it. macOS/Linux can build everything except `B2.Desktop`. |
| MAUI workload | latest | `dotnet workload install maui` — **Windows only** for Phase 1. Skip on macOS/Linux and use `B2.NoMaui.slnf`. |
| Git | 2.40+ | — |

### Build

```bash
# Full solution (Windows + MAUI workload installed):
dotnet restore B2.sln
dotnet build B2.sln -c Release /p:TreatWarningsAsErrors=true
dotnet test B2.sln -c Release

# Cross-platform subset (no MAUI workload required):
dotnet restore B2.NoMaui.slnf
dotnet build B2.NoMaui.slnf -c Release /p:TreatWarningsAsErrors=true
dotnet test B2.NoMaui.slnf -c Release
dotnet format B2.NoMaui.slnf --verify-no-changes
```

### Run the dev server

```bash
dotnet run --project src/B2.Server.Service -- --console
# Listens on https://localhost:5001 (dev cert; trust via `dotnet dev-certs https --trust`).
```

For the Desktop dev loop, see [`specs/001-b2-clearance/quickstart.md § 4`](./specs/001-b2-clearance/quickstart.md).

---

## Sprint status

Phase 1 ships in 6 × 2-week sprints (S0..S6) per
[`specs/04-implementation-plan.md § 2`](./specs/04-implementation-plan.md). The
113-task plan lives at
[`specs/001-b2-clearance/tasks.md`](./specs/001-b2-clearance/tasks.md).

| Sprint | Goal | Status |
| --- | --- | --- |
| **S0** | Bootstrap (scaffold, CI, ADRs 0001–0006) | ✅ done — PR #2 |
| **S1** | Core domain + auth (F-01..F-04) | ✅ done — PR #4 |
| **S2** | Entry & exit happy path (F-05..F-08) | ✅ done — PR #5 |
| **S3** | KVGS + ledger + realtime (F-09, F-10) | ✅ done — PRs #6 (core), #7 (R1: FR-028 logging + F-10 test + tokens), #8 (R2: modal popup focus-trap + Toast severity), #9 (R3a: wireframe § 5 sidebar + Alt+1..8 + F1), #11 (R3b: functional Cỡ chữ dropdown), #12 (R3c: KVGS row fade + Đã xác nhận hôm nay widget), #15 (R3d: ledger drawer chi tiết + F5 / `/` / Esc shortcuts), #16 (R3e: list-view skeletons + correlationId on error banners). Low-priority visual polish carries to a backlog: ledger child-vehicle expand/collapse, right-click context menu, 300/400 ms animations on SignalR push, sidebar collapse toggle, focus-ring style sweep, ⚙️ quick-settings menu, top-right toast position. |
| **S4** | Background jobs + reports (F-11, F-12, F-14 + FR-025) | ✅ done — PR #18 (`9f0d15b`: Quartz daily-header `0 1 0 * * ?` + daily-summary `0 0 23 * * ?` jobs in `Asia/Ho_Chi_Minh`; five `/api/reports/*` endpoints with `SupervisorOrAbove`; PATCH `/api/daily-headers/{date}/officer` + SignalR `DailyHeaderOfficerChanged` push; Admin POST `/api/daily-headers/{date}/recompute` exposed as the ledger `Tính lại` button; rebuilt `ReportsPage` per wireframe § 6.6 with sortable columns + clipboard + ClosedXML export; HttpAuditContext no-ops outside HTTP so cron jobs no longer crash the Windows Service host). Docs follow-up in PR #19 (`6514c2e`) and PR #20 (status rollup). 9 carry-overs `T-S4-CO-01..09` deferred to S5 hardening — see [`specs/001-b2-clearance/tasks.md`](./specs/001-b2-clearance/tasks.md) Phase 5 tail. |
| **S5** | Export, backup, audit (F-13, FR-022, F-16, F-18, F-15) | ✅ done — PRs #40–#55 (head `fcb0412`). #40 cleared S4 carry-overs `T-S4-CO-01..09`; **F-13** template-based Sổ B2 Excel export (`GET /api/reports/excel` + `ExportPage`, #41/#43); **FR-022** declaration cancellation (`POST /api/declarations/{id}/cancel`, INV-9/INV-10, ledger strike-through, #44); **F-16** Admin audit viewer (`GET /api/admin/audit` + `AuditPage`, #45); **F-18** Admin health dashboard + in-app log viewer (`GET /api/admin/logs` + `/health/detail` + tabbed `HealthPage`, #46); **F-15** encrypted backup/restore (AES-256-GCM dual-PBKDF2 wrap, `POST /api/admin/backup-now`, apply-on-startup restore `POST /api/admin/restore`, `BackupPage`, recovery-code generator + DPAPI-sealed secrets store, Litestream supervisor — 5 slices #48/#50/#52/#53/#55). CI flake killed for good in #42/#47. Two remainders roll to S6: `T-S5-06` NBomber export scenario (placeholder) + `T-S5-13` install-time recovery-code print UI (→ `T-S6-11`). |
| **S6** | Hardening (F-17 simple v1 + a11y + FR-021 archive + runbook + installer + UAT) | 🔄 in progress — `T-S6-01..11` done, **`T-S6-12` (on-site UAT) is the only task left**. **F-17** sticky plate–trailer suggestions (#59); **a11y** audit + `AxeRunner` static analysis, FR-021 `AnnualArchiveJob`, Vietnamese `docs/runbook.md` (#61); installer + UAT template (#63); UAT-blocking Desktop fixes (#64, #65). Then two post-`v1.0.0` rounds: **#103** self-contained offline deployment — Velopack dropped for a plain `B2Clearance.exe install\|uninstall` CLI + `deploy/install.ps1`, after `ADR-0007` evaluated and **rejected** a Go backend port (stack stays .NET); **#112** FR-021 archive data-loss + FR-022 un-exit race + KVGS lock + installer upgrade path. `docs/uat-001-b2-clearance.md` is the sign-off sheet for the remaining task and is still an unfilled template. |

Released builds: **`v1.0.0`** (2026-06-07) → **`v1.1.0`** (2026-07-19) →
**`v1.1.1`** (2026-07-26) → **`v1.2.0`** (2026-08-02) → **`v1.2.1`** (2026-09-27,
current). `v1.2.0` re-checks the implementation against the unit's own documents —
several rules the code enforced had no basis in them, and the exit-gate PC had never
been deployable (see `CHANGELOG.md`). `v1.2.1` fixes clean installs (v1.1.0 to v1.2.0 fail
at service registration) and ships the Vietnamese install guide
(`docs/huong-dan-cai-dat.md`) inside the zip. **`v1.2.1` is the build to run `T-S6-12`
with**; Phase-1 GA is gated on that sign-off, not on another feature.

---

## Contributing

Read the [Constitution](.specify/memory/constitution.md) v1.1.0 before any code.

- **Constitution Principle II (locked tech stack)** — adding a library outside
  [`specs/01-overview.md § 6`](./specs/01-overview.md) requires a new ADR under
  [`docs/adr/`](./docs/adr/).
- **Constitution Principle V (layered dependencies)** — `B2.Domain` references
  nothing outward. `B2.Server.Api.Tests` enforces this with ArchUnitNET (see
  [`LayerRulesTests.cs`](./tests/B2.Server.Api.Tests/Architecture/LayerRulesTests.cs)).
- **Constitution Principle VIII (UI wireframes binding)** — every Desktop page
  follows [`specs/05-ui-wireframes.md § 6`](./specs/05-ui-wireframes.md);
  Vietnamese copy verbatim into `AppResources.vi.resx`.
- Conventional Commits (`feat(F-05): …`, `chore(s0): …`).
- One feature per branch (`feat/F-XX-short-name`); squash-merge to `main`.

---

## License

To be confirmed by the customs unit before public release.
