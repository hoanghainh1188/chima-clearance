# chima-clearance Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-05-04

> **For AI coding agents (Claude Code, Cursor, Spec Kit)**: this file is the
> short-form briefing. Authoritative sources are `specs/01-overview.md`,
> `specs/02-architecture.md`, `specs/03-functional-spec.md`,
> `specs/04-implementation-plan.md`, `specs/05-ui-wireframes.md`, and
> `.specify/memory/constitution.md`. Read those before generating non-trivial code.

---

## Active feature

| Feature | Spec | Plan | Constitution |
| --- | --- | --- | --- |
| `001-b2-clearance` — Customs Gate B2 vehicle clearance log replacement | [spec.md](specs/001-b2-clearance/spec.md) | [plan.md](specs/001-b2-clearance/plan.md) | [v1.1.0](.specify/memory/constitution.md) |

Active phase: **Sprint S4 complete on branch** — F-11/F-12 background jobs
(daily-header create + daily-summary recompute), FR-025 daily-header
officer-name inline edit, and F-14 report endpoints (processor/yard/hour/
officer/pending) are implemented with test coverage in this working branch.
Next phase is Sprint S5 unless S3 visual-polish backlog items get prioritized
(child-vehicle expand/collapse, right-click context menu, 300/400 ms
SignalR animations, sidebar collapse toggle, focus-ring sweep, quick-settings
menu, top-right toast position).

**Done**: S0 (scaffold, ADRs 0001–0006), S1 (F-01/F-02/F-03/F-04 — auth,
users, master data, force-rotate password), S2 (F-05/F-06/F-07/F-08 —
declaration entry, add child vehicle, suffix search, mark exit), S3 core
(F-09 KVGS + F-10 ledger + SignalR realtime + wireframe § 5 sidebar shell
+ Modal popup focus-trap + Toast severity timing + FR-028 Serilog logging
+ Constitution VI tokens + functional Cỡ chữ dropdown + KVGS row fade-out
animation + self-monitoring counter). **S4 (F-11/F-12/F-14 + FR-025) is
implemented and validated on this branch, pending review/merge.**

---

## Active Technologies (locked — Constitution Principle II)

- **Language / runtime**: C# 12 on .NET 8 LTS. `net8.0` for libraries;
  `net8.0-windows10.0.19041.0` for `B2.Desktop`. `WindowsPackageType=None`.
- **Desktop UI**: .NET MAUI 8 + CommunityToolkit.Maui + CommunityToolkit.Mvvm
  (source generators). Custom resource dictionaries for theming (no third-party
  Material lib).
- **API**: ASP.NET Core 8 Minimal API hosted in-process by `B2.Server.Service`
  (Windows Service). SignalR hub at `/hubs/events`.
- **Background jobs**: Quartz.NET hosted service.
- **ORM**: EF Core 8 + Microsoft.Data.Sqlite.
- **Storage**: SQLite at `C:\ProgramData\B2\db\b2.db`. WAL mode. PRAGMAs applied
  at startup: `journal_mode=WAL`, `synchronous=NORMAL`, `temp_store=MEMORY`,
  `mmap_size=268435456`, `busy_timeout=5000`.
- **Continuous backup**: Litestream (60 s replication to USB + LAN folder).
- **Auth**: JWT RS256 + BCrypt.Net-Next; signing key sealed with DPAPI.
- **Excel I/O**: ClosedXML (template-based against `Mẫu sổ B2.xlsx`).
- **Validation**: FluentValidation.
- **Logging**: Serilog (rolling file + bounded in-memory sink for in-app viewer).
- **HTTP client (Desktop → Server)**: Refit + Polly v8 (retry, exponential
  backoff). `Idempotency-Key` header on POST `/api/declarations` and
  POST `/api/vehicles/{id}/exit`.
- **Installer**: Velopack (preferred) or Inno Setup.
- **Test**: xUnit + FluentAssertions + NSubstitute + `WebApplicationFactory<Program>` +
  in-memory `Microsoft.Data.Sqlite` (shared cache); NBomber for performance.

> **Adding a dependency outside this list requires an ADR** (Constitution II).
> The 5 baseline ADRs (0001–0005) plus ADR-0006 (API surface extensions) are
> Sprint S0 deliverables.

---

## Project Structure (eight production projects + five test projects)

```text
src/
├── B2.Domain/              # Entities, Value Objects, Domain Services. Zero outward refs.
├── B2.Application/         # Use cases, DTOs, FluentValidation, repository interfaces.
├── B2.Infrastructure/      # EF Core, repositories, Quartz jobs, Litestream wrapper, ClosedXML, BCrypt, JWT.
├── B2.Server.Api/          # Minimal API endpoint groups, SignalR Hub, JWT/Audit/Idempotency middleware, ProblemDetails handler.
├── B2.Server.Service/      # Windows Service host (worker template) hosting B2.Server.Api in-process.
├── B2.Shared.Contracts/    # DTOs shared between Server and Desktop. No logic.
├── B2.Desktop/             # .NET MAUI 8, Windows target only. Views/, ViewModels/, Services/, Resources/Styles/, Resources/Strings/AppResources.vi.resx, Platforms/Windows/.
└── B2.Installer/           # Velopack config or Inno Setup script.
tests/
├── B2.Domain.Tests/        # ≥ 90 % line coverage.
├── B2.Application.Tests/   # ≥ 80 %.
├── B2.Server.Api.Tests/    # WebApplicationFactory + in-memory SQLite shared cache. Every endpoint, every Gherkin AC.
├── B2.Desktop.Tests/       # ViewModel unit + accessibility AC.
└── B2.Performance.Tests/   # NBomber scenarios. Nightly on the spec VM.
```

**Dependency rules** (Constitution V — non-negotiable):

```text
Domain        ← (nothing)
Application   ← Domain
Infrastructure ← Application, Domain
Server.Api    ← Application, Infrastructure, Shared.Contracts
Server.Service ← Server.Api
Desktop       ← Shared.Contracts (only)
Tests         ← the project under test
```

No project may reference `B2.Desktop`. `B2.Domain` MUST NOT reference any package
outside the .NET BCL — not even `Microsoft.Extensions.*`. ArchUnit assertion in
`B2.Server.Api.Tests` enforces these rules.

---

## Commands

```bash
dotnet workload install maui                         # one-time
dotnet restore
dotnet format --verify-no-changes                    # CI gate
dotnet build  -c Release /p:TreatWarningsAsErrors=true  # CI gate
dotnet test   -c Release --collect:"XPlat Code Coverage"
dotnet ef migrations add <Name> -p src/B2.Infrastructure -s src/B2.Server.Service
dotnet ef database update     -p src/B2.Infrastructure -s src/B2.Server.Service
```

Per-feature work: `feat/F-XX-short-name` branch, Conventional Commits, squash-merge.
PR title MUST cite the Feature ID(s).

---

## Pre-push gates (run locally before `git push`)

Catches what we already know will block CI, without burning a Windows runner:

```bash
dotnet format B2.NoMaui.slnf --verify-no-changes --no-restore           # CHARSET (BOM) + WHITESPACE
dotnet build  B2.NoMaui.slnf -c Release /p:TreatWarningsAsErrors=true   # warnings-as-errors on libraries + API
dotnet test   B2.NoMaui.slnf -c Release --no-build                      # endpoint + domain tests
```

> **macOS analyzer trap (Constitution III + VI)**: `B2.Desktop` targets
> `net8.0-windows10.0.19041.0` and requires the `maui-tizen` workload that
> does **not** install on macOS, so its `dotnet build` cannot run locally and
> its analyzers (CA1305, CA1863, CA1001, …) only fire on the Windows CI
> runner. When editing `src/B2.Desktop/**/*.cs`, follow these rules so the
> trip to CI is not wasted:
>
> - **CA1305 + CA1863** — never call `string.Format(literal, args)` directly.
>   Cache a `static readonly CompositeFormat` and pass `CultureInfo.CurrentCulture`:
>
>   ```csharp
>   private static readonly CompositeFormat ToastFormat =
>       CompositeFormat.Parse(AppResources.Exit_Toast_ExitMarked_Format);
>   // ...
>   var toast = string.Format(CultureInfo.CurrentCulture, ToastFormat, plate);
>   ```
>
> - **CA1001** — any class owning a `CancellationTokenSource` (or other
>   `IDisposable`) field MUST implement `IDisposable` and dispose the field.
>
> - **CHARSET** — new `.cs` / `.xaml.cs` files MUST be UTF-8 **without BOM**
>   (`.editorconfig` `charset = utf-8`). Configure your editor to omit the
>   BOM, or strip it after creation:
>   `perl -i -pe 'BEGIN{$f=1} s/^\xef\xbb\xbf// if $f; $f=0' <file>`.

> **Windows CI workflow convention** (`.github/workflows/ci.yml`): every
> dotnet step in the `build-and-test` job is pinned to `shell: bash` after
> `setup-dotnet@v5` was observed losing PATH propagation on the default
> pwsh shell three times in a row. Two follow-up rules for editing the
> workflow:
>
> - **MSBuild args use `-p:` (dash), never `/p:` (slash)** — Git Bash's
>   MSYS2 layer would otherwise translate the leading `/` and you get
>   `MSB1008: Only one project can be specified`.
> - **New dotnet steps in the Windows job MUST keep `shell: bash`** —
>   dropping it re-opens the PATH flake.
>
> The local pre-push gates above stay on macOS / Linux bash where `/p:`
> works fine; the Windows-specific rules only apply to CI yaml.

---

## Code Style (Constitution III — non-negotiable)

- `<Nullable>enable</Nullable>` and `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`
  in every `.csproj` (set once via `Directory.Build.props`).
- `<LangVersion>latest</LangVersion>`.
- `dotnet format` MUST pass in CI; `.editorconfig` is the source of truth.
- I/O is `async` by default. `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` are
  forbidden outside startup composition.
- `record` for DTOs and value objects; `class` for entities.
- One public type per file; files SHOULD stay under 400 lines (hard cap 800),
  functions under 50 lines, nesting under 4 levels.

---

## Vietnamese-First UX & Accessibility (Constitution VI + VIII)

- **All** user-facing strings live in
  `B2.Desktop/Resources/Strings/AppResources.vi.resx`. Hard-coded Vietnamese or
  English literals in XAML, code-behind, ViewModels, validators, exception messages
  surfaced to UI, or Excel templates are **forbidden**. Vietnamese copy is taken
  verbatim from `specs/05-ui-wireframes.md`.
- **Design tokens** (colors `Color.*`, typography `Font.*`, spacing `Spacing.*`)
  declared once as `StaticResource` in `Resources/Styles/Colors.xaml` and
  `Resources/Styles/Typography.xaml`. Hard-coded hex / sizes outside those two
  files are forbidden.
- Every list view implements four states per `specs/05-ui-wireframes.md § 8`:
  Loading (skeleton), Empty (icon + action), Error (red banner + retry +
  correlationId), Offline (yellow banner + last-success timestamp).
- Status indicators combine **icon + color + text label simultaneously** (never
  color alone — WCAG 1.4.1).
- Toast and Modal are reusable services in `B2.Desktop/Services/`.
- Tab order is enforced by `TabIndex`; keyboard accelerators registered in
  `Platforms/Windows/`.

---

## Security & Auditability (Constitution VII)

- Every write endpoint is guarded by an ASP.NET Core authorization policy
  (`AdminOnly`, `OperatorOrAbove`, `SupervisorOrAbove`). The full role × endpoint
  matrix is `spec.md` FR-027 — single source of truth for both API authorization
  AND wireframe sidebar visibility.
- `AuditMiddleware` writes one immutable `AuditLog` row per successful write
  (FR-003). Append-only — no UPDATE / DELETE endpoint exists.
- BCrypt cost 10; password ≥ 10 chars, 3 of 4 char classes; default admin password
  rotation forced on first login.
- JWT RS256, signing key sealed with Windows DPAPI on disk.
- Backups encrypted with AES-256; key-wrap construction lets either the
  install-time password OR the printable recovery code (FR-026) decrypt.
- `gitleaks` runs in CI; secrets in code or repo block merge.

---

## Tri-state connectivity (`FR-028`)

`B2.Desktop` exposes a `ConnectivityState` service in `Services/`:

| State | Meaning | Writes? | Live updates |
| --- | --- | --- | --- |
| 🟢 Online | SignalR connected | ✅ Allowed | SignalR push (≤ 1 s) |
| 🟡 Đang thử lại | Polling fallback (HTTP up, SignalR down) | ✅ Allowed | Polling delta every 3 s |
| 🔴 Mất kết nối | Server unreachable on both transports | ❌ Refused (FR-011 scoped here) | Stalled |

Yellow / red banners stay persistent in their respective states (no toast spam).

---

## Recent Changes

- **2026-05-04** — Sprint S3 **Round 3e** merged via PR #16: list-view
  skeletons (`Views/Common/SkeletonRows`) + correlationId on every
  error banner (`Views/Common/ErrorCodeBanner`) across Ledger / Kvgs /
  Users / MasterData / Exit. 5 VMs now take `ISessionCorrelationService`
  and expose it as a `Correlation` property; pages bind via
  `BindingContext="{Binding Correlation}"`. Closes wireframe § 8 list-
  view-states promise.
- **2026-05-04** — Sprint S3 **Round 3d** merged via PR #15 (`14c39b2`):
  ledger drawer chi tiết (420 px right-side panel; double-tap row →
  `GET /api/declarations/{id}`) and the in-list Windows accelerators
  `F5` (refresh), `/` (focus search), `Esc` (close drawer / clear
  search). Advances T-S3-10 by closing the wireframe § 6.2 drawer +
  in-list shortcuts items; child-vehicle expand/collapse, context
  menu, and SignalR push animations remain.
- **2026-05-04** — **CI workflow fix** merged via PR #13 (`2068373`):
  pinned every Windows job's dotnet step to `shell: bash` to defeat the
  recurring `setup-dotnet@v5` PATH flake on `windows-2022` pwsh
  ("Unable to locate dotnet CLI" hit three times across PR #9 first
  run + R3b post-merge twice). Switched MSBuild args from `/p:` to
  `-p:` to avoid Git Bash MSYS2 path translation (MSB1008
  "Only one project can be specified" otherwise).
- **2026-05-04** — Sprint S3 **Round 3c** merged via PR #12 (`d0d47de`):
  KVGS row fade-out (300 ms / 12-frame VM tween via a new bindable
  `Opacity` on `LedgerRowViewModel` — promoted to `ObservableObject`)
  + per-session "Đã xác nhận hôm nay: X tờ khai" counter; LedgerVM
  `OnPageChanged` notifies `DisplayRangeText` so pagination summary
  doesn't lag a refresh. Closes T-S3-04.
- **2026-05-04** — Sprint S3 **Round 3b** merged via PR #11 (`90d6b00`):
  functional `Cỡ chữ` dropdown (Bình thường / Lớn / Rất lớn) backed by
  a new `IUserSettingsService` (MAUI Preferences-backed). App.xaml.cs
  mutates `Application.Current.Resources` `FontSize*` tokens at
  startup (offsets +0 / +4 / +8 per wireframe § 3); status bar Picker
  on change saves the preference + shows a Warning toast
  "Đăng nhập lại để áp dụng".
- **2026-05-04** — Sprint S3 **Round 3a** merged via PR #9 (`577d388`):
  wireframe § 5 vertical sidebar (220 px) replaces the temporary tab strip;
  Windows `F1` + `Alt+1..Alt+8` accelerators wired via
  `Microsoft.UI.Xaml.Input.KeyboardAccelerator`; connectivity badge becomes
  tappable and opens a state-specific info popup; `ColorSidebarBg` and
  `ColorBorderFocus` tokens added.
- **2026-05-04** — Sprint S3 **Round 2** merged via PR #8 (`d41c607`):
  modal popups (`ConfirmPopup`, `InfoPopup`) backed by
  `CommunityToolkit.Maui.Views.Popup` with focus trap + Esc / backdrop
  dismissal per wireframe § 7; toast switched to Snackbar with
  severity-based timing (4 s success, 8 s warning, manual dismiss for error);
  Entry / Exit / KVGS toast call sites tagged `ToastSeverity.Success`.
- **2026-05-04** — Sprint S3 **Round 1** merged via PR #7 (`797a987`):
  FR-028 Serilog `Information` event on every `ConnectivityService.Mode`
  transition; F-10 Scenario 3 acceptance test (writes-during-🟡-polling)
  added to `ListDeclarationsEndpointTests.cs` (F-10 trait count: 5 → 6);
  two hard-coded `FontSize` literals migrated to design tokens
  (`Typography.xaml` now matches wireframe § 3 fully).
- **2026-05-04** — Sprint S3 **partial** merged via PR #6 (`e926982`):
  KVGS + ledger backend (`Declaration.ConfirmKvgs`,
  `ListDeclarationsQueryHandler`, `EventsHub`,
  `SignalRLedgerDispatcher`); desktop chrome (LedgerPage, KvgsPage,
  `MainShellPage : ContentPage` + Grid, `ISessionCorrelationService`,
  `ISessionContext`, `IAppNavigator`, `ConnectivityService` FR-028
  tri-state); `IToastService` / `IModalService` services + Entry / Exit /
  MasterData VM migration. T-S3-01..03, T-S3-05..08, T-S3-11, T-S3-12
  marked complete; T-S3-04, T-S3-09, T-S3-10 partial (Round 3b/c/d/e
  backlog).
- **2026-05-04** — Sprint S2 merged via PR #5 (`de24ec5`): Entry, Exit, Master
  Data backend + MAUI desktop pages. All 22 S2 tasks (`T-S2-01..T-S2-22`)
  complete. Post-mortem captured as the new `Pre-push gates` section above
  (macOS cannot install the MAUI workload, so `B2.Desktop` analyzers only run
  on the Windows CI runner).
- **2026-05-04** — Sprint S1 merged via PR #4 (`3407a5c`): F-01 login, F-02
  user admin, FR-017 force-rotate-password (`RequirePasswordChangeMiddleware`
  + `ChangePasswordPage`), seed admin bootstrap.
- **2026-05-03** — Sprint S0 merged via PR #2 (`fab8c55`): solution scaffold,
  Constitution III gates wired (`Directory.Build.props`, `.editorconfig`,
  `B2.NoMaui.slnf`), CI workflow with `windows-2022` + `ubuntu-latest` jobs,
  ADRs 0001–0006.
- **2026-05-03** — `001-b2-clearance`: Sprint S0 plan committed
  (`specs/001-b2-clearance/plan.md`). Tech stack + repo layout locked. Six ADRs
  scheduled for S0 (0001–0006).
- **2026-05-03** — Constitution amended to v1.1.0 (added Principle VIII — UI
  Wireframes Binding).
- **2026-05-03** — Spec re-clarified for UI gaps: 4 `[UI]` clarifications added
  FR-027 (role × endpoint matrix), FR-028 (tri-state connectivity), promoted F-17
  to P1 with simple-v1 scope, and codified the Excel-export "Đã bật" badges.

<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
