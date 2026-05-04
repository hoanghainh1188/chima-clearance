# Changelog

All notable changes to this project are documented here. Format follows
[Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/) and the project
uses [Conventional Commits](https://www.conventionalcommits.org/).

## [Unreleased]

### Added

- `CLAUDE.md` § "Pre-push gates" — locally runnable subset of the CI gates
  (`dotnet format` + `dotnet build` + `dotnet test` against
  `B2.NoMaui.slnf`) plus a "macOS analyzer trap" callout documenting the
  `CompositeFormat` / `CultureInfo.CurrentCulture` / `IDisposable` patterns
  required by the `B2.Desktop` analyzers (CA1305 / CA1863 / CA1001) that only
  run on the Windows CI runner.
- `specs/001-b2-clearance/quickstart.md` § 7 — two new troubleshooting rows
  (UTF-8 BOM `error CHARSET` and the macOS-vs-CI Desktop analyzer divergence)
  with concrete remediation steps.

### Fixed

- **Sprint S3 (backend slice)**: `GET /api/declarations` reads query parameters
  via `HttpRequest.Query` (avoids minimal-API binding returning HTTP 400 with an
  empty body). `DeclarationRepository.ListDeclarationsAsync` avoids SQLite EF
  limitations on `DateTimeOffset` in `ORDER BY` and on `since` / vehicle time
  predicates (in-memory sort + `since` filter, plate filter via `IN` subquery).
  SignalR integration test connects through `WebApplicationFactory.Server` with
  `HttpMessageHandlerFactory` so negotiation uses the in-memory test server
  instead of `localhost:80`.

### Changed

- `CLAUDE.md` "Active phase" updated to **Sprint S3** (S0 + S1 + S2 merged
  to `main`). "Recent Changes" log extended with PR #4 (S1) and PR #5 (S2)
  merge entries.

## [0.2.0] — 2026-05-04 — Sprint S2 (Entry & Exit happy path)

Merged via PR #5 (`de24ec5`). All 22 sprint tasks (`T-S2-01..T-S2-22`)
complete; CI green on `gitleaks`, `no-maui-quick-check`, `build-and-test`.

### Added

- **F-03 / F-04 Master Data**:
  - `Processor` and `Yard` aggregate roots + EF Core configurations and
    repositories in `src/B2.Domain/MasterData/` and
    `src/B2.Infrastructure/MasterData/`.
  - `/api/master/processors` and `/api/master/yards` GET/POST/PUT/DELETE
    endpoints (`AdminOnly` policy) wired in `src/B2.Server.Api/Endpoints/`.
  - `Admin/MasterDataPage.xaml` + `Admin/MasterDataViewModel.cs` per
    wireframe, Vietnamese strings in `AppResources.vi.resx`.
- **F-05 Create Declaration**:
  - `Declaration` aggregate (state machine `Draft → Active → AllExited →
    KvgsConfirmed`) + `DeclarationRepository` with `RowVersion` concurrency.
  - `CreateDeclarationCommandHandler` with master-data resolution at write
    time + `Idempotency-Key` middleware.
  - `POST /api/declarations` endpoint (`OperatorOrAbove` policy).
  - `EntryPage.xaml` + `EntryViewModel.cs` with verbatim Vietnamese copy
    from wireframe § 6.3.
- **F-06 Add Vehicle**:
  - `AddVehicleCommandHandler` (with INV-2 child-plate-uniqueness check at
    write time) + `POST /api/declarations/{id}/vehicles` endpoint.
  - Read-only vehicle list panel on `EntryPage`.
- **F-07 Suffix Search**:
  - `SearchVehiclesBySuffixQueryHandler` + `GET /api/vehicles/search`
    endpoint (3-char minimum, 500 ms debounce on the client).
- **F-08 Mark Exit**:
  - `MarkVehicleExitCommandHandler` + `UnExitVehicleCommandHandler` (FR-024
    cross-client exit race resolution; 409 returns `AlreadyExitedProblem`
    body with the winner's `ExitTime` and `ExitedByUserId`).
  - `POST /api/vehicles/{id}/exit` (`OperatorOrAbove`) and
    `POST /api/vehicles/{id}/un-exit` (`SupervisorOrAbove` per FR-022).
  - `ExitPage.xaml` + `ExitViewModel.cs` per wireframe § 6.4 (height-56
    search box, 500 ms debounce, Enter-Enter quick-mark when single
    result, "Đã đánh dấu hôm nay: N xe" widget).
- `MainShell` + `IMainShellFactory` for role-aware navigation post-login.
- `SupervisorHomePage` placeholder (filled in S3).
- 18 endpoint integration tests covering the four features
  (`tests/B2.Server.Api.Tests/Declarations/` +
  `tests/B2.Server.Api.Tests/Vehicles/`), all tagged
  `[Trait("Feature","F-XX")]`.

### Fixed (post-merge clean-up driven by Windows CI)

- `dotnet format B2.sln` reported `error WHITESPACE` and `error CHARSET` on
  16 newly added `.cs` files (Application + Shared.Contracts) and 13
  `B2.Desktop` files because they were saved as UTF-8 with BOM. Stripped
  BOM and re-ran `dotnet format`. The lesson is now codified in
  `CLAUDE.md` § "Pre-push gates" and `quickstart.md` § 7.
- `B2.Desktop` build (Release, `TreatWarningsAsErrors=true`) failed on
  Windows CI with three analyzer rules that do not surface on macOS
  because the `maui-tizen` workload is unavailable there:
  - **CA1305 / CA1863** in `EntryViewModel.SubmitAsync`,
    `ExitViewModel.SearchAsync`, `ExitViewModel.MarkExitAsync` — switched
    to cached `static readonly CompositeFormat` and `string.Format` with
    `CultureInfo.CurrentCulture`.
  - **CA1001** on `ExitViewModel` — implemented `IDisposable` to dispose
    the debounce `CancellationTokenSource` it owns.

### Quality gates met

- ✅ `dotnet format B2.sln --verify-no-changes` on Windows runner — clean.
- ✅ `dotnet build B2.sln -c Release /p:TreatWarningsAsErrors=true` on
  Windows runner — green (full MAUI desktop included).
- ✅ `dotnet test B2.sln -c Release` — 18 endpoint tests pass / 1 skipped
  (NBomber perf placeholder, Windows-only) / 0 failed.
- ✅ `gitleaks` — no secrets detected.

## [0.1.0] — 2026-05-04 — Sprint S1 (Auth + Users + Master Data preview)

Merged via PR #4 (`3407a5c`). Covers F-01 login, F-02 user admin, the
seeded admin bootstrap, and FR-017 force-rotate-password.

### Added

- `RequirePasswordChangeMiddleware` — FR-017 enforcement; authenticated
  requests with `must_change_password=true` claim are rejected `403` /
  `PASSWORD_CHANGE_REQUIRED` except `POST /api/auth/change-password` and
  `POST /api/auth/logout` (`src/B2.Server.Api/Middleware/`).
- `ChangePasswordPage.xaml` + `ChangePasswordViewModel.cs` for forced
  first-login rotation flow on Desktop, wired into `AppShell` route
  `auth/change-password` and `MauiProgram` DI. Vietnamese strings in
  `AppResources.vi.resx`.
- `tests/B2.Server.Api.Tests/Auth/ChangePasswordEndpointTests.cs` — four
  scenarios: blocked first-login, successful rotation, wrong current
  password, weak new password.

### Changed

- `LoginViewModel` redirects to `auth/change-password` when
  `LoginResponse.MustChangePassword` is true.
- `LoginAsAdminAsync` test helper now bootstraps the seeded admin
  (login → change-password → re-login) so existing integration tests keep
  green under the new middleware.
- Marked tasks `T-S1-22` and `T-S1-23` complete in `tasks.md`.

## [0.0.1] — 2026-05-03 — Sprint S0 (Bootstrap)

### Added

- Solution scaffold per `specs/04-implementation-plan.md § 3.5`: 8 production
  projects (`B2.Domain`, `B2.Application`, `B2.Infrastructure`, `B2.Server.Api`,
  `B2.Server.Service`, `B2.Shared.Contracts`, `B2.Desktop`, `B2.Installer`) and
  5 test projects (`*.Tests` + `B2.Performance.Tests`).
- `B2.NoMaui.slnf` solution filter for cross-platform builds without the MAUI
  workload.
- `Directory.Build.props` enforcing Constitution III: `Nullable=enable`,
  `TreatWarningsAsErrors=true`, `LangVersion=latest`, `AnalysisMode=Recommended`.
- `.editorconfig` aligned with Constitution III code-style rules.
- `global.json` pinning the .NET 8 SDK (alongside any newer SDK on dev
  machines).
- `.gitignore` with .NET / MAUI / IDE patterns.
- Project-reference graph wired per Constitution V (Domain ← Application ←
  Infrastructure / Server.Api; Desktop ↔ Shared.Contracts only).
- Empty `AssemblyMarker` types in each production project for test assembly
  discovery.
- `tests/B2.Server.Api.Tests/Architecture/LayerRulesTests.cs` — five ArchUnitNET
  assertions enforcing Constitution V at runtime.
- Minimal `Program.cs` for `B2.Server.Api` exposing `GET /api/admin/health`
  (anonymous) — replaces the WeatherForecast template.
- `.github/workflows/ci.yml` — `windows-2022` job (full solution + MAUI),
  `ubuntu-latest` "no-MAUI quick check" job, and a `gitleaks` job.
- ADR-0001 — Modular Monolith over Microservices.
- ADR-0002 — SQLite as the primary persistence store.
- ADR-0003 — .NET MAUI 8 as the desktop UI framework.
- ADR-0004 — JWT (RS256) with a local user store.
- ADR-0005 — Litestream for continuous backup.
- ADR-0006 — API surface extensions for FR-022 / FR-025 / FR-026 / FR-027 /
  F-12 / F-17 + ArchUnitNET dependency (resolves analyse-pass W2 + W3).
- `contracts/openapi.yaml` extended with `POST /api/daily-headers/{date}/recompute`
  (per ADR-0006 + analyse-pass W3).
- `README.md` with build instructions, repo layout, sprint status.

### Changed

- N/A (first release).

### Removed

- WeatherForecast template stub from `B2.Server.Api`.
- `Class1.cs` / `UnitTest1.cs` placeholders from every templated project.
- `B2.Server.Api.http` template artifact.

### Quality gates met

- ✅ `dotnet build B2.NoMaui.slnf -c Release /p:TreatWarningsAsErrors=true` — green.
- ✅ `dotnet test B2.NoMaui.slnf -c Release` — 5/5 ArchUnit tests pass; other
  test projects empty by design at S0.
- ✅ `dotnet format B2.NoMaui.slnf --verify-no-changes` — clean.
- ⏭ MAUI build deferred to Windows runner with `dotnet workload install maui`
  (CI handles this in `ci.yml`'s `build-and-test` job).

### Known follow-ups (carry-over from analyse pass)

- M1..M7 + L1 in `specs/001-b2-clearance/analyze-report.md` are expected to be
  applied during their respective sprints (M2 + M5 + M7 in S1; M3 + M4 + M6 in
  S3..S5). None block this release.

[Unreleased]: https://example.invalid/chima-clearance/compare/v0.2.0...HEAD
[0.2.0]: https://example.invalid/chima-clearance/compare/v0.1.0...v0.2.0
[0.1.0]: https://example.invalid/chima-clearance/compare/v0.0.1...v0.1.0
[0.0.1]: https://example.invalid/chima-clearance/releases/tag/v0.0.1
