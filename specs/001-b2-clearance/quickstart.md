# Quickstart — Chima Customs B2

**Feature**: `001-b2-clearance` · **Audience**: developers + AI coding agents about
to start work on Sprint S0 (Bootstrap) per [`plan.md`](./plan.md). Operator-facing
documentation lives in `docs/runbook.md` (S6 deliverable).

---

## 1. Local prerequisites

Install once on every dev machine:

| Tool | Version | Why |
| --- | --- | --- |
| Windows 10/11 64-bit, **OR** macOS / Linux for libraries-only work | — | MAUI Windows build requires Windows; everything else builds anywhere. |
| .NET SDK | **8.0.x** (LTS) | All projects target net8.0 / net8.0-windows10.0.19041.0 |
| MAUI workload | latest | `dotnet workload install maui` |
| Git | 2.40+ | Standard |
| (Windows only) Visual Studio 2022 17.10+ **OR** VS Code with C# Dev Kit | latest | XAML hot reload, .NET MAUI tooling |
| (optional) `litestream` CLI | 0.3+ | Manual replication tests; production binary is bundled by the installer |
| (optional) `gitleaks` | latest | Pre-commit secret scan (CI runs it too) |

> **CI parity**: GitHub Actions uses `windows-2022` + `actions/setup-dotnet@v4` with
> `dotnet-version: 8.0.x` and runs `dotnet workload install maui` (per
> `04-implementation-plan.md § 7`). Pin your local SDK to whatever CI uses.

---

## 2. Clone & build

```bash
git clone <repo> chima-clearance
cd chima-clearance

# The solution does not exist until Sprint S0 — see § 3 below for the bootstrap.
# After S0 is done, the working loop is:
dotnet restore
dotnet format --verify-no-changes
dotnet build -c Release /p:TreatWarningsAsErrors=true
dotnet test  -c Release
```

Coverage thresholds are enforced by CI per Constitution Principle IV: aggregate
≥ 80 %, `B2.Domain` ≥ 90 %, `B2.Application` ≥ 80 %.

---

## 3. Bootstrap commands (Sprint S0)

The exact commands from `04-implementation-plan.md § 3.5`, kept here so the bootstrap
agent has a single command list to run:

```bash
dotnet new sln -n B2

dotnet new classlib -n B2.Domain                  -o src/B2.Domain
dotnet new classlib -n B2.Application             -o src/B2.Application
dotnet new classlib -n B2.Infrastructure          -o src/B2.Infrastructure
dotnet new webapi   -n B2.Server.Api              -o src/B2.Server.Api  --use-minimal-apis
dotnet new worker   -n B2.Server.Service          -o src/B2.Server.Service
dotnet new classlib -n B2.Shared.Contracts        -o src/B2.Shared.Contracts
dotnet new maui     -n B2.Desktop                 -o src/B2.Desktop

dotnet new xunit    -n B2.Domain.Tests            -o tests/B2.Domain.Tests
dotnet new xunit    -n B2.Application.Tests       -o tests/B2.Application.Tests
dotnet new xunit    -n B2.Server.Api.Tests        -o tests/B2.Server.Api.Tests
dotnet new xunit    -n B2.Desktop.Tests           -o tests/B2.Desktop.Tests
dotnet new xunit    -n B2.Performance.Tests       -o tests/B2.Performance.Tests

# Wire up the dependency graph (see plan.md > Project Structure):
#   Application      -> Domain
#   Infrastructure   -> Application, Domain
#   Server.Api       -> Application, Infrastructure, Shared.Contracts
#   Server.Service   -> Server.Api
#   Desktop          -> Shared.Contracts (only)
#   *.Tests          -> the project they test
# Use `dotnet add <project> reference <path>` for each edge.
```

Other S0 deliverables (see `plan.md` Constitution Check):

1. `Directory.Build.props` enabling `<Nullable>enable</Nullable>`,
   `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, `<LangVersion>latest</LangVersion>`.
2. `.editorconfig` (style + naming) + `dotnet format --verify-no-changes` in CI.
3. `.github/workflows/ci.yml` per `04-implementation-plan.md § 7`.
4. **Six** ADRs under `docs/adr/`:
   - `ADR-0001-modular-monolith.md`
   - `ADR-0002-sqlite-as-primary-store.md`
   - `ADR-0003-dotnet-maui-as-desktop-ui.md`
   - `ADR-0004-jwt-rs256-with-local-user-store.md`
   - `ADR-0005-litestream-for-continuous-backup.md`
   - `ADR-0006-api-surface-extensions.md` *(see [`research.md § 8`](./research.md))*
5. `B2.Installer` skeleton (Velopack config or Inno Setup script — no real installer
   yet).
6. `README.md` (how to build) + `CHANGELOG.md` v0.0.1.
7. ArchUnit dependency-rule test in `B2.Server.Api.Tests` asserting Constitution V.

After S0: `dotnet build` and `dotnet test` (empty but green) MUST both pass.
`dotnet format --verify-no-changes` MUST report no diffs.

---

## 4. Running the system locally (after Sprint S2 deliverables exist)

### 4.1 Bring up the API

```bash
# In one terminal — run the Windows Service host as a console app for dev:
dotnet run --project src/B2.Server.Service -- --console
# Listens on https://localhost:5001 (dev cert; see `dotnet dev-certs https --trust`).
```

### 4.2 Bring up the Desktop

```bash
# In another terminal:
dotnet build src/B2.Desktop -t:Run -f net8.0-windows10.0.19041.0
```

Initial credentials (F-01, ADR-0009): there is no shared default password. On a dev
machine, stage one before the first start (non-Windows dev file, applied once and deleted):

```bash
DATA="$HOME/Library/Application Support/B2"   # macOS; Linux: ~/.local/share/B2
mkdir -p "$DATA/keys"
echo '{"Password":"Dev-Admin-Pass-2x"}' > "$DATA/keys/admin-bootstrap.dev.json"
```

Then log in as `admin` / `Dev-Admin-Pass-2x` — forced password change on first login
(FR-017). Production installs get a random password from `B2Clearance.exe install`.

### 4.3 Smoke walk

After Sprint S2 (Entry & Exit happy path), the dev-loop check is:

1. Login → forced password change → login again.
2. Add 1 Processor + 1 Yard via Admin → Master Data.
3. Create a declaration with plate `12h05769` (server uppercases to `12H05769`).
4. Add a second vehicle with plate `29A12345`.
5. Search suffix `5769` → finds the first vehicle.
6. Mark exit on the first vehicle → row text turns black.
7. Mark exit on the second → declaration auto-promotes to `AllExited` (parent row
   green).
8. (Sprint S3) On a second client, observe the green row appearing within 1 s
   (SignalR push).
9. (Sprint S5) Export Excel → file matches the legacy Sổ B2 layout.

---

## 5. Spec-VM smoke checklist (release gate)

The release agent / human MUST run the full 10-step checklist on the spec VM before
tagging a release. Reproduced verbatim from `04-implementation-plan.md § 8`:

> **Spec VM**: Win 10 64-bit (21H2), 2 vCPU, 4 GB RAM, .NET runtime NOT pre-installed
> (the installer must bring it). Office 2010 only relevant for the Excel handoff.

1. Run installer, accept defaults → service is created and starts.
2. Open Desktop on the same VM, log in as default Admin → change password forced.
3. Add 1 Processor + 1 Yard.
4. Create 1 declaration with 2 vehicles.
5. Search by 4-digit suffix → finds them.
6. Mark both exits → declaration goes green.
7. Confirm KVGS → green clears.
8. Export Excel → file opens in Excel 2010 (or LibreOffice as proxy) without errors.
9. "Backup now" → file appears in the backup folder.
10. Stop service → desktop shows clear "Mất kết nối" with retry.

---

## 6. Useful commands during development

| Goal | Command |
| --- | --- |
| Add an EF Core migration | `dotnet ef migrations add <Name> -p src/B2.Infrastructure -s src/B2.Server.Service` |
| Apply migrations to the dev DB | `dotnet ef database update -p src/B2.Infrastructure -s src/B2.Server.Service` |
| Open the SQLite DB in a viewer | DB at `C:\ProgramData\B2\db\b2.db`; use `litecli` or DB Browser for SQLite |
| Run NBomber perf scenarios | `dotnet test tests/B2.Performance.Tests -c Release --filter Category=perf` |
| Tail Serilog file logs | `Get-Content C:\ProgramData\B2\logs\b2-yyyymmdd.log -Wait -Tail 50` (PowerShell) |
| Trigger a manual backup | `curl -k -X POST https://localhost:5001/api/admin/backup-now -H "Authorization: Bearer …"` |
| Restore the dev DB to a clean state | Stop service → delete `C:\ProgramData\B2\db\b2.db*` → start service (migrations recreate schema + seed) |

---

## 7. Where to look first when something is wrong

| Symptom | First check |
| --- | --- |
| Build fails with "MAUI workload not installed" | `dotnet workload install maui` |
| `dotnet format` reports `error CHARSET: Fix file encoding` | New file was saved as UTF-8 **with** BOM. Re-save without BOM, or strip it: `perl -i -pe 'BEGIN{$f=1} s/^\xef\xbb\xbf// if $f; $f=0' <file>`. |
| `B2.Desktop` builds green on macOS but CI is red with `CA1305` / `CA1863` / `CA1001` | macOS cannot install the MAUI workload, so the `B2.Desktop` analyzers only run on the Windows CI runner. Apply the patterns in `CLAUDE.md` § "macOS analyzer trap" (cached `CompositeFormat`, `CultureInfo.CurrentCulture`, `IDisposable` for `CancellationTokenSource`). Run `dotnet format B2.NoMaui.slnf --verify-no-changes --no-restore` before push. |
| `TreatWarningsAsErrors` blocks build | Fix the warning. Do not flip the property — Constitution III is non-negotiable. |
| Test fails with "Should_<criterion>" naming convention | Either the test's `[Trait("Feature","F-XX")]` tag is wrong or the corresponding Gherkin AC moved — re-read `spec.md`. |
| Desktop shows 🔴 "Mất kết nối" | Service may not be running on `:5001`; firewall may be blocking; cert may not be trusted. See `docs/runbook.md` (S6) for the operator-facing version. |
| Declaration won't promote to `AllExited` | Re-run `Declaration.RecomputeStatus()` mentally over its children. Likely an INV-5 unit test will reproduce it; add it to `B2.Domain.Tests`. |
| Coverage drops below 80 % | Add tests, do **not** suppress the gate. Per-layer thresholds: Domain 90 %, Application 80 %. |

---

## 8. References

- [`spec.md`](./spec.md) — feature contract (18 features, FR-001..FR-028, NFR-001..NFR-006)
- [`plan.md`](./plan.md) — this iteration's plan, Constitution gate decisions
- [`research.md`](./research.md) — decisions, state machines, ADR-0006 draft
- [`data-model.md`](./data-model.md) — entities, invariants, retention
- [`contracts/openapi.yaml`](./contracts/openapi.yaml) — API surface (locked + ADR-0006)
- [`../01-overview.md`](../01-overview.md) — constraints C1–C8, locked tech stack
- [`../02-architecture.md`](../02-architecture.md) — sequences, ERD, API table
- [`../04-implementation-plan.md`](../04-implementation-plan.md) — sprint plan, DoD
- [`../05-ui-wireframes.md`](../05-ui-wireframes.md) — 12 wireframes, design tokens
- [`../../.specify/memory/constitution.md`](../../.specify/memory/constitution.md) — v1.1.0
