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

Active phase: **Sprint S3 (KVGS + ledger + realtime)** — F-09 confirmation +
F-10 always-on ledger with SignalR push. **Done**: S0 (scaffold, ADRs 0001–
0006), S1 (F-01/F-02/F-03/F-04 — auth, users, master data, force-rotate
password), S2 (F-05/F-06/F-07/F-08 — declaration entry, add child vehicle,
suffix search, mark exit). All merged to `main`.

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
