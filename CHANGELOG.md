# Changelog

All notable changes to this project are documented here. Format follows
[Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/) and the project
uses [Conventional Commits](https://www.conventionalcommits.org/).

## [Unreleased]

Sprints S0 through S4 are merged on `main`. The nine S4 review carry-overs
(`T-S4-CO-01..09`) are now cleared (see below). Remaining low-priority polish
from S3: ledger child-vehicle expand/collapse, right-click context menu,
300/400 ms animations on SignalR push, sidebar collapse toggle, focus-ring
style sweep, ⚙️ quick-settings menu, top-right toast position.

### Added

#### Sprint S5 — F-13 Sổ B2 Excel export (backend) — `feat/F-13-excel-export`

First slice of Sprint S5: the template-based Sổ B2 export endpoint. UI
(`ExportPage`, T-S5-04) and the NBomber 30-day perf scenario (T-S5-06) follow
in the next PR. Full `B2.sln -c Release` builds clean (warnings-as-errors);
Server.Api (68), Application (7), Domain (5) suites pass; `dotnet format` clean.

- **T-S5-02 exporter** — `ClosedXmlSoB2Exporter` (B2.Infrastructure/Excel)
  renders the legacy column order A–O from the `Mẫu sổ B2.xlsx` template:
  daily-header banner row per day, parent row (declaration metadata) + child
  vehicle rows, red font for un-exited vehicles, green fill on `AllExited`
  parent rows, strike-through for `Cancelled` (FR-023). Times rendered in
  `Asia/Ho_Chi_Minh`. The template is an **embedded resource** so the export
  works with no on-disk file; `C:\ProgramData\B2\templates\Mẫu sổ B2.xlsx`
  overrides it when present.
- **Assembly** — `ExportSoB2QueryHandler` (B2.Application/Export) groups
  declarations by business day, merges each day's persisted daily-header
  (officer + total), and emits a header row even for declaration-less days;
  an empty range still yields a valid file. `ISoB2Exporter` /
  `ISoB2ExportRepository` abstractions keep ClosedXML + EF in Infrastructure.
- **T-S5-03 endpoint** — `GET /api/reports/excel?from=&to=`
  (`SupervisorOrAbove`), returns the `.xlsx` as `SoB2_{from}_{to}.xlsx`.
  Added to the FR-027 `EndpointPolicyAlignmentTests` matrix.
- **T-S5-05** — template bundled into `src/B2.Installer/Templates/` (Content,
  PreserveNewest) for the Velopack packaging in T-S6-11.
- **T-S5-01 tests** — `ExcelExportEndpointTests` (5): 1-day header + rows +
  column order, un-exited-red, cancelled-strikethrough, operator-403, file
  name + content type.
- **Notes** — `SoB2ExportRepository` runs two flat queries stitched
  client-side because SQLite cannot `ORDER BY` a `DateTimeOffset`. ClosedXML
  added to B2.Infrastructure + B2.Server.Api.Tests.

### Changed

#### S4 review carry-overs `T-S4-CO-01..09`

Quality + correctness follow-ups deferred from PR #18, cleared as one batch.
Build green on `B2.sln -c Release` (warnings-as-errors); Application,
Server.Api, and Domain test suites all pass.

- **CO-01** — `EndpointPolicyAlignmentTests` now asserts every `/api/`
  endpoint binds its *specific* FR-027 policy (new `Should_each_endpoint_bind_the_specific_fr027_policy`
  drives an explicit method × route → policy matrix), not merely that some
  auth attribute is present. A new endpoint that is not classified in the
  matrix fails the test.
- **CO-02** — added F-11 startup catch-up tests (`Should_catch_up_today_header_on_startup_when_missing`,
  multi-day backfill, fresh-install-only-today, idempotent-when-present).
- **CO-03** — `EnsureTodayHeaderCommandHandler.EnsureCatchUpAsync` backfills
  every business day from the last persisted header through today (capped at
  366 days); `DailyHeaderCatchupHostedService` recomputes each day's summary
  across the window. New `IDailyHeaderRepository.GetLatestDateAsync`.
- **CO-04** — `ReportsRepository.GetByOfficerAsync` wraps the entry-count and
  KVGS-count reads in a snapshot transaction so a concurrent WAL writer cannot
  skew one tally against the other. The KVGS-only-officer union landed
  independently in PR #37 (`fix(F-14)`); this rebase reconciles the two — the
  retained implementation keeps the union (folding the officer-name lookup into
  the KVGS query's join) and adds the transaction on top. PR #37's KVGS-only
  tests pass against it.
- **CO-05** — removed the redundant `.Include(v => v.Declaration)` from
  `ReportsRepository.BaseVehicleQuery` (callers only touch the navigation
  inside server-side projections/predicates).
- **CO-06** — consolidated the duplicated Vietnam time-zone lookup into a
  single cached `VietnamTime.TimeZone` (used by Quartz triggers + report
  hour-bucketing). `LedgerViewModel.VietnamToday` keeps its own copy by
  necessity — Constitution V forbids `B2.Desktop → B2.Application` — now
  documented in a code comment.
- **CO-07** — moved the officer-name validator's English literal into
  `B2.Application.Common.ValidationMessages`.
- **CO-08** — `ReportsViewModel.LoadAsync` fires the five report reads with
  `Task.WhenAll` instead of five sequential awaits.
- **CO-09** — `ConnectivityService` implements `IAsyncDisposable` and disposes
  the `HubConnection` on both Dispose paths (no more leaked TCP connection on
  DI tear-down).

### Added

#### PR #18 — Sprint S4: Quartz jobs + reports + officer name (`9f0d15b`)

Squash-merged 2026-05-05 after 5 commits. Combines Cursor's first-pass
S4 implementation with a review-fix layer. CI green on `windows-2022` +
`ubuntu-latest`; gitleaks transient runner-acquisition failure on the
post-merge run was cleared by re-running the failed job.

- **F-11 Daily Header**:
  - `EnsureTodayHeaderCommandHandler` + `CreateDailyHeaderJob` (cron `0 1 0 * * ?` Asia/Ho_Chi_Minh).
  - `DailyHeaderCatchupHostedService` runs ensure-today + recompute-today on Windows Service start.
  - `IDailyHeaderRepository` (`GetByDateAsync`, `GetByDateRangeAsync`, `CountVehiclesEnteredOnDateAsync`).
- **F-12 Daily Summary**:
  - `RecomputeDailySummaryCommandHandler` + `GenerateDailySummaryJob` (cron `0 0 23 * * ?`).
  - Admin endpoint `POST /api/daily-headers/{date}/recompute`.
  - Ledger daily-header row gets an Admin-only `Tính lại` button (ConfirmPopup → POST → toast).
- **FR-025 Officer name inline edit**:
  - `UpdateOfficerNameCommandHandler` + validator (today-only INV-11) + endpoint
    `PATCH /api/daily-headers/{date}/officer` (any-authenticated per FR-027).
  - SignalR `DailyHeaderOfficerChanged` broadcast on `EventsHub` so other clients converge.
  - Desktop optimistic edit with revert-on-failure; cache `_officerByDate` so existing
    officer names render after a refresh and the user's typed value isn't blanked
    on validation errors.
- **F-14 Reports**:
  - `IReportsRepository` + `ReportsRepository` projections:
    by-processor / by-yard / by-hour / by-officer / pending.
  - Five `GET /api/reports/*` endpoints with `SupervisorOrAbove`.
  - Five role-403 tests for Operator-on-each-report.
  - Rebuilt `ReportsPage` (wireframe § 6.6): date-range pickers,
    IncludeCancelled toggle, sortable columns per section, Sao chép (TSV
    → clipboard), Xuất Excel (ClosedXML to `~/Documents/B2-Reports-*.xlsx`).
  - Snapshot collections + `Task.Run` around `workbook.SaveAs` so a large
    export doesn't freeze the MAUI UI thread or stall SignalR delivery.
- **GET /api/daily-headers** — `ListDailyHeadersQueryHandler` returns
  `DailyHeaderDto[]` for a date range so the ledger can render the existing
  officer-name overlay (previously a blank field on every render).
- **ArchUnit**: `EndpointPolicyAlignmentTests` ensures mapped `/api/*`
  endpoints declare authorization or explicit anonymous policy. (Carry-over
  T-S4-CO-01 strengthens this to specific FR-027 policy alignment.)
- **Audit accuracy**: `UpdateOfficerNameCommandHandler` and
  `RecomputeDailySummaryCommandHandler` now branch by `isNew` so the
  immutable AuditLog records `Create` on first-of-day insertion and the
  appropriate Update / `OfficerNameChanged` on subsequent edits — no
  more `Update` rows on what is actually an `INSERT`.
- **Testing**:
  - `B2.Application.Tests/Jobs/DailyHeaderJobTests.cs` for F-11/F-12 scenarios.
  - `B2.Server.Api.Tests/Declarations/DailyHeadersEndpointTests.cs` covers
    FR-025 PATCH (today + audit row) and the new GET range endpoint.
  - `B2.Server.Api.Tests/Reports/ReportsEndpointsTests.cs` for F-14 scenarios + role-403 checks.

##### Round-2 review fixes folded into the same PR

- **CRITICAL — host crash**: `HttpAuditContext.Set` used to throw
  `InvalidOperationException("No HttpContext.")` when called outside the
  HTTP pipeline. The Quartz cron jobs and the startup catch-up host both
  call audit-emitting handlers — production Windows Service would have
  crashed at boot. Now no-ops gracefully outside HTTP per FR-003 ("audit
  binds to successful mutating requests"; cron-driven runs leave their
  trail in the data itself).
- **CRITICAL — Designer.cs sync**: 35+ resource keys (Reports_*,
  Sidebar_Item_Reports, Ledger_Header_OfficerPlaceholder, Common_Save)
  were only in `AppResources.vi.resx`. Designer.cs is the static accessor
  compiled into B2.Desktop, so the missing entries surfaced as Windows-only
  XamlC `XFC0101` and CS errors. Added all of them.
- **CRITICAL — UTF-8 BOM regression**: 7 Desktop files had BOMs introduced
  by Cursor's editor; `.editorconfig charset = utf-8` is enforced by
  `dotnet format` against the full B2.sln on Windows CI. Stripped them.
- **HIGH — analyzer cleanup**:
  - CA1305 in `ReportsViewModel.WriteSheet`: replaced
    `XLCellValue.FromObject(object)` with typed implicit conversions
    (no `IFormatProvider` dependency since values are already typed).
  - CA1859 in `LedgerViewModel.RebuildDisplayItems`: parameter type
    tightened from `IReadOnlyDictionary<DateOnly, string?>` to
    `Dictionary<DateOnly, string?>`.
- **Audit JSON consistency**: `RecomputeDailySummaryCommandHandler` and
  `UpdateOfficerNameCommandHandler` both emit before/after JSON via
  `JsonSerializer.Serialize` instead of hand-rolled string concat.
- **Carry-over** (`T-S4-CO-01..09`): items intentionally deferred from the
  PR-18 review — see `specs/001-b2-clearance/tasks.md` Phase 5 tail for the
  full enumerated list.

#### PR #6 — Sprint S3 partial (`e926982`)

- **F-09 KVGS Confirmation** — `Declaration.ConfirmKvgs` domain method
  enforcing INV-6 + state machine; `ConfirmKvgsCommandHandler`;
  `POST /api/declarations/{id}/kvgs-confirm` (`SupervisorOrAbove`).
  KvgsPage + KvgsViewModel with confirmation modal.
- **F-10 ledger query + realtime** — `ListDeclarationsQueryHandler`
  with paginated date / status / plate / yard / processor / officer /
  `since` / `includeCancelled` filters; `GET /api/declarations` and
  `/api/declarations/{id}`; `EventsHub` SignalR hub at `/hubs/events`
  with `SignalRLedgerDispatcher` publishing `LedgerChanged` for
  Create / Exit / KvgsConfirm / Cancel / OfficerNameChanged.
- **Desktop ledger** — LedgerPage / LedgerViewModel per wireframe
  § 6.2: sticky search row, daily-header rows, status badges
  (icon + color + text per Constitution VIII), date / status / yard /
  plate filters, "Chỉ tờ còn xe trong cổng" toggle, pagination, four
  list-view states.
- **FR-028 tri-state connectivity** — `ConnectivityService` driving
  ledger live updates and the new top-bar badge.
- **Wireframe § 5 app shell (T-S3-11 baseline)** —
  `MainShellPage : ContentPage` + Grid layout, tri-state badge,
  user menu (Đổi mật khẩu / Đăng xuất), F1 cheat-sheet modal,
  status bar with `Mã phiên` from a new `ISessionCorrelationService`
  populated by a `CorrelationCaptureHandler` on every API response.
- **Service consolidation (T-S3-12 baseline)** — Entry / Exit /
  MasterData ViewModels migrated off direct `Toast.Make` and
  `page.DisplayAlert` to `IToastService` / `IModalService` per
  Constitution VIII.

#### PR #7 — Round 1 audit follow-up (`797a987`)

- **FR-028 audit log** — `ConnectivityService.Mode` setter now emits
  a Serilog `Information` event on every state transition with
  `{Previous}` and `{Current}` modes. Spec FR-028 demands this; was
  previously absent.
- **F-10 Scenario 3 test** — `Should_accept_writes_during_polling_fallback_and_surface_in_since_delta`
  added to `ListDeclarationsEndpointTests.cs` (the spec lists 4
  acceptance scenarios; only 3 had server-side coverage). F-10 trait
  count: 5 → 6.
- **Constitution VI tokens** — `Typography.xaml` matches wireframe § 3
  fully (`FontSizeXS=12`, `FontSizeL=18`, `FontSizeXXL=28` newly
  added); `FontSizeSearchInput=24` for the § 6.4 special case. Two
  hard-coded `FontSize` literals (`ExitPage.xaml`, `Admin/UsersPage.xaml`)
  now reference tokens.

#### PR #8 — Round 2 audit follow-up (`d41c607`)

- **Toast severity + timing (wireframe § 7)** — new `ToastSeverity`
  enum (Info/Success/Warning/Error) and overload
  `IToastService.ShowAsync(message, severity, ct)`. `ToastService`
  switched from CT.Maui Toast to Snackbar so duration is configurable:
  4 s Success/Info, 8 s Warning, manual close via "Đóng" action button
  for Error. Entry / Exit / KVGS toast call sites tagged
  `ToastSeverity.Success`.
- **Modal popup with focus trap (wireframe § 7)** — `IModalService`
  now backed by `CommunityToolkit.Maui.Views.Popup` (`ConfirmPopup`,
  `InfoPopup` under `Services/Popups/`). Focus trap, Esc dismissal,
  backdrop tap-to-close, return-focus-to-trigger are enforced by
  CT.Maui's Popup natively. `ConfirmAsync` gets a `destructive` bool
  (default false) that suppresses backdrop dismissal for destructive
  flows per § 7.

#### PR #11 — Round 3b: functional Cỡ chữ dropdown (`90d6b00`)

- **`IUserSettingsService` + `FontScaleMode`** — singleton MAUI
  Preferences-backed service under `Services/UserSettings/`. Three
  modes: Normal / Large / ExtraLarge per wireframe § 5 + § 9.
- **App.xaml.cs startup** mutates `Application.Current.Resources`
  `FontSize*` tokens BEFORE resolving AppShell, so the first XAML
  parse sees scaled values. Offsets +0 / +4 / +8 across the token
  set per wireframe § 3 (body 16 → 20 → 24).
- **Status bar Cỡ chữ Picker** in `MainShellPage` replaces the static
  Label. On change: save preference + show Warning toast
  "Đăng nhập lại để áp dụng" (StaticResource bindings already
  parsed in the current session can't pick up the new value).

#### PR #12 — Round 3c: KVGS row fade + self-monitoring counter (`d0d47de`)

- **`LedgerRowViewModel` → `ObservableObject`** with a bindable
  `Opacity` property (default 1.0). KvgsPage row template binds
  `Opacity="{Binding Opacity}"` so a VM-driven tween drives the visual
  fade.
- **KvgsViewModel** runs a 300 ms / 12-frame VM tween on the
  confirmed row before refreshing the list (wireframe § 6.5 animated
  row removal). Per-session counter `ConfirmedTodayCount` increments
  on success and surfaces via `ConfirmedTodayLabel` (resx
  `Kvgs_ConfirmedToday_Format`). KvgsPage refresh-button row becomes
  a 3-column Grid with the counter aligned right.
- **LedgerVM `OnPageChanged`** also notifies `DisplayRangeText`
  (pagination summary "Hiển thị X–Y của N") so the value isn't
  stale-by-default on page navigation.

#### PR #15 — Round 3d: ledger drawer chi tiết + keyboard shortcuts (`14c39b2`)

- **Right-side detail drawer (420 px)** — `LedgerPage` outer Grid wraps
  the existing content into a 2-column layout with a drawer in col 1
  (`IsVisible` bound to a new `IsDrawerOpen` ObservableProperty).
  Double-tap a data row → fires `OpenDetailCommand` which calls
  `GET /api/declarations/{id}` and shows declaration details
  (number / status / processor / yard / company / goods) plus a
  per-vehicle list with Trong cổng / Đã ra status tags.
- **`LedgerViewModel`** — new `SelectedDeclarationDetail`,
  `IsDrawerOpen`, `IsDrawerLoading`, `DrawerErrorMessage`
  ObservableProperties; computed `HasDrawerData` for the inner
  content visibility; `OpenDetail` / `CloseDetail` RelayCommands.
- **Keyboard shortcuts on `LedgerPage`** —
  `Microsoft.UI.Xaml.Input.KeyboardAccelerator` registered on first
  `Loaded`: `F5` → refresh; `/` (`VK_OEM_2` = 0xBF) → focuses the
  search Entry; `Esc` → closes the drawer if open, else clears the
  search.
- 14 new `Ledger_Drawer_*` strings in resx. Mirrors the
  `MainShellPage` accelerator pattern from R3a.

#### PR #16 — Round 3e: list-view skeletons + correlationId on error banners

- **`Views/Common/SkeletonRows`** — shared `ContentView` with five
  staggered-opacity grey bars (32 px tall) replacing the previous
  spinner. Constitution VIII: defined once, dropped into every list
  page's loading branch (Ledger / Kvgs / Users / MasterData / Exit).
- **`Views/Common/ErrorCodeBanner`** — shared "Mã lỗi: {id}"
  HorizontalStackLayout that hides itself when there's no correlation
  id. Hosting page binds `BindingContext="{Binding Correlation}"` to
  attach to its VM's `ISessionCorrelationService`.
- **VM constructors** — Ledger / Kvgs / Users / MasterData / Exit
  ViewModels now take `ISessionCorrelationService` and expose it as
  a `Correlation` property for the banner. Same singleton the status
  bar already uses.
- New resx `Common_ErrorCode_Prefix` ("Mã lỗi:").

#### PR #13 — CI workflow Windows shell-bash fix (`2068373`)

- The Windows `build-and-test` job hit
  "Unable to locate dotnet CLI" three times in a row (PR #9 first
  run, post-merge R3b, post-merge R3b re-run) at the
  "Verify formatting" step even when the immediately preceding
  "Restore" step ran dotnet successfully. `setup-dotnet@v5` writes
  to `GITHUB_PATH`, but the default pwsh shell on `windows-2022`
  intermittently loses the propagated PATH between steps.
- Pinned every dotnet step in the Windows job to `shell: bash`
  (Git Bash). Switched MSBuild args from `/p:` to `-p:` everywhere
  to avoid Git Bash MSYS2 path translation
  (MSB1008 "Only one project can be specified" otherwise).

#### PR #9 — Round 3a audit follow-up (`577d388`)

- **Wireframe § 5 sidebar (220 px)** — `MainShellPage` replaces the
  temporary horizontal tab strip with a vertical sidebar. Items
  follow FR-027 / sidebar matrix; active item shows a 4-px
  `ColorPrimary` left bar + bold label. New Vietnamese sidebar item
  strings (📋 Sổ B2 / ➕ Nhập / 🔍 Xe ra / ✅ KVGS / 🗂️ Danh mục /
  👥 Người dùng) live in `AppResources.vi.resx`.
- **Windows keyboard accelerators (§ 5)** — `F1` opens the cheat
  sheet; `Alt+1..Alt+8` activates the N-th visible sidebar item.
  `Microsoft.UI.Xaml.Input.KeyboardAccelerator` attached to the
  page's `PlatformView` on `Loaded`. Out-of-range `Alt+N` is a
  no-op so role-restricted sidebars don't spuriously navigate.
- **Connectivity badge clickable** — tap opens an info popup with
  state-specific Vietnamese description (`Connectivity_Detail_Online`
  / `_Polling` / `_Offline`). Health-page nav for Admin is a follow-up.
- **Tokens (Constitution VI)** — `ColorSidebarBg` (#FAFAFC) and
  `ColorBorderFocus` (#1A73E8) added to `Colors.xaml`. Style sweep
  to apply the focus ring across controls is its own commit.

#### Other

- `CLAUDE.md` § "Pre-push gates" — locally runnable subset of the CI
  gates plus a "macOS analyzer trap" callout documenting the
  `CompositeFormat` / `CultureInfo.CurrentCulture` / `IDisposable`
  patterns required by the `B2.Desktop` analyzers (CA1305 / CA1863 /
  CA1001) that only run on the Windows CI runner.
- `specs/001-b2-clearance/quickstart.md` § 7 — two new troubleshooting
  rows (UTF-8 BOM `error CHARSET` and the macOS-vs-CI Desktop analyzer
  divergence) with concrete remediation steps.
- `ISessionContext` — singleton identity holder populated on login,
  cleared on logout, surfaced in the user-menu button label.
- `IAppNavigator` — abstracts the "send the user back to login"
  navigation so it works from both the pre-login
  `Shell.Current.GoToAsync("//login")` path and the post-login
  `MainPage = AppShell` path.

### Changed

- Tab pages (Ledger / Entry / Exit / Kvgs / MasterData / Users)
  converted from `ContentPage` to `ContentView` so they can be hosted
  inside the `MainShellPage` grid; `OnAppearing` init logic moved to
  one-shot `Loaded` handlers.
- `IMainShellFactory.CreateMainShell` no longer takes a `UserRoleDto`
  — role is now read from `ISessionContext`. `ChangePasswordViewModel`
  uses `IAppNavigator` so the post-login voluntary password change
  works outside `Shell.Current`.
- `CLAUDE.md` "Active phase" updated to **Sprint S3** (S0 + S1 + S2
  merged to `main`). "Recent Changes" log extended with PR #4 (S1),
  PR #5 (S2), PR #6 (S3 partial), PR #7 (R1), PR #8 (R2), PR #9 (R3a)
  merge entries.

### Fixed

- **Sprint S3 (backend slice)**: `GET /api/declarations` reads query
  parameters via `HttpRequest.Query` (avoids minimal-API binding
  returning HTTP 400 with an empty body).
  `DeclarationRepository.ListDeclarationsAsync` avoids SQLite EF
  limitations on `DateTimeOffset` in `ORDER BY` and on `since` /
  vehicle time predicates (in-memory sort + `since` filter, plate
  filter via `IN` subquery). SignalR integration test connects
  through `WebApplicationFactory.Server` with
  `HttpMessageHandlerFactory` so negotiation uses the in-memory
  test server instead of `localhost:80`.
- **Round 1 fix** — `KvgsViewModel` was missing `using System.Text;`
  for `CompositeFormat`; only the Windows runner caught it.
- **Round 2 fix** — `Page.ShowPopupAsync` returns `Task<object?>` in
  CT.Maui v9 (not `Task<IPopupResult>`); `result is true` pattern
  match instead of `.Result` accessor.
- **Round 3a fix** — `VirtualKeyModifiers` lives in `Windows.System`,
  not `Microsoft.UI.Xaml.Input`. CS0234 from the Windows runner.

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
