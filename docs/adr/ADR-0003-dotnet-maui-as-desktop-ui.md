# ADR-0003: .NET MAUI 8 as the desktop UI framework

**Status**: Accepted
**Date**: 2026-05-03
**Supersedes**: none

## Context

The desktop client must present a native Vietnamese-first UX on Windows 10 / 11
gate PCs and supervisor PCs (constraints C2, C4). Stakeholders explicitly preferred
.NET MAUI to keep cross-platform options open. The 12 wireframes in
`specs/05-ui-wireframes.md` are binding for the Desktop layer (Constitution VIII).

## Decision

Adopt **.NET MAUI 8 (LTS)** for `B2.Desktop`, **Windows target only for Phase 1**:

- `<TargetFrameworks>net8.0-windows10.0.19041.0</TargetFrameworks>`
- `<UseMaui>true</UseMaui>`, `<SingleProject>true</SingleProject>`
- `<WindowsPackageType>None</WindowsPackageType>` — ship as plain Win32 exe so
  Velopack / Inno Setup can deploy on locked-down customs PCs (MSIX is harder to
  push without sysadmin tooling).
- MVVM via **CommunityToolkit.Mvvm** source generators
  (`[ObservableProperty]`, `[RelayCommand]`).
- Theming via custom resource dictionaries
  (`Resources/Styles/Colors.xaml`, `Typography.xaml`) — design tokens from
  `specs/05-ui-wireframes.md § 2-3`. **No** third-party Material lib (none mature
  enough on MAUI as of authoring).
- Refit + Polly v8 for the typed HTTP client to `B2.Server.Api`.
- SignalR client with automatic reconnect + polling fallback (FR-028 tri-state).

## Consequences

**Positive**:

- Single codebase, single install per machine.
- LTS support window through Nov 2026; runtime included in installer.
- macOS / iOS / Android targets remain available for hypothetical future.
- WCAG 2.1 AA achievable with the chosen toolkit (Constitution VI).

**Negative / accepted**:

- MAUI's DataGrid is weak on Windows. Mitigation: use a virtualised
  `CollectionView` for the ledger (`02-architecture.md § 4.6`). If unacceptable
  in performance review, escalate via a future ADR (Syncfusion Community vs hand-
  rolled).
- Native rich keyboard-shortcut handling requires Windows-specific code in
  `Platforms/Windows/`. Acceptable given `WindowsPackageType=None` constrains
  Phase 1 to Windows already.
- Hot reload occasionally flaky — documented in `docs/dev-setup.md` (S1
  follow-up).

## Alternatives considered

- **WPF** — rejected: no cross-platform path; tooling is in maintenance mode.
- **WinUI 3** — rejected: more complex packaging story; less stakeholder
  familiarity.
- **Avalonia** — rejected: smaller ecosystem; stakeholder explicitly preferred
  MAUI.
- **Pure web app** — rejected per ADR-0001 (constraint C4).

## References

- `specs/01-overview.md § 6` tech-stack table
- `specs/05-ui-wireframes.md` (binding)
- `specs/04-implementation-plan.md § 4` (.NET MAUI specifics)
- `.specify/memory/constitution.md` Principles VI + VIII
