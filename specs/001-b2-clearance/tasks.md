# Tasks — Chima Customs B2

**Feature**: `001-b2-clearance` · **Date**: 2026-05-03 · **Plan**:
[`plan.md`](./plan.md) · **Constitution**:
[v1.1.0](../../.specify/memory/constitution.md)

> **Sprint contract** (from
> [`specs/04-implementation-plan.md § 2`](../04-implementation-plan.md) and
> `docs/prompts.md:228-258` — locked, do not reorder or merge sprints):
>
> | Sprint | Goal | Features |
> | --- | --- | --- |
> | **S0** | Bootstrap | scaffold, CI, installer skeleton, ADRs 0001–0006 |
> | **S1** | Core domain + auth | F-01, F-02, F-03, F-04 |
> | **S2** | Entry & exit happy path | F-05, F-06, F-07, F-08 |
> | **S3** | KVGS + ledger + realtime | F-09, F-10 |
> | **S4** | Background jobs + reports | F-11, F-12, F-14 |
> | **S5** | Export, backup, audit | F-13, F-15, F-16, F-18 |
> | **S6** | Hardening | F-17 (simple v1, P1) + accessibility audit + runbook + UAT |
>
> **Task ID convention**: `T-S{sprint}-{NN}`. Story label is the **Feature ID**
> (`[F-05]`, `[F-08]`, …) for any task that implements a feature; setup,
> foundational, and polish tasks omit the label. `[P]` marks tasks that can run in
> parallel (different files, no incomplete dependencies).
>
> **TDD**: every "Write Gherkin tests for F-XX" task lands **before** any of that
> feature's implementation tasks (directive rule 6). The test name convention is
> `Should_<criterion>` with `[Trait("Feature","F-XX")]` per Constitution IV.

---

## Definition of Done — applied to **every** task

Reproduced once from
[`specs/04-implementation-plan.md § 1`](../04-implementation-plan.md). Each task
below carries a "DoD: §DoD" pointer instead of repeating the ten items.

- [ ] **DoD-1** Code merged behind required-review on `main`.
- [ ] **DoD-2** Domain invariants are unit-tested (xUnit + FluentAssertions).
- [ ] **DoD-3** Application use cases are unit-tested with mocked repositories.
- [ ] **DoD-4** API endpoints have integration tests using
  `WebApplicationFactory<Program>` against in-memory SQLite.
- [ ] **DoD-5** All Gherkin acceptance criteria from
  [`spec.md`](./spec.md) for the feature ID are covered by tests, named
  `Should_<criterion>` and tagged `[Trait("Feature","F-XX")]`.
- [ ] **DoD-6** Vietnamese strings are in
  `src/B2.Desktop/Resources/Strings/AppResources.vi.resx` — no hard-coded
  user-facing text in code.
- [ ] **DoD-7** Logging at correct levels (Information for command success,
  Warning for retried errors, Error for unhandled).
- [ ] **DoD-8** AuditLog entry verified for write commands (integration test
  asserts the row).
- [ ] **DoD-9** Manual smoke on the spec VM (per
  [`04-implementation-plan.md § 8`](../04-implementation-plan.md)) passes for
  features that touch startup, installer, persistence, backup, or service
  lifecycle.
- [ ] **DoD-10** `CHANGELOG.md` entry added.

---

## Acceptance criteria source map

Every test task references the verbatim Gherkin in
[`spec.md`](./spec.md) (which preserves
[`specs/03-functional-spec.md`](../03-functional-spec.md) verbatim per directive
rule 3). The location of each feature's scenarios:

| Feature | `spec.md` section | Cross-feature ACs |
| --- | --- | --- |
| F-01 Login | `### User Story F-01` (lines ~43–82) | `### Cross-Feature Acceptance Criteria`, `### UI Acceptance Criteria` |
| F-02 Users | `### User Story F-02` | same |
| F-03 Processors | `### User Story F-03` | same |
| F-04 Yards | `### User Story F-04` | same |
| F-05 Entry | `### User Story F-05` | same |
| F-06 Add child vehicle | `### User Story F-06` | same |
| F-07 Suffix search | `### User Story F-07` | same |
| F-08 Mark exit | `### User Story F-08` | same |
| F-09 KVGS confirm | `### User Story F-09` | same |
| F-10 Ledger | `### User Story F-10` (incl. new "Writes permitted in 🟡" scenario from UI clarification Q4) | same |
| F-11 Daily header | `### User Story F-11` | same |
| F-12 Daily summary | `### User Story F-12` | same |
| F-13 Excel export | `### User Story F-13` (incl. "Đã bật" badge clarification) | same |
| F-14 Reports | `### User Story F-14` | same |
| F-15 Backup & restore | `### User Story F-15` + FR-026 | same |
| F-16 Audit log viewer | `### User Story F-16` | same |
| F-17 Smart suggestions (simple v1) | `### User Story F-17` (incl. Phase-1 scope box) | same |
| F-18 Health & logs | `### User Story F-18` | same |

The new system-level FRs added during clarify (FR-022 cancellation, FR-024
cross-client exit race, FR-025 officer name, FR-026 recovery code, FR-027 role
matrix, FR-028 tri-state connectivity) have their own task slots below; their
acceptance criteria sit under the closest feature ID + are verified by ArchUnit /
integration sweeps where appropriate.

---

## Sprint overview table

| Sprint | Tasks | Feature IDs | Approx. LOC |
| --- | --- | --- | --- |
| S0 | T-S0-01 .. T-S0-12 (12) | infra | ~600 |
| S1 | T-S1-01 .. T-S1-21 (21) | F-01, F-02, F-03, F-04 | ~3 500 |
| S2 | T-S2-01 .. T-S2-22 (22) | F-05, F-06, F-07, F-08 + FR-019/024 | ~4 500 |
| S3 | T-S3-01 .. T-S3-12 (12) | F-09, F-10 + FR-027/FR-028 | ~2 500 |
| S4 | T-S4-01 .. T-S4-15 (15) | F-11, F-12, F-14 + FR-025 | ~2 800 |
| S5 | T-S5-01 .. T-S5-19 (19) | F-13, F-15, F-16, F-18 + FR-022/026 | ~3 800 |
| S6 | T-S6-01 .. T-S6-12 (12) | F-17 simple v1 + a11y + runbook + UAT | ~1 800 |
| **Total** | **113 tasks** | 18 features + 7 cross-cutting FRs | **~19 500 LOC** |

> Per directive rule 1: every task is PR-sized (≤ 1 dev-day or ≤ 400 LOC diff).

---

# Phase 1 — Sprint S0 (Bootstrap)

**Goal**: solution scaffold, CI, six ADRs, installer skeleton; no business logic.
**Exit gate**: `dotnet build` and `dotnet test` (empty suites) both green;
`dotnet format --verify-no-changes` clean.

- [X] **T-S0-01** Initialise solution per
  [`quickstart.md § 3`](./quickstart.md): create `B2.sln` + 8 production projects
  + 5 test projects via the `dotnet new` commands listed in
  [`04-implementation-plan.md § 3.5`](../04-implementation-plan.md). Files:
  `B2.sln`, `src/B2.Domain/B2.Domain.csproj`, `src/B2.Application/B2.Application.csproj`,
  `src/B2.Infrastructure/B2.Infrastructure.csproj`,
  `src/B2.Server.Api/B2.Server.Api.csproj`,
  `src/B2.Server.Service/B2.Server.Service.csproj`,
  `src/B2.Shared.Contracts/B2.Shared.Contracts.csproj`,
  `src/B2.Desktop/B2.Desktop.csproj`,
  `src/B2.Installer/B2.Installer.csproj`,
  `tests/B2.Domain.Tests/`, `tests/B2.Application.Tests/`,
  `tests/B2.Server.Api.Tests/`, `tests/B2.Desktop.Tests/`,
  `tests/B2.Performance.Tests/`. **Dep**: none. **LOC**: ~50 (project files).
  **DoD**: §DoD-1, DoD-9 partial.
- [X] **T-S0-02** Add `Directory.Build.props` at repo root enforcing
  `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`,
  `<LangVersion>latest</LangVersion>`. Files: `Directory.Build.props`,
  `.editorconfig`. **Dep**: T-S0-01. **LOC**: ~80. **DoD**: §DoD-1.
- [X] **T-S0-03** [P] Wire project references per dependency rules
  (Constitution V; [`plan.md` Project Structure](./plan.md)). Use
  `dotnet add reference`. Files: `*.csproj` (edits only). **Dep**: T-S0-01.
  **LOC**: ~30. **DoD**: §DoD-1.
- [X] **T-S0-04** [P] Add ArchUnitNET dependency-rule test asserting
  Constitution V layers in `tests/B2.Server.Api.Tests/Architecture/LayerRulesTests.cs`.
  Verify: `B2.Domain` references nothing outside the BCL; no project references
  `B2.Desktop`; `B2.Desktop` and `B2.Server.Api` only share types via
  `B2.Shared.Contracts`. **Dep**: T-S0-03. **LOC**: ~80. **DoD**: §DoD-1, DoD-2.
- [X] **T-S0-05** [P] Configure MAUI Windows-only target in `B2.Desktop.csproj`
  (`<TargetFrameworks>net8.0-windows10.0.19041.0</TargetFrameworks>`,
  `<UseMaui>true</UseMaui>`, `<SingleProject>true</SingleProject>`,
  `<WindowsPackageType>None</WindowsPackageType>`). Files:
  `src/B2.Desktop/B2.Desktop.csproj`. **Dep**: T-S0-01. **LOC**: ~30. **DoD**: §DoD-1.
- [X] **T-S0-06** [P] Author `docs/adr/ADR-0001-modular-monolith.md` per the
  template in [`04-implementation-plan.md § 9`](../04-implementation-plan.md);
  rationale from [`01-overview.md § 4`](../01-overview.md). **Dep**: none. **LOC**:
  ~80. **DoD**: §DoD-1.
- [X] **T-S0-07** [P] Author `docs/adr/ADR-0002-sqlite-as-primary-store.md`.
  **Dep**: none. **LOC**: ~60. **DoD**: §DoD-1.
- [X] **T-S0-08** [P] Author `docs/adr/ADR-0003-dotnet-maui-as-desktop-ui.md`.
  **Dep**: none. **LOC**: ~70. **DoD**: §DoD-1.
- [X] **T-S0-09** [P] Author `docs/adr/ADR-0004-jwt-rs256-with-local-user-store.md`.
  **Dep**: none. **LOC**: ~70. **DoD**: §DoD-1.
- [X] **T-S0-10** [P] Author `docs/adr/ADR-0005-litestream-for-continuous-backup.md`.
  **Dep**: none. **LOC**: ~60. **DoD**: §DoD-1.
- [X] **T-S0-11** [P] Author `docs/adr/ADR-0006-api-surface-extensions.md` from
  the draft in [`research.md § 8`](./research.md). Required because
  `contracts/openapi.yaml` introduces endpoints beyond
  `02-architecture.md § 6` (FR-022 / FR-025 / FR-026 / FR-027 / F-17).
  **Dep**: none. **LOC**: ~120. **DoD**: §DoD-1.
- [X] **T-S0-12** Add `.github/workflows/ci.yml` per
  [`04-implementation-plan.md § 7`](../04-implementation-plan.md): `windows-2022`,
  `dotnet workload install maui`, `dotnet format --verify-no-changes`,
  `dotnet build /p:TreatWarningsAsErrors=true`,
  `dotnet test --collect:"XPlat Code Coverage"`, `gitleaks` scan. Files:
  `.github/workflows/ci.yml`, `README.md`, `CHANGELOG.md` (v0.0.1 entry).
  **Dep**: T-S0-01..05. **LOC**: ~120. **DoD**: §DoD-1, DoD-10.

---

# Phase 2 — Sprint S1 (Core domain + auth)

**Goal**: the system can be logged into; Admin can manage users and master data.
**Independent test**: from a freshly installed system the default Admin can
log in (forced password change), create an Operator account, and add one
Processor + one Yard; an Operator-level call to `POST /api/users` returns 403.
**Sprint dependencies**: T-S0-01 .. T-S0-12 complete.

### Cross-cutting infrastructure for S1+

- [x] **T-S1-01** [P] Implement `IClock` (UTC) in `B2.Application/Abstractions/`
  and `SystemClock` in `B2.Infrastructure/Clock/`. Inject everywhere a server
  timestamp is needed (INV-3, FR-004). Unit-test that two consecutive calls are
  monotonic-non-decreasing. **Dep**: T-S0-04. **LOC**: ~40.
- [x] **T-S1-02** [P] Implement Serilog setup in
  `src/B2.Server.Service/Serilog/SerilogStartup.cs` with rolling file sink at
  `C:\ProgramData\B2\logs\b2-yyyymmdd.log` and a bounded in-memory sink (1000
  entries) registered as singleton in DI for the `F-18` log viewer. Files:
  `SerilogStartup.cs`, `InMemoryLogSink.cs`. **Dep**: T-S0-12. **LOC**: ~120.
- [x] **T-S1-03** [P] Implement `ProblemDetails` exception-mapping middleware
  in `src/B2.Server.Api/Middleware/ProblemDetailsMiddleware.cs` per
  [`research.md § 2.6`](./research.md). Map `DomainException → 400/404/409` with
  stable English `code` field; unhandled → 500 + correlationId. **Dep**: T-S0-04.
  **LOC**: ~150.
- [x] **T-S1-04** Implement W3C Trace-Context correlation-ID propagation in
  `src/B2.Server.Api/Middleware/CorrelationIdMiddleware.cs`. Every request gets
  / inherits a correlation id; Serilog enriches every log line with it.
  **Dep**: T-S1-02, T-S1-03. **LOC**: ~80.
- [x] **T-S1-05** Implement `B2DbContext` in
  `src/B2.Infrastructure/Persistence/B2DbContext.cs` + `SqliteStartup`
  applying PRAGMAs from [`data-model.md § 9`](./data-model.md). Files:
  `B2DbContext.cs`, `SqliteStartup.cs`,
  `Persistence/Configurations/` (one per entity placeholder). **Dep**: T-S0-04.
  **LOC**: ~200.
- [x] **T-S1-06** Implement initial EF Core migration `0001_BaselineSchema`
  generating tables for `User`, `Processor`, `Yard`, `Declaration`, `Vehicle`
  (incl. `PlateLast4` STORED column), `DailyHeader`, `AuditLog`, `IdempotencyKey`,
  with all indexes from [`data-model.md`](./data-model.md). Apply at startup.
  Files: `src/B2.Infrastructure/Migrations/0001_BaselineSchema.cs`. **Dep**:
  T-S1-05. **LOC**: ~250.
- [x] **T-S1-07** Implement `AuditMiddleware` in
  `src/B2.Server.Api/Middleware/AuditMiddleware.cs` writing one immutable
  `AuditLog` row per successful POST/PUT/DELETE/PATCH (excludes
  `/api/admin/health`). Includes actor / action / entity / before / after JSON
  / reason / correlationId. **Dep**: T-S1-04, T-S1-06. **LOC**: ~180.
- [x] **T-S1-08** Implement `IdempotencyMiddleware` in
  `src/B2.Server.Api/Middleware/IdempotencyMiddleware.cs` per
  [`research.md § 2.7`](./research.md). Storage in
  `IdempotencyKey` table; replays return cached body / status. **Dep**: T-S1-06.
  **LOC**: ~180.
- [x] **T-S1-09** [P] Implement `IdempotencyKeyEvictionJob` (Quartz, every 30 min)
  in `src/B2.Infrastructure/Jobs/IdempotencyKeyEvictionJob.cs`. **Dep**: T-S1-08.
  **LOC**: ~60.

### F-01 Login

- [x] **T-S1-10** [P] [F-01] Write Gherkin tests for F-01 (login, lockout,
  auto-logout) in `tests/B2.Server.Api.Tests/Auth/LoginEndpointTests.cs` —
  scenarios verbatim from [`spec.md` F-01](./spec.md) §
  Acceptance Scenarios. Tag `[Trait("Feature","F-01")]`. **Dep**: T-S1-04..08.
  **LOC**: ~250.
- [x] **T-S1-11** [F-01] Implement `User` aggregate + `Username` /
  `RoleEnum` value objects in `src/B2.Domain/Users/`. Enforce INV-8 (write-once
  fields). Unit tests in `tests/B2.Domain.Tests/Users/UserTests.cs` covering
  `MustChangePassword`, lockout state machine. **Dep**: T-S0-04. **LOC**: ~250.
- [x] **T-S1-12** [F-01] Implement `BCryptPasswordHasher` and
  `JwtTokenService` (RS256) in
  `src/B2.Infrastructure/Auth/`. Key sealed via DPAPI in
  `C:\ProgramData\B2\keys\`. Unit tests cover hash/verify and token
  issue/validate. **Dep**: T-S1-11. **LOC**: ~280.
- [x] **T-S1-13** [F-01] Implement `LoginCommandHandler` +
  `RefreshCommandHandler` + `ChangePasswordCommandHandler` in
  `src/B2.Application/Auth/`. FluentValidation validators in
  `src/B2.Application/Auth/Validators/`. **Dep**: T-S1-11, T-S1-12. **LOC**: ~280.
- [x] **T-S1-14** [F-01] Wire `/api/auth/login`, `/api/auth/refresh`,
  `/api/auth/logout`, `/api/auth/change-password` endpoints in
  `src/B2.Server.Api/Endpoints/AuthEndpoints.cs`. Make T-S1-10 pass green.
  **Dep**: T-S1-13. **LOC**: ~150.
- [x] **T-S1-15** [F-01] Implement `LoginPage.xaml` + `LoginViewModel.cs` per
  [wireframe § 6.1](../05-ui-wireframes.md). TabIndex per § 6.1 (1=username,
  2=password, 3=show-toggle, 4=login button); Enter submits; lockout
  countdown; aria-live error region. Files: `src/B2.Desktop/Views/LoginPage.xaml`,
  `src/B2.Desktop/ViewModels/LoginViewModel.cs`. **Dep**: T-S1-14. **LOC**: ~250.
- [x] **T-S1-16** [F-01] Wire Vietnamese strings for Login page into
  `src/B2.Desktop/Resources/Strings/AppResources.vi.resx` (verbatim from
  wireframe § 6.1). Update `LoginPage.xaml` to use `{x:Static …}` references.
  **Dep**: T-S1-15. **LOC**: ~80. **DoD**: §DoD-6.
- [x] **T-S1-22** [F-01] Implement `RequirePasswordChangeMiddleware` in
  `src/B2.Server.Api/Middleware/` and wire it between `UseAuthentication` and
  `UseAuthorization` in `B2.Server.Api/Hosting/B2ApiHost.cs`. Authenticated
  requests carrying `must_change_password=true` are rejected with `403` /
  `code: PASSWORD_CHANGE_REQUIRED`; whitelist `POST /api/auth/change-password`
  and `POST /api/auth/logout`. Tests in
  `tests/B2.Server.Api.Tests/Auth/ChangePasswordEndpointTests.cs` cover blocked
  first-login, successful rotation, wrong current password, and weak new
  password. Update `LoginAsAdminAsync` test helper to bootstrap the seeded admin
  (login → change → re-login) so existing tests keep green. **Dep**: T-S1-14.
  **LOC**: ~180. **DoD**: §DoD-4, §DoD-7, §DoD-8.
- [x] **T-S1-23** [F-01] Implement `Views/ChangePasswordPage.xaml` +
  `ViewModels/ChangePasswordViewModel.cs` with vi.resx strings; register
  `auth/change-password` route in `AppShell` and DI in `MauiProgram`. Login
  flow now redirects to the page when `LoginResponse.MustChangePassword` is
  true; on success the page clears the access token and routes back to
  `//login` so the user re-authenticates with the new password. **Dep**:
  T-S1-15, T-S1-22. **LOC**: ~220. **DoD**: §DoD-6.

### F-02 User & Role Management

- [x] **T-S1-17** [P] [F-02] Write Gherkin tests for F-02 in
  `tests/B2.Server.Api.Tests/Users/UsersEndpointTests.cs` — verbatim from
  [`spec.md` F-02](./spec.md). Tag `[Trait("Feature","F-02")]`. Include
  Operator-403 case (FR-027). **Dep**: T-S1-09. **LOC**: ~200.
- [x] **T-S1-18** [F-02] Implement `CreateUserCommand` /
  `UpdateUserCommand` / `ResetPasswordCommand` handlers + validators in
  `src/B2.Application/Users/`. Enforce "deactivate, not delete" (FR-014).
  **Dep**: T-S1-11, T-S1-13. **LOC**: ~250.
- [x] **T-S1-19** [F-02] Wire `/api/users` endpoints in
  `src/B2.Server.Api/Endpoints/UsersEndpoints.cs` with `AdminOnly` policy
  (FR-027 row). Make T-S1-17 green. **Dep**: T-S1-18. **LOC**: ~150.
- [x] **T-S1-20** [F-02] Implement `Admin/UsersPage.xaml` +
  `UsersViewModel.cs` + Add/Edit modal per [wireframe § 6.8](../05-ui-wireframes.md).
  Bind to API via Refit `IB2Api`. Vietnamese copy verbatim into
  `AppResources.vi.resx`. Files:
  `src/B2.Desktop/Views/Admin/UsersPage.xaml`,
  `src/B2.Desktop/ViewModels/Admin/UsersViewModel.cs`. **Dep**: T-S1-19,
  T-S1-15. **LOC**: ~350. **DoD**: §DoD-6.

### F-03 Processors + F-04 Yards (master data — combined wireframe § 6.9)

- [x] **T-S1-21** [P] [F-03] [F-04] Write Gherkin tests in
  `tests/B2.Server.Api.Tests/MasterData/{ProcessorsTests,YardsTests}.cs`
  verbatim from `spec.md` F-03 + F-04 (incl. yard-code uniqueness, processor
  unknown-on-entry rejection). Tag `[Trait("Feature","F-03")]` and
  `[Trait("Feature","F-04")]`. **Dep**: T-S1-09. **LOC**: ~250.

> **F-03 / F-04 implementation tasks are bundled into Sprint S2 entry-form work
> (T-S2-01..T-S2-04) because the master-data write endpoints + the master-data
> tab UI ship together.** Splitting earlier would create a mid-sprint inert
> deliverable. The Sprint S1 exit gate uses the seeded master data inserted by
> T-S1-06 baseline migration to satisfy the smoke test.

**Sprint S1 exit smoke**: log in as default admin → forced password change →
create Operator account → list users → Operator's `POST /api/users` returns 403.
Verified manually on the spec VM (DoD-9).

---

# Phase 3 — Sprint S2 (Entry & exit happy path)

**Goal**: end-to-end flow — create declaration with first vehicle, add child
vehicles, search by suffix, mark exit. Live updates land in S3.
**Independent test**: Operator creates a declaration with two vehicles; Exit
Operator searches the 4-digit suffix and marks both exits; the declaration
auto-promotes to `AllExited`.
**Sprint dependencies**: S1 complete.

### F-03 / F-04 master-data implementation (deferred from S1 per note above)

- [x] **T-S2-01** [P] [F-03] Implement `Processor` aggregate + repository in
  `src/B2.Domain/MasterData/Processor.cs` and
  `src/B2.Infrastructure/Persistence/Repositories/ProcessorRepository.cs`. EF
  configuration enforces `FullName` UK + `IsActive` default. **Dep**: T-S1-06.
  **LOC**: ~120.
- [x] **T-S2-02** [F-03] Wire `/api/master/processors` GET / POST / PUT / DELETE
  in `src/B2.Server.Api/Endpoints/ProcessorsEndpoints.cs`. Make
  T-S1-21 processor-tests green. **Dep**: T-S2-01. **LOC**: ~140.
- [x] **T-S2-03** [P] [F-04] Implement `Yard` aggregate + repository in
  `src/B2.Domain/MasterData/Yard.cs` and
  `src/B2.Infrastructure/Persistence/Repositories/YardRepository.cs`. EF
  enforces `Code` UK regex `^[A-Z0-9]{1,20}$`. **Dep**: T-S1-06. **LOC**: ~120.
- [x] **T-S2-04** [F-04] Wire `/api/master/yards` GET / POST / PUT / DELETE in
  `src/B2.Server.Api/Endpoints/YardsEndpoints.cs`. Make T-S1-21 yard-tests
  green. **Dep**: T-S2-03. **LOC**: ~140.
- [x] **T-S2-05** [F-03] [F-04] Implement `Admin/MasterDataPage.xaml`
  (tabbed) + `MasterDataViewModel.cs` per
  [wireframe § 6.9](../05-ui-wireframes.md). Vietnamese copy verbatim. Files:
  `src/B2.Desktop/Views/Admin/MasterDataPage.xaml`,
  `src/B2.Desktop/ViewModels/Admin/MasterDataViewModel.cs`. **Dep**: T-S2-02,
  T-S2-04. **LOC**: ~350. **DoD**: §DoD-6.

### F-05 Vehicle Entry

- [x] **T-S2-06** [P] [F-05] Write Gherkin tests for F-05 in
  `tests/B2.Server.Api.Tests/Declarations/CreateDeclarationEndpointTests.cs`
  verbatim from `spec.md` F-05 (happy path, duplicate, 12-digit, server
  timestamp). Tag `[Trait("Feature","F-05")]`. **Dep**: T-S1-09. **LOC**: ~280.
- [x] **T-S2-07** [F-05] Implement `Declaration` aggregate root +
  `DeclarationNumber`, `PlateNumber`, `ContainerNumber` value objects in
  `src/B2.Domain/Declarations/`. Implement `Declaration.RecomputeStatus()`,
  `Vehicle` child entity (private constructor — only `Declaration` creates).
  Enforce INV-1, INV-2, INV-3, INV-7 at construction. Files:
  `Declaration.cs`, `Vehicle.cs`, `DeclarationNumber.cs`, `PlateNumber.cs`,
  `ContainerNumber.cs`, `DeclarationStatus.cs`. Unit tests in
  `tests/B2.Domain.Tests/Declarations/` covering all five invariants.
  **Dep**: T-S0-04. **LOC**: ~400.
- [x] **T-S2-08** [F-05] Implement `DeclarationRepository` in
  `src/B2.Infrastructure/Persistence/Repositories/DeclarationRepository.cs`
  with EF Core mapping respecting `RowVersion BLOB(8)` concurrency token. EF
  configuration in `Persistence/Configurations/DeclarationConfiguration.cs`
  and `VehicleConfiguration.cs`. **Dep**: T-S2-07, T-S1-06. **LOC**: ~250.
- [x] **T-S2-09** [F-05] Implement `CreateDeclarationCommandHandler` +
  validator in `src/B2.Application/Declarations/CreateDeclaration/`. Validator
  enforces INV-1 (12 digits unique) and INV-7 (Processor / Yard active at
  write time). **Dep**: T-S2-07, T-S2-01, T-S2-03. **LOC**: ~250.
- [x] **T-S2-10** [F-05] Wire `POST /api/declarations` endpoint in
  `src/B2.Server.Api/Endpoints/DeclarationsEndpoints.cs` with
  `OperatorOrAbove` policy + `Idempotency-Key` middleware. Make T-S2-06 green.
  **Dep**: T-S2-09, T-S1-08. **LOC**: ~150.
- [x] **T-S2-11** [F-05] Implement `EntryPage.xaml` + `EntryViewModel.cs` per
  [wireframe § 6.3](../05-ui-wireframes.md). TabIndex 1..11; Enter saves;
  Ctrl+Enter saves and opens "add child" mode (F-06). Inline ✓/✗ validation.
  Smart-suggestion ghost text region present but disabled until S6 (F-17).
  Files: `src/B2.Desktop/Views/EntryPage.xaml`,
  `src/B2.Desktop/ViewModels/EntryViewModel.cs`. **Dep**: T-S2-10. **LOC**:
  ~400.
- [x] **T-S2-12** [F-05] Wire Vietnamese strings for EntryPage into
  `AppResources.vi.resx` verbatim from wireframe § 6.3. **Dep**: T-S2-11.
  **LOC**: ~100. **DoD**: §DoD-6.

### F-06 Add Child Vehicle

- [x] **T-S2-13** [P] [F-06] Write Gherkin tests in
  `tests/B2.Server.Api.Tests/Declarations/AddVehicleEndpointTests.cs` verbatim
  from `spec.md` F-06 (add-second, can-not-add-to-KVGS-confirmed,
  can-not-add-to-Cancelled per FR-022). Tag `[Trait("Feature","F-06")]`.
  **Dep**: T-S1-09. **LOC**: ~200.
- [x] **T-S2-14** [F-06] Implement `AddVehicleCommandHandler` in
  `src/B2.Application/Declarations/AddVehicle/` calling
  `Declaration.AddVehicle(...)` aggregate method (which guards locked statuses).
  **Dep**: T-S2-07, T-S2-09. **LOC**: ~180.
- [x] **T-S2-15** [F-06] Wire `POST /api/declarations/{id}/vehicles` endpoint
  in `DeclarationsEndpoints.cs`. Make T-S2-13 green. **Dep**: T-S2-14. **LOC**: ~80.
- [x] **T-S2-16** [F-06] Extend `EntryPage.xaml` with the read-only
  "Tờ khai 1234...12 — [Đổi tờ khai]" banner activated when the page is opened
  in add-child mode after Ctrl+Enter (per wireframe § 6.3 last block).
  **Dep**: T-S2-15, T-S2-11. **LOC**: ~120.

### F-07 Suffix Search

- [x] **T-S2-17** [P] [F-07] Write Gherkin tests in
  `tests/B2.Server.Api.Tests/Vehicles/SearchEndpointTests.cs` verbatim from
  `spec.md` F-07 (un-exited only, 3 or 4 digits, multiple matches, performance
  benchmark on 50k seeded rows). Tag `[Trait("Feature","F-07")]`. **Dep**:
  T-S1-09. **LOC**: ~280.
- [x] **T-S2-18** [F-07] Implement `SearchVehiclesQueryHandler` in
  `src/B2.Application/Vehicles/Search/` using `IX_Vehicle_PlateLast4` index
  (per [`data-model.md § 2.5`](./data-model.md)). Order by `EntryTime` asc.
  Wire `GET /api/vehicles/search` in `VehiclesEndpoints.cs`. **Dep**: T-S2-08.
  **LOC**: ~180.

### F-08 Mark Vehicle Exit (incl. FR-019 idempotency, FR-024 cross-client race)

- [x] **T-S2-19** [P] [F-08] Write Gherkin tests in
  `tests/B2.Server.Api.Tests/Vehicles/ExitEndpointTests.cs` verbatim from
  `spec.md` F-08 (mark exit, auto-promote AllExited, idempotent retry,
  Operator-cannot-un-exit, Admin un-exit override). Add FR-024 cross-client
  race test (two different Idempotency-Keys → first commits, second gets
  `VEHICLE_ALREADY_EXITED` 409 with winner username + ExitTime). Tag
  `[Trait("Feature","F-08")]`. **Dep**: T-S1-09. **LOC**: ~350.
- [x] **T-S2-20** [F-08] Implement `MarkVehicleExitCommandHandler` and
  `UnExitVehicleCommandHandler` in `src/B2.Application/Vehicles/Exit/` calling
  `Vehicle.MarkExit(clock)` / `Vehicle.UnExit(reason)` aggregate methods +
  `Declaration.RecomputeStatus()`. The handler returns the
  `AlreadyExitedProblem` body with winner + ExitTime on 409. **Dep**: T-S2-07,
  T-S2-08. **LOC**: ~250.
- [x] **T-S2-21** [F-08] Wire `POST /api/vehicles/{id}/exit` (Operator+) and
  `POST /api/vehicles/{id}/un-exit` (Admin) in `VehiclesEndpoints.cs`. Make
  T-S2-19 green. **Dep**: T-S2-20, T-S1-08. **LOC**: ~120.
- [x] **T-S2-22** [F-08] [F-07] Implement `ExitPage.xaml` +
  `ExitViewModel.cs` per [wireframe § 6.4](../05-ui-wireframes.md). Big input
  (height 56, font 24); 500 ms debounce; Enter-Enter for single-result quick
  mark; "Đã đánh dấu hôm nay: N xe" self-monitoring widget. Vietnamese copy
  verbatim. **Dep**: T-S2-18, T-S2-21. **LOC**: ~400. **DoD**: §DoD-6.

**Sprint S2 exit smoke**: full happy path from quickstart § 4.3 (steps 1–7).
SignalR live updates land in S3.

---

# Phase 4 — Sprint S3 (KVGS + ledger + realtime)

**Goal**: KVGS confirmation + the always-on ledger view with SignalR push.
**Independent test**: a declaration in `AllExited` is confirmed by Supervisor;
on a second client the row reverts color within 1 s without manual refresh.
**Sprint dependencies**: S2 complete.

### F-09 KVGS Confirmation

- [ ] **T-S3-01** [P] [F-09] Write Gherkin tests in
  `tests/B2.Server.Api.Tests/Declarations/KvgsConfirmEndpointTests.cs` verbatim
  from `spec.md` F-09 (Confirm KVGS, Operator-cannot-confirm, cannot-confirm-
  before-AllExited). Tag `[Trait("Feature","F-09")]`. **Dep**: T-S2-22.
  **LOC**: ~200.
- [ ] **T-S3-02** [F-09] Implement `ConfirmKvgsCommandHandler` in
  `src/B2.Application/Declarations/ConfirmKvgs/` calling
  `Declaration.ConfirmKvgs(actorRole, actorId, clock)` (INV-6 + state-machine
  guard). **Dep**: T-S2-07. **LOC**: ~150.
- [ ] **T-S3-03** [F-09] Wire `POST /api/declarations/{id}/kvgs-confirm` in
  `DeclarationsEndpoints.cs` with `SupervisorOrAbove` policy. Make T-S3-01
  green. **Dep**: T-S3-02. **LOC**: ~80.
- [ ] **T-S3-04** [F-09] Implement `KvgsPage.xaml` + `KvgsViewModel.cs` per
  [wireframe § 6.5](../05-ui-wireframes.md). Confirmation modal with focus
  trap; row removes from list (animated 300 ms) on success. Vietnamese copy
  verbatim. **Dep**: T-S3-03. **LOC**: ~280. **DoD**: §DoD-6.

### F-10 Ledger + FR-027 / FR-028 (realtime)

- [ ] **T-S3-05** [P] [F-10] Write Gherkin tests in
  `tests/B2.Server.Api.Tests/Declarations/ListDeclarationsEndpointTests.cs`
  verbatim from `spec.md` F-10 (default-today / live-updates / pagination /
  writes-permitted-in-🟡 from UI Q4). Tag `[Trait("Feature","F-10")]`.
  **Dep**: T-S2-22. **LOC**: ~280.
- [ ] **T-S3-06** [F-10] Implement `ListDeclarationsQueryHandler` in
  `src/B2.Application/Declarations/List/` with paginated filtering (date /
  status / plate / processor / yard / officer / since-for-polling /
  includeCancelled). **Dep**: T-S2-08. **LOC**: ~250.
- [ ] **T-S3-07** [F-10] Wire `GET /api/declarations` and
  `GET /api/declarations/{id}` (detail + child vehicles) in
  `DeclarationsEndpoints.cs`. Make T-S3-05 partly green (live-updates land in
  T-S3-08..09). **Dep**: T-S3-06. **LOC**: ~120.
- [ ] **T-S3-08** [F-10] Implement `EventsHub` SignalR hub at
  `src/B2.Server.Api/Realtime/EventsHub.cs` and a `ISignalRDomainEventDispatcher`
  in `B2.Application/DomainEvents/`. Wire it so successful Create / Exit /
  KvgsConfirm / Cancel / OfficerNameChanged commands publish events. **Dep**:
  T-S2-21, T-S3-03. **LOC**: ~250.
- [ ] **T-S3-09** [F-10] Implement `SignalRClient` and `ConnectivityState`
  service in `src/B2.Desktop/Services/`. Implement the FR-028 tri-state
  machine (transitions per [`research.md § 3`](./research.md)). Polling
  fallback hits `GET /api/declarations?since=…`. Files:
  `Services/SignalRClient.cs`, `Services/ConnectivityState.cs`. **Dep**:
  T-S3-07, T-S3-08. **LOC**: ~350.
- [ ] **T-S3-10** [F-10] Implement `LedgerPage.xaml` + `LedgerViewModel.cs`
  per [wireframe § 6.2](../05-ui-wireframes.md). Sticky search row, daily
  header rows, 🔴/🟢/🔵/📋 status badges (icon + color + text per Constitution
  VIII), four list-view states (loading / empty / error / offline) per
  wireframe § 8, virtualized `CollectionView`. Subscribe to `ConnectivityState`.
  Vietnamese copy verbatim. **Dep**: T-S3-09. **LOC**: ~500. **DoD**: §DoD-6.
- [ ] **T-S3-11** [F-10] Implement `AppShell.xaml` + `AppShellViewModel.cs`
  per [wireframe § 5](../05-ui-wireframes.md) — top bar with tri-state
  connectivity badge + user menu, sidebar (`Alt+1`..`Alt+8` accelerators), F1
  cheat-sheet modal, status bar with correlationId. **Dep**: T-S3-10. **LOC**:
  ~350.
- [ ] **T-S3-12** [F-10] [F-09] Implement reusable `ToastService` and
  `ModalService` in `src/B2.Desktop/Services/` per
  [wireframe § 7](../05-ui-wireframes.md). Constitution VIII: per-page
  reimplementations are forbidden. **Dep**: T-S3-10. **LOC**: ~280.

**Sprint S3 exit smoke**: from quickstart § 4.3 step 8 (SignalR push + KVGS
revert color on second client within 1 s).

---

# Phase 5 — Sprint S4 (Background jobs + reports)

**Goal**: daily header / summary jobs + the five reports surface.
**Independent test**: at 00:01 a daily-header row is created (or catch-up
fires on next start); at 23:00 the day's `TotalVehicles` populates; the five
reports return numbers matching a hand count of seeded data.
**Sprint dependencies**: S3 complete.

### Quartz infrastructure

- [ ] **T-S4-01** [P] Implement Quartz hosted service registration in
  `src/B2.Server.Service/Quartz/QuartzStartup.cs`. Job store: in-memory
  (jobs are idempotent + cron-only). Files: `QuartzStartup.cs`. **Dep**:
  T-S0-04. **LOC**: ~120.

### F-11 Daily Header Auto-Creation

- [ ] **T-S4-02** [P] [F-11] Write Gherkin tests in
  `tests/B2.Application.Tests/Jobs/CreateDailyHeaderJobTests.cs` verbatim from
  `spec.md` F-11 (header-at-00:01 / catch-up-on-startup / idempotent). Tag
  `[Trait("Feature","F-11")]`. **Dep**: T-S4-01. **LOC**: ~180.
- [ ] **T-S4-03** [F-11] Implement `DailyHeader` aggregate +
  `DailyHeaderRepository` in `src/B2.Domain/DailyHeaders/` and
  `src/B2.Infrastructure/Persistence/Repositories/`. **Dep**: T-S1-06. **LOC**:
  ~150.
- [ ] **T-S4-04** [F-11] Implement `CreateDailyHeaderJob` (Quartz cron
  `0 1 0 * * ?`) + `EnsureTodayHeaderCommandHandler` in
  `src/B2.Infrastructure/Jobs/` and `src/B2.Application/DailyHeaders/`. On
  service startup, run a catch-up check. **Dep**: T-S4-01, T-S4-03. **LOC**:
  ~200.

### F-12 Daily Summary (23:00)

- [ ] **T-S4-05** [P] [F-12] Write Gherkin tests in
  `tests/B2.Application.Tests/Jobs/GenerateDailySummaryJobTests.cs` verbatim
  from `spec.md` F-12 (summary-at-23:00 / re-run-on-demand). Tag
  `[Trait("Feature","F-12")]`. **Dep**: T-S4-01. **LOC**: ~150.
- [ ] **T-S4-06** [F-12] Implement `GenerateDailySummaryJob` (Quartz cron
  `0 0 23 * * ?`) + `RecomputeDailySummaryCommandHandler` (Admin can re-run on
  demand) in `src/B2.Infrastructure/Jobs/` and
  `src/B2.Application/DailyHeaders/`. Catch-up logic for missed runs. **Dep**:
  T-S4-04. **LOC**: ~200.
- [ ] **T-S4-07** [F-12] Wire Admin "Recompute today's summary" button
  endpoint `POST /api/daily-headers/{date}/recompute` (Admin only) in
  `DailyHeadersEndpoints.cs`. Surface in `LedgerPage.xaml` row context-menu
  per wireframe § 6.2. **Dep**: T-S4-06. **LOC**: ~150.

### FR-025 Officer name (inline edit on daily-header row)

- [ ] **T-S4-08** [P] [F-11] Write integration test for
  `PATCH /api/daily-headers/{date}/officer` covering INV-11 (today only),
  audit row presence, and any-role access (FR-027). Tag
  `[Trait("Feature","F-11")]` + `[Trait("Requirement","FR-025")]`. **Dep**:
  T-S4-04. **LOC**: ~150.
- [ ] **T-S4-09** [F-11] Implement `UpdateOfficerNameCommandHandler` and
  PATCH endpoint in `src/B2.Application/DailyHeaders/UpdateOfficer/` and
  `DailyHeadersEndpoints.cs`. **Dep**: T-S4-04. **LOC**: ~150.
- [ ] **T-S4-10** [F-11] Implement inline edit on
  `LedgerPage.xaml` daily-header row + `EditableCell<string>` control with
  optimistic update / revert-on-failure. SignalR `DailyHeaderOfficerChanged`
  push converges other clients. **Dep**: T-S4-09, T-S3-10, T-S3-08. **LOC**:
  ~220.

### F-14 Reports

- [ ] **T-S4-11** [P] [F-14] Write Gherkin tests in
  `tests/B2.Server.Api.Tests/Reports/ReportsEndpointsTests.cs` verbatim from
  `spec.md` F-14 (hour-buckets-configurable, pending-counts-vehicles). Add
  five role-403 tests for Operator-on-each-report (FR-027). Tag
  `[Trait("Feature","F-14")]`. **Dep**: T-S4-04. **LOC**: ~280.
- [ ] **T-S4-12** [F-14] Implement five report query handlers in
  `src/B2.Application/Reports/`: `ByProcessorReportQueryHandler`,
  `ByYardReportQueryHandler`, `ByHourReportQueryHandler`,
  `ByOfficerReportQueryHandler`, `PendingReportQueryHandler`. All respect
  `includeCancelled` (FR-023). **Dep**: T-S2-08, T-S4-03. **LOC**: ~400.
- [ ] **T-S4-13** [F-14] Wire five `GET /api/reports/*` endpoints in
  `src/B2.Server.Api/Endpoints/ReportsEndpoints.cs` with `SupervisorOrAbove`
  policy. Make T-S4-11 green. **Dep**: T-S4-12. **LOC**: ~150.
- [ ] **T-S4-14** [F-14] Implement `ReportsPage.xaml` + `ReportsViewModel.cs`
  per [wireframe § 6.6](../05-ui-wireframes.md). Sortable columns; bar viz
  using `█` characters; copy-to-clipboard + simple Excel export (ClosedXML
  generic, **not** the Sổ B2 template — that's F-13). Vietnamese copy verbatim.
  **Dep**: T-S4-13. **LOC**: ~400. **DoD**: §DoD-6.
- [ ] **T-S4-15** Implement `ArchUnit` test in `tests/B2.Server.Api.Tests/`
  verifying every endpoint declared in `contracts/openapi.yaml` is wired with a
  matching policy from FR-027 (no endpoint left without a `[Authorize]`
  attribute or anonymous + reason). **Dep**: T-S4-13. **LOC**: ~120.

**Sprint S4 exit smoke**: at 00:01 (or simulated via test clock) a header
appears; at 23:00 the day total populates; report numbers match a hand count.

---

# Phase 6 — Sprint S5 (Export, backup, audit, health)

**Goal**: Sổ B2 Excel export + Litestream backup + on-demand snapshot +
restore + audit viewer + health & logs page. Includes FR-022 cancellation +
FR-026 recovery code.
**Independent test**: 30-day export opens cleanly in Excel 2010 (or
LibreOffice); take a snapshot, corrupt the DB, restore, count = original;
audit viewer shows every write tagged with the actor.
**Sprint dependencies**: S4 complete.

### F-13 Excel Export

- [ ] **T-S5-01** [P] [F-13] Write Gherkin tests in
  `tests/B2.Server.Api.Tests/Reports/ExcelExportEndpointTests.cs` verbatim
  from `spec.md` F-13 (1-day, color-preservation, 30-day-perf). Tag
  `[Trait("Feature","F-13")]`. **Dep**: T-S4-15. **LOC**: ~200.
- [ ] **T-S5-02** [F-13] Implement `ExportSoB2QueryHandler` +
  `ClosedXmlExporter` in `src/B2.Application/Export/` and
  `src/B2.Infrastructure/Excel/`. Open template `Mẫu sổ B2.xlsx` from
  `C:\ProgramData\B2\templates\`; fill columns A–O; apply conditional
  formatting (red text for un-exited, green fill for AllExited, default for
  KVGSConfirmed); insert daily-header rows at the top of each day's block;
  strike-through for `Cancelled` (FR-023). All three transformations are
  always-on (Q3 — "Đã bật" badges in UI). **Dep**: T-S2-08, T-S4-03. **LOC**:
  ~400.
- [ ] **T-S5-03** [F-13] Wire `GET /api/reports/excel` endpoint in
  `ReportsEndpoints.cs` with `SupervisorOrAbove`. File name
  `SoB2_{from:yyyyMMdd}_{to:yyyyMMdd}.xlsx`. Make T-S5-01 green. **Dep**:
  T-S5-02. **LOC**: ~80.
- [ ] **T-S5-04** [F-13] Implement `ExportPage.xaml` + `ExportViewModel.cs`
  per [wireframe § 6.7](../05-ui-wireframes.md). Render the three "Bao gồm:"
  options as **read-only "Đã bật" badges** (UI clarification Q3 — NOT
  checkboxes). Progress modal (cancellable). Vietnamese copy verbatim.
  **Dep**: T-S5-03. **LOC**: ~280. **DoD**: §DoD-6.
- [ ] **T-S5-05** [F-13] Bundle `Mẫu sổ B2.xlsx` template into
  `src/B2.Installer/` so it lands at `C:\ProgramData\B2\templates\` after
  install. **Dep**: T-S5-02. **LOC**: ~30.
- [ ] **T-S5-06** [F-13] Add NBomber performance scenario in
  `tests/B2.Performance.Tests/ExportScenarios.cs` asserting the 30-day export
  budget < 10 s on the spec VM (SC-012). **Dep**: T-S5-03. **LOC**: ~120.

### F-15 Backup & Restore + FR-026 Recovery Code

- [ ] **T-S5-07** [P] [F-15] Write Gherkin tests in
  `tests/B2.Server.Api.Tests/Admin/BackupRestoreEndpointTests.cs` verbatim from
  `spec.md` F-15 (continuous-health, restore-round-trip, restore-confirmation).
  Add FR-026 tests: install-time recovery code generation, both-secrets-
  decrypt, lost-password-recovery-code-still-works. Tag
  `[Trait("Feature","F-15")]` + `[Trait("Requirement","FR-026")]`. **Dep**:
  T-S4-15. **LOC**: ~350.
- [ ] **T-S5-08** [F-15] Implement Litestream wrapper in
  `src/B2.Infrastructure/Backup/LitestreamProcess.cs` — child-process supervisor
  spawned by `B2.Server.Service`; reads config from `appsettings.json`; restart
  on crash. **Dep**: T-S0-04. **LOC**: ~250.
- [ ] **T-S5-09** [F-15] Implement `BackupSnapshotService` in
  `src/B2.Infrastructure/Backup/BackupSnapshotService.cs`: `sqlite3 .backup`
  → AES-256-GCM zip with key-wrap (FR-026: DK wrapped twice, once with
  KDF(install-password), once with KDF(recovery-code)). Header JSON lists both
  wrapped DK blobs. **Dep**: T-S5-08. **LOC**: ~350.
- [ ] **T-S5-10** [F-15] Implement `RestoreCommandHandler` in
  `src/B2.Application/Admin/Restore/`: tries password OR recovery code; on
  success, pauses writes (FR-011 🔴 state announced via SignalR), swaps DB
  file, restarts. Downtime budget ≤ 30 s (SC-009). **Dep**: T-S5-09. **LOC**:
  ~280.
- [ ] **T-S5-11** [F-15] Wire `POST /api/admin/backup-now` and
  `POST /api/admin/restore` (multipart) in
  `src/B2.Server.Api/Endpoints/AdminEndpoints.cs`. Restore form accepts
  `password` OR `recoveryCode`. Make T-S5-07 green. **Dep**: T-S5-09, T-S5-10.
  **LOC**: ~180.
- [ ] **T-S5-12** [F-15] Implement `Admin/BackupPage.xaml` +
  `BackupViewModel.cs` per [wireframe § 6.10](../05-ui-wireframes.md).
  Continuous status, manual snapshot, restore with typed-RESTORE confirmation
  (FR-013) + password/recoveryCode field. Vietnamese copy verbatim. **Dep**:
  T-S5-11. **LOC**: ~400. **DoD**: §DoD-6.
- [ ] **T-S5-13** [F-15] Implement install-time recovery-code generator in
  `src/B2.Installer/` — generates 24-char base32, displays once, requires
  Admin to type it back to confirm acknowledgement, persists
  `recoveryCodeAcknowledgedAtInstall: true` flag. **Dep**: T-S5-09. **LOC**:
  ~200.

### FR-022 Declaration Cancellation (Admin only)

- [ ] **T-S5-14** [P] [F-10] Write Gherkin tests in
  `tests/B2.Server.Api.Tests/Declarations/CancelDeclarationEndpointTests.cs`
  for FR-022 (admin-only, only-from-Open, mandatory-reason, prevent-further-
  writes, 403 for Operator/Supervisor). Tag
  `[Trait("Requirement","FR-022")]`. **Dep**: T-S4-15. **LOC**: ~180.
- [ ] **T-S5-15** [F-10] Extend `Declaration` aggregate with `Cancel(reason)`
  method (INV-9, INV-10). Implement `CancelDeclarationCommandHandler` and
  `POST /api/declarations/{id}/cancel` (Admin) in
  `src/B2.Application/Declarations/Cancel/` and `DeclarationsEndpoints.cs`.
  Update `LedgerPage.xaml` and `ExportPage` to render strike-through for
  `Cancelled` rows (FR-023). **Dep**: T-S2-07, T-S5-04. **LOC**: ~300.

### F-16 Audit Log Viewer

- [ ] **T-S5-16** [P] [F-16] Write Gherkin tests in
  `tests/B2.Server.Api.Tests/Admin/AuditEndpointTests.cs` verbatim from
  `spec.md` F-16 (filter-by-entity, audit-is-read-only — no DELETE endpoint
  exists). Tag `[Trait("Feature","F-16")]`. **Dep**: T-S4-15. **LOC**: ~150.
- [ ] **T-S5-17** [F-16] Implement `ListAuditQueryHandler` in
  `src/B2.Application/Admin/Audit/` + `GET /api/admin/audit` endpoint in
  `AdminEndpoints.cs` (Admin only). Implement `Admin/AuditPage.xaml` per
  [wireframe § 6.12](../05-ui-wireframes.md). Vietnamese copy verbatim.
  **Dep**: T-S1-07, T-S5-11. **LOC**: ~400. **DoD**: §DoD-6.

### F-18 Health & Logs

- [ ] **T-S5-18** [P] [F-18] Write Gherkin tests in
  `tests/B2.Server.Api.Tests/Admin/HealthLogsEndpointTests.cs` verbatim from
  `spec.md` F-18 (disk-space-warning, read-recent-log). Tag
  `[Trait("Feature","F-18")]`. **Dep**: T-S4-15. **LOC**: ~180.
- [ ] **T-S5-19** [F-18] Implement `HealthQueryHandler` and
  `LogsQueryHandler` in `src/B2.Application/Admin/`. Wire
  `GET /api/admin/health` (anonymous; minimal info) and `GET /api/admin/logs`
  (Admin) in `AdminEndpoints.cs`. Implement `Admin/HealthPage.xaml`
  (tabbed: Tổng quan / Nhật ký) per
  [wireframe § 6.11](../05-ui-wireframes.md). Vietnamese copy verbatim.
  **Dep**: T-S1-02, T-S5-09. **LOC**: ~450. **DoD**: §DoD-6.

**Sprint S5 exit smoke**: full quickstart § 5 spec-VM smoke checklist (10
steps) MUST pass. DoD-9 enforced for the entire S5 deliverable.

---

# Phase 7 — Sprint S6 (Hardening — F-17 simple v1 + audit + runbook + UAT)

**Goal**: F-17 ships at simple-v1 scope (P1 per UI clarification Q2);
accessibility audit clears; operator runbook in Vietnamese; on-site UAT.
**Independent test**: prime the DB with 28 plate-trailer pairings; entering
the plate in a new form shows the trailer as ghost text; Tab accepts; F1
cheat sheet lists every keyboard shortcut; runbook delivered.
**Sprint dependencies**: S5 complete.

### F-17 Smart Suggestions (simple v1, P1)

- [ ] **T-S6-01** [P] [F-17] Write Gherkin tests in
  `tests/B2.Server.Api.Tests/Vehicles/SuggestEndpointTests.cs` verbatim from
  `spec.md` F-17 + the Phase-1 scope box. Tag
  `[Trait("Feature","F-17")]`. **Dep**: T-S5-19. **LOC**: ~180.
- [ ] **T-S6-02** [F-17] Implement `SuggestPairQueryHandler` per
  [`research.md § 7`](./research.md): top-1 only, last-90-day window, no ML.
  Use `IX_Vehicle_PlateEntry` (added in T-S1-06 baseline migration). Auto-
  disable + INFO log on two consecutive overruns of the < 100 ms budget.
  Files: `src/B2.Application/Vehicles/Suggest/SuggestPairQueryHandler.cs`,
  `src/B2.Server.Api/Endpoints/VehiclesEndpoints.cs` (add suggest endpoint).
  **Dep**: T-S2-08. **LOC**: ~200.
- [ ] **T-S6-03** [F-17] Activate ghost-text suggestion UI on
  `EntryPage.xaml` (the wireframe-§ 6.3 affordance disabled in S2). Hook to
  `GET /api/vehicles/suggest`. Tab accepts; any other key dismisses. Footer
  hint "💡 Gợi ý gần đây …" wires from the same data source. **Dep**:
  T-S6-02, T-S2-12. **LOC**: ~250.
- [ ] **T-S6-04** [F-17] NBomber latency scenario in
  `tests/B2.Performance.Tests/SuggestScenarios.cs` asserting < 100 ms p95 over
  a 90-day primed DB. **Dep**: T-S6-02. **LOC**: ~100.

### Accessibility audit (Constitution VI + VIII; UI ACs from wireframe § 9)

- [ ] **T-S6-05** [P] Write Gherkin accessibility tests in
  `tests/B2.Desktop.Tests/Accessibility/AccessibilityTests.cs` verbatim from
  [`spec.md` UI Acceptance Criteria](./spec.md) (5 scenarios). Tag
  `[Trait("Category","A11y")]`. **Dep**: T-S5-19. **LOC**: ~250.
- [ ] **T-S6-06** Implement `axe-style for MAUI` audit in CI — script that
  walks every page once, asserts contrast ≥ 4.5:1 (≥ 3:1 large/UI), keyboard
  reachability, no missing aria-live regions. Files:
  `tests/B2.Desktop.Tests/Accessibility/AxeRunner.cs`,
  `.github/workflows/ci.yml` (add a11y job). **Dep**: T-S6-05. **LOC**: ~180.
- [ ] **T-S6-07** [P] Add Cỡ chữ: Bình thường / Lớn / Rất lớn toggle in
  `AppShellViewModel`; persists to `Preferences`; resizes by `Font.SizeLargeMode`
  delta per wireframe § 5 status bar. **Dep**: T-S3-11. **LOC**: ~150.

### Annual archive (FR-021 + INV-12)

- [ ] **T-S6-08** [P] [F-15] Write tests in
  `tests/B2.Application.Tests/Jobs/AnnualArchiveJobTests.cs` for FR-021
  (verify-then-delete; failure rolls back). Tag
  `[Trait("Requirement","FR-021")]`. **Dep**: T-S5-19. **LOC**: ~200.
- [ ] **T-S6-09** [F-15] Implement `AnnualArchiveJob` (Quartz cron `0 0 1 1
  2 ?` configurable) per [`data-model.md § 6.2`](./data-model.md). Writes
  Excel + PDF to `C:\ProgramData\B2\archive\{year}\`; verifies; deletes; logs
  failure if verification fails. **Dep**: T-S4-01, T-S5-02. **LOC**: ~350.

### Runbook + UAT

- [ ] **T-S6-10** Author `docs/runbook.md` per
  [`04-implementation-plan.md § 12`](../04-implementation-plan.md) — Vietnamese,
  5 pages: start/stop service, reset Admin password (recovery code), backup
  manually, restore + warning, read Health, 3 common-error fixes, escalation
  contact. **Dep**: T-S5-13. **LOC**: ~400 (markdown).
- [ ] **T-S6-11** Build the Velopack installer in `src/B2.Installer/`
  bundling: dotnet runtime, self-signed cert, Litestream binary, Sổ B2
  template, recovery-code generator. Output: single `.exe`. **Dep**: T-S5-13,
  T-S5-05. **LOC**: ~250 (config + scripts).
- [ ] **T-S6-12** Run on-site UAT per quickstart § 5 spec-VM checklist (all
  10 steps) **on real hardware** (not VM). Capture findings; fix any
  CRITICAL/HIGH; re-run smoke. Sign-off into `docs/uat-001-b2-clearance.md`.
  **Dep**: T-S6-11, all prior. **LOC**: ~150 (markdown report).

**Sprint S6 exit gate** = Phase-1 GA Deliverables Checklist from
[`04-implementation-plan.md § 13`](../04-implementation-plan.md):

- [ ] All P0 features (F-01 .. F-13, F-15) Done.
- [ ] All P1 features (F-14, F-16, F-17 simple v1, F-18) Done.
- [ ] Installer artifact builds reproducibly from a tag.
- [ ] Spec-VM smoke checklist passes (DoD-9).
- [ ] Performance budget met (`04-implementation-plan.md § 11` — verified by
  NBomber).
- [ ] Security checklist passed (`04-implementation-plan.md § 10`).
- [ ] Vietnamese runbook delivered.
- [ ] Two on-site training sessions delivered.
- [ ] Source code + ADRs handed over.

---

## Dependencies Summary (sprint-level)

```
S0 (Bootstrap) ─► S1 (Auth + Users + Master Data tests)
                  │
                  ▼
                  S2 (Master Data impl + Entry/Exit)
                  │
                  ▼
                  S3 (KVGS + Ledger + Realtime + AppShell)
                  │
                  ▼
                  S4 (Daily jobs + Reports + Officer name)
                  │
                  ▼
                  S5 (Excel + Backup + Cancel + Audit + Health)
                  │
                  ▼
                  S6 (F-17 simple v1 + a11y + Archive + Runbook + UAT)
```

Within a sprint, parallelism is marked `[P]`. The most common parallel
pattern is **"write tests"** (always `[P]` because they live in the test
project and depend only on prior infrastructure tasks). Implementation tasks
depend on their tests.

---

## Parallel-execution examples (per sprint)

**Sprint S0 fan-out** (after T-S0-01 lands): T-S0-03, T-S0-04, T-S0-05,
T-S0-06, T-S0-07, T-S0-08, T-S0-09, T-S0-10, T-S0-11 are all `[P]` — five
ADRs + arch-test + MAUI csproj edit can run in seven concurrent worktrees.

**Sprint S1 fan-out** (after T-S1-09 cross-cutting infra): T-S1-10, T-S1-17,
T-S1-21 are all `[P]` test-writing tasks for F-01 / F-02 / F-03 + F-04. Three
agents can write tests concurrently; implementation tasks then serialize per
feature.

**Sprint S2 fan-out** (after T-S1-21): T-S2-01 + T-S2-03 are `[P]` (different
master-data entities); T-S2-06, T-S2-13, T-S2-17, T-S2-19 are `[P]`
test-writing tasks for F-05 / F-06 / F-07 / F-08.

**Sprint S5 fan-out**: T-S5-01, T-S5-07, T-S5-14, T-S5-16, T-S5-18 — five
test-writing tasks `[P]` covering F-13 / F-15 / FR-022 / F-16 / F-18. The
heavy implementation tasks (T-S5-02 ClosedXML, T-S5-09 BackupService,
T-S5-19 Health) can also run in parallel because they touch different
projects.

---

## Implementation strategy

1. **MVP definition**: Sprint S0 + S1 + S2 = a system that can log in, manage
   master data, and process the entry-and-exit happy path *without* live
   updates. This is enough for a first internal demo.
2. **Incremental delivery**: every sprint ends with a working `.exe` that the
   stakeholder can install on a test VM (per
   [`04-implementation-plan.md § 2`](../04-implementation-plan.md)).
3. **TDD per directive rule 6**: every feature's "Write Gherkin tests" task
   completes before any of that feature's implementation tasks. Failing-then-
   passing trace is observable in CI.
4. **Avoid out-of-stack libraries**: any library suggestion outside
   `01-overview.md § 6` requires a new ADR (Constitution II). The plan
   already preserves the locked stack; no implementation task introduces a
   new dependency.
5. **PR sizing**: every task is ≤ 1 dev-day or ≤ 400 LOC diff (directive rule
   1). Larger items (T-S2-07 Declaration aggregate, T-S3-10 LedgerPage) sit
   at the upper bound — review with care.
6. **Don't skip DoD**: each task carries the 10-point DoD pointer. Item
   DoD-9 (spec-VM smoke) only fires for tasks that touch startup / installer
   / persistence / backup / service lifecycle; the rest get DoD-1..DoD-8 +
   DoD-10.
