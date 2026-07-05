# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

`chima-clearance` (project **B2**) replaces the shared-Excel "Sổ B2" vehicle
clearance log at Customs Gate B2 (Cửa khẩu Chi Ma). It is a **LAN-only modular
monolith**: a Windows Service hosts an ASP.NET Core 8 Minimal API + SignalR hub
in-process, consumed by .NET MAUI 8 Windows desktop clients, over SQLite + WAL
with Litestream continuous backup. All user-facing copy is Vietnamese.

## This is a spec-driven repository — read the specs first

The `specs/` tree and the Constitution are the source of truth; the code
implements them. **Before writing non-trivial code**, read the relevant spec —
do not infer requirements from existing code alone:

1. `specs/01-overview.md` § 6 — **locked tech stack (IMMUTABLE)** and § 2 constraints
2. `specs/02-architecture.md` — § 4 ERD, § 6 API surface, § 7/§ 9 state machines & backup flow
3. `specs/03-functional-spec.md` — Gherkin acceptance criteria + `FR-*`/`INV-*`/`F-*` IDs
4. `specs/04-implementation-plan.md` — § 1 Definition of Done, dependency rules
5. `specs/05-ui-wireframes.md` — wireframes § 6, design tokens § 2-3 (binding for any UI change)
6. `.specify/memory/constitution.md` (v1.1.0) — **highest authority; non-negotiable**
7. `docs/adr/ADR-0001..0006` — locked architectural decisions

When Spec Kit artifacts (`specs/001-b2-clearance/{spec,plan,tasks}.md`) conflict
with the hand-written `specs/0X-*.md`, follow the Spec Kit artifacts.

**Adding any dependency outside `specs/01-overview.md § 6` requires a new ADR**
(Constitution II). Do not propose swapping the stack (no WPF/Avalonia/Blazor,
no PostgreSQL/SQL Server, no MediatR/Dapper/MassTransit).

Sprint/feature status lives in `README.md` (sprint table) and `CHANGELOG.md` —
consult those rather than tracking status here.

## Commands

There are two solution surfaces: **`B2.sln`** (everything, incl. MAUI Desktop —
Windows only) and **`B2.NoMaui.slnf`** (a filter excluding `B2.Desktop`, builds
on macOS/Linux). Prefer the filter for local work off Windows.

```bash
# One-time
dotnet workload install maui        # Windows only; do NOT run on macOS/Linux
dotnet restore B2.NoMaui.slnf

# Build / test / format (cross-platform subset)
dotnet build B2.NoMaui.slnf -c Release /p:TreatWarningsAsErrors=true
dotnet test  B2.NoMaui.slnf -c Release
dotnet format B2.NoMaui.slnf --verify-no-changes   # CI gate (.editorconfig is source of truth)

# Full solution (Windows + MAUI workload only)
dotnet build B2.sln -c Release /p:TreatWarningsAsErrors=true

# Run a single test (xUnit --filter)
dotnet test B2.NoMaui.slnf --filter "FullyQualifiedName~ListDeclarationsEndpointTests"
dotnet test B2.NoMaui.slnf --filter "DisplayName~officer name"

# Run the dev server in console mode (hosts the API + SignalR in-process)
dotnet run --project src/B2.Server.Service -- --console   # https://localhost:5001

# EF Core migrations (Infrastructure = model project, Server.Service = startup)
dotnet ef migrations add <Name> -p src/B2.Infrastructure -s src/B2.Server.Service
dotnet ef database update     -p src/B2.Infrastructure -s src/B2.Server.Service
```

### Pre-push gates (run locally before `git push`)

Catches what will block CI without burning a Windows runner:

```bash
dotnet format B2.NoMaui.slnf --verify-no-changes --no-restore          # CHARSET (BOM) + WHITESPACE
dotnet build  B2.NoMaui.slnf -c Release /p:TreatWarningsAsErrors=true   # warnings-as-errors
dotnet test   B2.NoMaui.slnf -c Release --no-build                      # endpoint + domain tests
```

### macOS analyzer trap (editing `src/B2.Desktop/**`)

`B2.Desktop` targets `net8.0-windows10.0.19041.0` and needs a workload that does
**not** install on macOS, so its `dotnet build` cannot run locally and its
analyzers only fire on the Windows CI runner. When editing Desktop `.cs`:

- **CA1305 + CA1863** — never `string.Format(literal, args)` directly. Cache a
  `static readonly CompositeFormat` and pass `CultureInfo.CurrentCulture`.
- **CA1001** — a class owning a `CancellationTokenSource` (or any `IDisposable`)
  field MUST implement `IDisposable` and dispose it.
- **CHARSET** — new `.cs`/`.xaml.cs` files MUST be UTF-8 **without BOM**. Strip a
  stray BOM: `perl -i -pe 'BEGIN{$f=1} s/^\xef\xbb\xbf// if $f; $f=0' <file>`.

### CI workflow conventions (`.github/workflows/ci.yml`)

Every dotnet step in the Windows job is pinned to `shell: bash` (defeats a
recurring `setup-dotnet@v5` PATH flake on pwsh). Two rules when editing the yaml:

- **MSBuild args use `-p:` (dash), never `/p:`** — Git Bash's MSYS2 layer
  translates the leading `/` → `MSB1008`. (Local macOS/Linux gates keep `/p:`.)
- New dotnet steps in the Windows job MUST keep `shell: bash`.

## Architecture

### Layered dependency rules (Constitution V — enforced by ArchUnit)

```text
Domain        ← (nothing outside the .NET BCL — not even Microsoft.Extensions.*)
Application   ← Domain
Infrastructure ← Application, Domain
Server.Api    ← Application, Infrastructure, Shared.Contracts
Server.Service ← Server.Api
Desktop       ← Shared.Contracts (only)
```

No project may reference `B2.Desktop`.
`tests/B2.Server.Api.Tests/Architecture/LayerRulesTests.cs` (ArchUnitNET) fails
the build if these are violated. `src/` holds 8 production projects, `tests/` 5.

### Composition + request pipeline

The single entry point is `src/B2.Server.Service/Program.cs`, which delegates
almost everything to **`B2.Server.Api/Hosting/B2ApiHost.cs`** (the composition
root):

- `Program.cs` wires Serilog with a **shared in-memory log buffer**
  (`InMemoryLogStore`/`InMemoryLogReader`) that the F-18 `GET /api/admin/logs`
  endpoint reads back, registers Quartz (`AddB2QuartzStartup`) and the Litestream
  supervisor (`AddB2Litestream`), and calls `UseWindowsService()` on Windows.
- `B2ApiHost.InitializeDatabaseAsync` runs **before** the app serves traffic: it
  applies a pending restore snapshot (`b2.db.restore-pending`) if present, then
  EF migrations.
- Middleware order (`B2ApiHost.MapPipeline`) is deliberate:
  `CorrelationId → GlobalException → Authentication → RequirePasswordChange →
  Authorization → RestoreWriteBlock → AuditPersistence → Idempotency`, then the
  endpoint groups.

Endpoints are grouped as `Map*Endpoints()` extension methods in
`B2.Server.Api/Endpoints/` (Auth, Users, MasterData, Declaration, Vehicles,
DailyHeaders, Reports, Admin). Every **write** endpoint is guarded by an
authorization policy (`AdminOnly`, `OperatorOrAbove`, `SupervisorOrAbove`); the
full role × endpoint matrix is `spec.md` **FR-027**, and it is the single source
of truth for *both* API authz and the Desktop sidebar visibility.

### Key infrastructure subsystems (`src/B2.Infrastructure/`)

`Persistence/` (EF Core + repositories), `Jobs/` (Quartz: daily-header
`0 1 0 * * ?` + daily-summary `0 0 23 * * ?` in `Asia/Ho_Chi_Minh`), `Excel/`
(ClosedXML template export against embedded `Excel/Templates/`), `Backup/`
(AES-256-GCM data key wrapped twice via PBKDF2 under install password **and**
FR-026 recovery code; DPAPI-sealed secrets; Litestream supervisor), `Auth/`
(JWT RS256 + BCrypt), `Archive/`, `Clock/`.

### Auditability & write invariants

- `AuditPersistenceMiddleware` writes one immutable `AuditLog` row per successful
  write (FR-003). The audit log is **append-only** — there is no UPDATE/DELETE
  endpoint anywhere in the API.
- `RestoreWriteBlockMiddleware` returns `503 RESTORE_PENDING` for mutating verbs
  while a restore is staged, so writes cannot be silently lost by the
  apply-on-startup swap; `ApplyPending` uses `File.Replace` (see
  `.cursor/automation-memory/bug-finding.md`).
- POST `/api/declarations` and POST `/api/vehicles/{id}/exit` are idempotent via
  an `Idempotency-Key` header (Desktop sends a fresh `Guid` per submit).

### Realtime + tri-state connectivity (FR-028)

The SignalR hub is at `/hubs/events`. Desktop's `ConnectivityService` exposes
three states — 🟢 Online (SignalR push ≤ 1 s), 🟡 Đang thử lại (HTTP polling
every 3 s when SignalR is down), 🔴 Mất kết nối (both transports down; writes
refused). Yellow/red banners are persistent (no toast spam).

## Code style (Constitution III — non-negotiable)

Set once via `Directory.Build.props`: `<Nullable>enable</Nullable>`,
`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, `<LangVersion>latest</LangVersion>`.

- I/O is `async` by default. `.Result`, `.Wait()`, `.GetAwaiter().GetResult()`
  are forbidden outside startup composition.
- `record` for DTOs/value objects; `class` for entities. One public type per
  file. Files SHOULD stay < 400 lines (hard cap 800), functions < 50, nesting < 4.
- Conventional Commits citing the feature: `feat(F-05): …`, `fix(F-15): …`,
  `chore(s0): …`. One feature per branch (`feat/F-XX-short-name`), squash-merge.
  PR title MUST cite the Feature ID(s).

## Vietnamese-first UX & accessibility (Constitution VI + VIII)

- **All** user-facing strings live in
  `B2.Desktop/Resources/Strings/AppResources.vi.resx`. Hard-coded Vietnamese or
  English literals in XAML, code-behind, ViewModels, validators, UI-surfaced
  exceptions, or Excel templates are **forbidden**. Copy comes verbatim from
  `specs/05-ui-wireframes.md`. (`AppResources.Designer.cs` accessors are
  hand-maintained — a missing accessor is a Windows-only build break.)
- **Design tokens** (`Color.*`, `Font.*`, `Spacing.*`) are declared once as
  `StaticResource` in `Resources/Styles/Colors.xaml` and `Typography.xaml`.
  Hard-coded hex/sizes outside those two files are forbidden.
- Every list view implements four states (wireframes § 8): Loading (skeleton),
  Empty (icon + action), Error (red banner + retry + correlationId), Offline
  (yellow banner + last-success timestamp).
- Status indicators combine **icon + color + text simultaneously** (never color
  alone — WCAG 1.4.1). Toast and Modal are reusable services in `B2.Desktop/Services/`.

## Testing (Constitution + `specs/04-implementation-plan.md`)

`B2.Server.Api.Tests` uses `WebApplicationFactory<Program>` + in-memory
`Microsoft.Data.Sqlite` (shared cache) and covers every endpoint / every Gherkin
AC. Coverage targets: Domain ≥ 90 %, Application ≥ 80 %. `B2.Performance.Tests`
holds NBomber scenarios (nightly on the spec VM). Prefer testing against the
in-memory factory over mocking the DB.
