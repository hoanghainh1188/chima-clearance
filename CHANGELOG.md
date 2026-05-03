# Changelog

All notable changes to this project are documented here. Format follows
[Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/) and the project
uses [Conventional Commits](https://www.conventionalcommits.org/).

## [Unreleased]

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

[Unreleased]: https://example.invalid/chima-clearance/compare/v0.0.1...HEAD
[0.0.1]: https://example.invalid/chima-clearance/releases/tag/v0.0.1
