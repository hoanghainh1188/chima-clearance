<!--
SYNC IMPACT REPORT
==================
Version change: 1.0.0 → 1.1.0
Bump rationale: MINOR — one new principle added (VIII. UI Wireframes
Binding) to bind the Desktop layer to the wireframe contract in
`specs/05-ui-wireframes.md`, which was authored after the initial
constitution ratification. No existing principle was altered, removed,
or re-numbered (Principles I–VII are byte-identical to v1.0.0). No
governance rule changed.

Modified principles: none

Added sections:
  - Core Principles → VIII. UI Wireframes Binding (NON-NEGOTIABLE)

Removed sections: none

Templates requiring updates (for Principle VIII propagation):
  - .specify/templates/plan-template.md         ⚠ pending — Constitution
                                                  Check must add an item
                                                  for Principle VIII
                                                  (wireframe fidelity,
                                                  design-token resource
                                                  dictionaries, 4 list
                                                  states, icon+color+text
                                                  status indicators,
                                                  reusable Toast/Modal,
                                                  Vietnamese copy verbatim)
  - .specify/templates/spec-template.md         ⚠ pending — UI section in
                                                  feature specs MUST
                                                  reference the relevant
                                                  wireframe in
                                                  specs/05-ui-wireframes.md
                                                  § 6.X
  - .specify/templates/tasks-template.md        ⚠ pending — add standing
                                                  task buckets for
                                                  Colors.xaml /
                                                  Typography.xaml token
                                                  declaration, shared
                                                  Toast/Modal services,
                                                  and list-state
                                                  components
  - .specify/templates/checklist-template.md    ⚠ pending — link the
                                                  accessibility AC in
                                                  specs/05-ui-wireframes.md
                                                  § 9
  - README.md                                   ⚠ pending — link to
                                                  specs/05-ui-wireframes.md
                                                  alongside the existing
                                                  Constitution link

Templates already-pending from v1.0.0 (still pending):
  - .specify/templates/plan-template.md, spec-template.md,
    tasks-template.md, checklist-template.md, README.md, docs/runbook.md
  (See git history of this file for the original v1.0.0 punch list.)

Follow-up TODOs:
  - None. Last Amended date is the date of this amendment.
-->

# Chima Customs B2 Constitution

## Core Principles

### I. Constraint-Driven Architecture (NON-NEGOTIABLE)

The eight operating constraints in `specs/01-overview.md § 2` (C1–C8) are
immutable for Phase 1. Every design and implementation decision MUST be
checkable against them before merge:

- C1 LAN-only / no internet — no cloud calls, no online package fetch at
  runtime, self-signed TLS only.
- C2 Low-spec hardware (i5-3570 / 4–8 GB / Win 10) — no heavy frameworks;
  cold start MUST stay under the budget in `01-overview.md § 7`.
- C3 No dedicated IT team — installer is one-click; recovery, backup, and
  admin tooling are in-app.
- C4 Native desktop UX — keyboard-first, large fonts, no web-app patterns.
- C5 Entry-gate PC = server + client — single-host failure modes are
  acceptable iff continuous backup (C5 + Litestream) is healthy.
- C6 Server runs 24/7 — implemented as a Windows Service that auto-starts
  independent of user login.
- C7 ~5 concurrent users — SQLite is sufficient; no client/server RDBMS
  may be introduced.
- C8 No offline-first on exit/supervisor PCs — when the server is down the
  client MUST display "Mất kết nối" and stop, not queue writes locally.

**Rationale**: these constraints are stakeholder-approved physical and
organizational realities; violating them invalidates the deployment.

### II. Locked Tech Stack & ADR Governance (NON-NEGOTIABLE)

The technology table in `specs/01-overview.md § 6` is the **complete and
exclusive** list of runtime, build, test, and packaging dependencies for
Phase 1. No code MAY introduce a library, framework, runtime, ORM,
logging sink, validator, HTTP client, installer, or test tool outside
that table.

Any deviation requires an Architecture Decision Record under
`docs/adr/NNNN-title.md` per `specs/04-implementation-plan.md § 9`,
authored and merged **before** the dependency lands. The ADR MUST cover:
Status, Date, Context, Decision, Consequences, Alternatives considered.
ADRs 0001–0005 (Modular Monolith, SQLite, .NET MAUI, JWT/RS256, Litestream)
MUST exist before the first feature PR.

**Rationale**: stack drift is the fastest way to break C1, C2, and C3.
A merged ADR is the project's only legitimate way to change § 6.

### III. Code Quality (Nullable + TreatWarningsAsErrors)

All `.csproj` projects MUST set:

- `<Nullable>enable</Nullable>` (no project-level disable; per-file
  `#nullable disable` is forbidden).
- `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.
- `<LangVersion>latest</LangVersion>` consistent with .NET 8 LTS.

Additional code-style rules (from impl-plan § 3.3) are non-negotiable:

- `dotnet format` MUST pass in CI; `.editorconfig` is the source of truth.
- I/O is `async` by default. `.Result`, `.Wait()`, and `.GetAwaiter().GetResult()`
  on async APIs are forbidden outside `Main` / startup composition.
- `record` for DTOs and value objects; `class` for entities.
- One public type per file; files SHOULD stay under 400 lines (hard cap
  800), functions under 50 lines, nesting under 4 levels.

**Rationale**: warnings caught at compile time are bugs we never ship.
Nullable enforcement is the cheapest defense against NREs in a system
that operates without on-site IT support (C3).

### IV. Test-First Discipline (xUnit + FluentAssertions, ≥80%)

Tests are written **before** implementation. Every PR MUST satisfy
the Definition of Done in `specs/04-implementation-plan.md § 1`:

- Domain invariants: xUnit + FluentAssertions, ≥ 90 % line coverage.
- Application use cases: xUnit + NSubstitute against mocked repositories,
  ≥ 80 % line coverage.
- Infrastructure: xUnit against in-memory SQLite (smoke-level repository
  and EF Core integration).
- API endpoints: xUnit + `WebApplicationFactory<Program>` against
  `Microsoft.Data.Sqlite` in-memory shared-cache mode; **every endpoint
  and every Gherkin acceptance criterion** in `03-functional-spec.md`
  MUST be covered by a test named `Should_<criterion>` and tagged
  `[Trait("Feature", "F-XX")]`.
- Desktop: ViewModel unit tests for critical view models; UI E2E is out
  of scope for Phase 1.

Aggregate solution-wide line coverage MUST be ≥ 80 % and MUST NOT
regress between merges. Performance budgets in impl-plan § 11 MUST be
re-verified before each release tag.

**Rationale**: with no QA team and 5 production users, the test suite is
the only line of defense. Coverage thresholds and per-Gherkin tagging
make regressions traceable to a stakeholder-signed criterion.

### V. Layered Dependency Rules (NON-NEGOTIABLE)

The dependency graph in `specs/04-implementation-plan.md § 3.4` is
enforced — by code review and (where feasible) by an architecture test:

```text
Domain        → (nothing)
Application   → Domain
Infrastructure→ Application, Domain
Server.Api    → Application, Infrastructure
Server.Service→ Server.Api
Desktop       → Shared.Contracts
Shared.Contracts ↔ Application (DTOs only)
```

Hard rules:

1. `B2.Domain` MUST NOT reference any package outside the .NET BCL —
   not even `Microsoft.Extensions.*`.
2. No project — production or test — MAY reference `B2.Desktop`.
3. `B2.Desktop` and `B2.Server.Api` MUST share types **only** via
   `B2.Shared.Contracts`. Direct project references between them are
   forbidden.
4. Cross-layer leakage (e.g., `EF Core` types in `Application`,
   `HttpContext` in `Domain`) is a CRITICAL review finding and blocks
   the merge.

**Rationale**: the modular monolith only stays modular if the layer
boundaries are enforced. This is what protects the option to extract
services in Phase 3 without rewriting the core.

### VI. Vietnamese-First UX & Accessibility

The system serves Vietnamese-speaking customs operators on a 24/7 gate.
The UX layer MUST therefore obey:

- **Localization**: all user-facing strings live in
  `Resources/Strings/AppResources.vi.resx`. Hard-coded Vietnamese or
  English literals in XAML, code-behind, ViewModels, validators,
  exception messages surfaced to UI, or Excel templates are forbidden.
  Logs and developer-only messages MAY remain in English.
- **Typography**: base font size ≥ 16 device-independent units;
  large-text mode SHALL bump to ≥ 20.
- **Contrast**: every foreground/background pairing in
  `Resources/Styles/Colors.xaml` MUST meet WCAG 2.1 AA — ≥ 4.5:1 for
  body text, ≥ 3:1 for large text and UI components. Verified ratios
  MUST be recorded in the resource dictionary as a comment next to each
  color pair.
- **Conformance**: WCAG 2.1 AA is the floor. Each release goes through
  the accessibility audit checklist before tagging.
- **Keyboard parity**: every action reachable by mouse MUST be
  reachable by keyboard; focus order is logical; visible focus rings
  are present (2 px outline, ≥ 3:1 contrast).
- **No web-app idioms** (right-click context menus, hover-only
  affordances, browser-tab metaphors) on screens an operator uses daily.

**Rationale**: C4 (operator profile) plus the non-functional target
"new-user proficiency < 15 min without training" make Vietnamese-first
copy and AA accessibility load-bearing requirements, not nice-to-haves.

### VII. Security & Auditability

Every release MUST pass the security checklist in
`specs/04-implementation-plan.md § 10`:

- No secrets in source or repo (verified by `gitleaks` in CI).
- All API endpoints require auth except `/api/auth/login` and
  `/api/admin/health`.
- Every write command checks a role policy (server-side; never UI-only).
- An `AuditLog` row is written on every successful write command;
  reviews MUST verify the row in tests, not just code paths.
- Passwords: BCrypt cost = 10; min length 10; 3 of 4 character classes;
  default admin password rotation forced on first login.
- JWT: RS256; signing key sealed with DPAPI on disk.
- Transport: self-signed cert installed by the installer to
  `LocalMachine\Root`; firewall rule scoped to LAN profile only.
- Backup archives encrypted with AES-256.
- Failed-login lockout and idle-session timeout MUST be active and
  tested.

**Rationale**: this is the single set of controls a non-IT operator
team can rely on to keep the system trustworthy without external
monitoring.

### VIII. UI Wireframes Binding (NON-NEGOTIABLE)

The 12 page wireframes, design tokens, and state specifications in
`specs/05-ui-wireframes.md` are the binding contract for every Desktop
surface. The following are non-negotiable:

- **Page fidelity**: every XAML page MUST follow the layout, tab
  order, and keyboard shortcuts defined in
  `specs/05-ui-wireframes.md § 6` for the corresponding wireframe
  (Pages 6.1–6.12).
- **Design tokens**: colors (`Color.*`), typography (`Font.*`), and
  spacing (`Spacing.*`) defined in `specs/05-ui-wireframes.md § 2–3`
  MUST be declared as `StaticResource` entries in
  `Resources/Styles/Colors.xaml` and `Resources/Styles/Typography.xaml`.
  Hard-coded hex values, font sizes, or spacing constants outside
  those two files are forbidden.
- **List view states**: every list/table view MUST implement the four
  states defined in `§ 8`:
  1. **Loading** — skeleton placeholder.
  2. **Empty** — icon + primary action.
  3. **Error** — red banner with retry control and `correlationId`.
  4. **Offline** — yellow banner with last-success timestamp.
- **Status indicators**: any state cue (🔴/🟢/🔵/📋, badge, chip,
  row colorization) MUST combine icon + color + text label
  simultaneously. Color alone is forbidden (cf. `§ 1` D5 / WCAG 1.4.1).
- **Reusable services**: Toast and Modal are services in
  `B2.Desktop/Services/`. Per-page reimplementations are forbidden.
- **Vietnamese copy**: every Vietnamese string in the wireframes is
  final. Copy verbatim into
  `Resources/Strings/AppResources.vi.resx`; do not paraphrase.

**Rationale**: the wireframes were authored after the original spec
files and codify accessibility, error-recovery, and operator-flow
decisions that the rest of the spec only sketches. Without binding,
Desktop work tends to silently drift toward template-looking UI
(violating Principle VI) and lose the recovery affordances operators
need on a 24/7 gate (violating the spirit of Principle I, C8).

## Technology Stack & Hardware Constraints

The locked technology choices live in `specs/01-overview.md § 6`. They
are reproduced here by reference, not by copy, to keep a single source
of truth. Any change to that table is governed by Principle II (ADR
required). The hardware constraints in `specs/01-overview.md § 2` C1–C8
are reproduced by reference under Principle I.

Cross-cutting standards that bind on top of § 6:

- Target framework: `net8.0` for libraries; `net8.0-windows10.0.19041.0`
  for `B2.Desktop`; `WindowsPackageType=None` (plain Win32 exe) is
  required for installer compatibility.
- Database PRAGMAs (impl-plan § 5.1) MUST be applied at startup:
  `journal_mode=WAL`, `synchronous=NORMAL`, `temp_store=MEMORY`,
  `mmap_size=268435456`, `busy_timeout=5000`.
- Logging: Serilog with rolling file sink only. No remote sinks, no
  telemetry SDKs (C1).
- Performance budgets in impl-plan § 11 are CI-checked nightly via
  NBomber on the spec VM; missed budgets block release.

## Development Workflow & Quality Gates

The end-to-end workflow follows
`specs/04-implementation-plan.md § 1, § 3, § 7, § 9, § 14`.

Branching and commits:

- `main` is always green and deployable.
- One feature per branch: `feat/F-XX-short-name`. Other prefixes:
  `chore/`, `fix/`, `refactor/`, `docs/`.
- Squash-merge into `main`. PR titles MUST reference the feature ID(s).
- Conventional Commits format (e.g., `feat(F-05): ...`).

Quality gates that MUST pass before merge:

1. `dotnet format --verify-no-changes` is clean.
2. `dotnet build -c Release /p:TreatWarningsAsErrors=true` succeeds.
3. `dotnet test -c Release` passes; coverage ≥ 80 % aggregate; per-layer
   thresholds in Principle IV met.
4. All Gherkin acceptance criteria for every implemented feature ID are
   covered by `Should_<criterion>` tests tagged `[Trait("Feature","F-XX")]`.
5. Vietnamese strings present in `AppResources.vi.resx`; no hard-coded
   user-facing text.
6. Logging is at correct levels (Information for command success,
   Warning for retried errors, Error for unhandled).
7. AuditLog assertions exist for write-command tests.
8. Security checklist (impl-plan § 10) is reviewed; any unticked item
   blocks merge.
9. Smoke checklist on the spec VM (impl-plan § 8) passes for any
   feature that touches startup, installer, persistence, backup, or
   service lifecycle.
10. CHANGELOG entry added.

ADR workflow:

- ADRs live under `docs/adr/NNNN-title.md`.
- New ADR is required for any change to `01-overview.md § 6` or to a
  governance rule in this Constitution.
- ADRs MUST be merged before the code that depends on them.

Definition of Done (per feature) is the union of these gates and the
ten-point list in `specs/04-implementation-plan.md § 1`. Any gap blocks
the feature, regardless of demo readiness.

## Governance

This Constitution supersedes informal practice. When it conflicts with
ad hoc convention or with a contributor's prior project habits, the
Constitution wins. Where it conflicts with the spec documents
(`specs/01-overview.md`, `specs/02-architecture.md`,
`specs/03-functional-spec.md`, `specs/04-implementation-plan.md`), the
spec documents win — this file restates and binds them, it does not
override them.

Amendment procedure:

1. Open a `docs/adr/NNNN-amendment-...md` describing the proposed
   change, its motivation, the affected principle(s), and the migration
   plan for in-flight work.
2. Update this file in the same PR. Update the version line per the
   semantic-versioning policy below. Update the Sync Impact Report at
   the top of this file.
3. Propagate changes to dependent artifacts: `.specify/templates/*`,
   `README.md`, `docs/runbook.md`, and any agent-specific guidance
   files (e.g., `CLAUDE.md`, `.cursor/rules/*`). Mark each as ✅
   updated or ⚠ pending in the Sync Impact Report.
4. Required reviewers: one architect + one engineer outside the
   submitter's working pair.

Versioning policy for this Constitution:

- **MAJOR** — backward-incompatible removal or redefinition of a
  principle or governance rule (e.g., dropping Principle V, replacing
  the testing framework family).
- **MINOR** — new principle or section, or materially expanded
  guidance within an existing principle.
- **PATCH** — clarifications, wording, typo fixes, non-semantic
  refinements that do not change what is required.

Compliance review:

- Every PR description MUST list which principles were touched and how
  they were satisfied (or, if a deviation is requested, link the ADR).
- Every release tag triggers a Constitution-compliance pass: each
  principle is checked off against the release artifact before the
  installer is published.
- Runtime developer guidance for AI coding agents lives in
  `specs/04-implementation-plan.md § 14`. Agent-specific files
  (CLAUDE.md, AGENTS.md, `.cursor/rules/*`) are derived views of this
  Constitution and the spec documents; they MUST NOT introduce rules
  that are absent here.

**Version**: 1.1.0 | **Ratified**: 2026-05-03 | **Last Amended**: 2026-05-03
