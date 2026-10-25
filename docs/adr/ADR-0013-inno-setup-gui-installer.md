# ADR-0013: GUI setup.exe built with Inno Setup as a thin wrapper over install.ps1

**Status**: **Accepted** — ratified by the owner on 2026-10-25 with PR #215.
**Date**: 2026-10-25
**Relates to**: #214 (simplify the installer), ADR-0007 (self-contained xcopy deployment, no
update framework), ADR-0009 (initial Admin password shown once at install), ADR-0010 (LAN TLS
certificate; exit gate trusts `b2-server.cer`), `specs/01-overview.md` § 6 (locked stack).

## Context

Since #103 the gate PCs are installed from an offline `.zip` by running `deploy/install.ps1` in
an elevated PowerShell. The installer works, but the people running it at Chi Ma are not
developers, and most of the failure rows in `docs/huong-dan-cai-dat.md` § 7 come from the
mechanics rather than the product: opening PowerShell as Administrator, `Set-ExecutionPolicy`,
and typing `-ClientOnly -ServerUrl … -ServerCertificate …` correctly on the exit gate.

#214 removes most of that inside the existing scripts — WebView2 bundled in the release, a
`CaiDat.cmd` that self-elevates, and an `install.ps1` that infers the role on upgrade, asks on a
fresh install and finds `b2-server.cer` on the USB. What remains is that operators expect
"double-click `setup.exe` → Next → Next", with the role, server IP and certificate chosen in a
window rather than a console menu.

Options considered:

- **Stay with `CaiDat.cmd` only** — no new tool, but no GUI and no single-file download.
- **WiX / MSI** — the Windows-native format, but an MSI that registers a service, issues a
  certificate and shows one-time secrets means custom actions that duplicate `B2Clearance.exe`,
  plus a much larger authoring and test surface for a two-PC deployment.
- **Inno Setup** — a free, widely used setup compiler that produces one `setup.exe` from a
  script, with Pascal scripting for custom wizard pages and process execution.

## Decision

Build `B2Clearance-<version>-setup.exe` with **Inno Setup 6** (`deploy/setup.iss`) in
`release.yml`, alongside the existing `.zip`, as a **thin wrapper over `install.ps1`**:

- Wizard pages: gate role → (exit gate only) server IP and `b2-server.cer` (pre-filled when
  found next to `setup.exe` or at a USB root). On upgrade the role page is skipped and the saved
  server address is pre-filled, matching `install.ps1`'s own upgrade inference.
- The payload is extracted to `{tmp}` only (`CreateAppDir=no`); `setup.exe` then runs
  `install.ps1 -Role … -PauseAtEnd` from there in a **visible console** and checks its exit code.
  The console is deliberate: the recovery code (FR-026) and the initial Admin password
  (ADR-0009) are printed there once and wait for Enter, exactly as in the script path.
- 64-bit install mode, so `{sys}` resolves to the native System32 and the script runs under
  64-bit PowerShell (32-bit PowerShell would see `Program Files (x86)`).
- `Uninstallable=no`: removal stays `uninstall.ps1`, which keeps `C:\ProgramData\B2`.

Inno Setup is a **build-time tool only**: it is installed on the release runner (preinstalled
image tool, or `choco install innosetup` as a fallback) and nothing of it runs on the gate PC
except the generated `setup.exe`. No application dependency changes; the `.NET` stack in § 6 is
untouched. This ADR exists because Constitution II requires one for any tool outside § 6.

## Consequences

- Install logic stays in one place (`install.ps1` → `B2Clearance.exe`). `setup.exe`, `CaiDat.cmd`
  and a typed `.\install.ps1` all exercise the same code, and the `.zip` remains the fallback.
- `setup.exe` is **not code-signed** (no signing certificate yet). Windows SmartScreen may warn
  "Windows protected your PC" on first run; the install guide tells the operator to choose
  *More info → Run anyway*. Signing is a follow-up once a certificate exists.
- The release now uploads two assets of roughly the payload's size each.
- The wizard and `.iss` cannot be built or run on macOS; changes to `setup.iss` must be checked
  on Windows (the dry-run VM procedure in `docs/deployment-offline.md`) before a release.
- The wizard's built-in Inno Setup messages are overridden in Vietnamese only for the pages the
  flow shows; rarely seen system messages (e.g. disk-space errors) remain in English.
