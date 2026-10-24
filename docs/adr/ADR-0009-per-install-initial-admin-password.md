# ADR-0009: Per-install initial Admin password

**Status**: **Accepted** — decisions ratified by the owner 2026-09-27.
**Date**: 2026-09-27
**Supersedes**: ADR-0004, *in part* — only the "Default Admin … password `Change-Me-Now-1!`" bullet.
**Relates to**: ADR-0004 (JWT + local user store), FR-017, FR-026, ADR-0008 (docs PDF tool)

## Context

Migration `0001_BaselineSchema` seeds the `admin` account with the same BCrypt hash in every
install (`Change-Me-Now-1!`, `MustChangePassword = true`). Since #114 the entry-gate service
listens on `0.0.0.0:5000` so the exit-gate PC can reach it, which means that between install and
the first login **any machine on the LAN** can sign in as Admin with a password that is written in
the repository, the test suite and ADR-0004. #125 moved it out of the shipped docs into a separately
handed-over PDF, which hides it but keeps it shared and known.

The installer (`B2Clearance.exe install`) already runs elevated, interactively, and shows the FR-026
recovery code on screen — the natural moment to hand the operator a secret. It is a separate
process from the service, and on a fresh install the database does not exist yet when it runs.

## Decision

1. **The installer generates a random initial Admin password per install** —
   `InitialPasswordGenerator` (16 characters in 4 groups joined by `-`, no `I l O o 0 1`,
   ≈ 91 bits) — and shows it **on screen only**, in a box next to the recovery code, with an
   Enter prompt. It is never written to a plaintext file.
2. **Hand-off by sealed file.** The installer seals `{"Password": …}` with DPAPI (LocalMachine) to
   `C:\ProgramData\B2\keys\admin-bootstrap.dpapi` (protected by the data-directory ACL, #128). On
   start, after migrations, the service (`AdminPasswordBootstrapper`) applies it with
   `MustChangePassword = true`, clears any lockout, writes an audit row, and deletes the file
   *after* saving. The installer never opens the database.
3. **A password a person chose is never overwritten.** `keys\admin-password.state` records
   `initial` / `rotated`, and **`rotated` is one-way**:
   - `ChangePasswordCommandHandler` writes `rotated` the moment `admin` changes its password;
   - on start, `initial` holds only while `MustChangePassword` is still set; with no state yet
     (install older than F-01) the account counts as initial **only** if its hash is exactly the
     migration-0004 placeholder — any other hash was set by a person;
   - a reset by another Admin sets `MustChangePassword` again but can never downgrade `rotated`
     (deriving "initial" from `MustChangePassword` alone would allow that — found in security
     review).

   The installer issues a new password only when the state is not `rotated`; the service ignores
   and deletes a pending password for a rotated account.
4. **Upgrades rotate a still-shared password.** Upgrading an install whose Admin never changed the
   seed password issues a new one. Installs from before this ADR have no state file; the installer
   then issues one and says it only applies if the Admin never changed their password — the service
   enforces that.
5. **The shared seed is retired** by migration `0004_RetireSharedAdminSeedHash`: the seeded hash is
   replaced by a well-formed BCrypt hash of a discarded random value, only where the seed is
   untouched. `Down` is intentionally a no-op.
6. **Recovery before first login:** `B2Clearance.exe reset-initial-admin` re-issues the initial
   password and restarts the service, refused once the state is `rotated` (then another Admin
   resets it, runbook § 2).
7. **Dev and tests** use the non-Windows plain file `keys/admin-bootstrap.dev.json`; the test
   factory stages a known password there, so tests exercise the real path and production gets no
   configuration switch.

## Consequences

- **+** No password is shared between installs or known in advance; the LAN exposure window only
  holds a password that exists on one screen.
- **+** No new dependency: BCL DPAPI/ACL APIs, existing BCrypt hasher, existing EF migration flow.
- **+** Upgrades close the hole on already-deployed PCs without touching Admins who rotated.
- **−** If the operator loses the password before the first login they must run
  `reset-initial-admin` (documented in the install guide and runbook).
- **−** An install-time secret now depends on the installer and the service agreeing on two file
  names and a JSON shape, duplicated across `B2.Installer` and `B2.Infrastructure` (the installer
  stays standalone by design). Covered by the API tests on the service side and by UAT on Windows.
- **−** Copying binaries by hand without running `install.ps1` after upgrading leaves the Admin on
  the retired seed hash with no pending password — nobody can log in as `admin` until
  `reset-initial-admin` is run. Upgrades are documented as "run `install.ps1`".
- The confidential install-guide PDF (ADR-0008) no longer needs to carry a password.

## Alternatives considered

| Option | Why not |
| --- | --- |
| Service generates the password on first start and writes it to a file | The service is headless (LocalSystem); the operator would have to find a plaintext file, and the installer prompt happens before the service starts. |
| Installer opens SQLite and sets the hash itself | Pulls EF/SQLite/BCrypt into the standalone installer, duplicates persistence logic, and does not work before the first migration. |
| Keep the shared password, only hide it (PDF) | Still identical across installs and known from the repository. |
| Force the password to be typed by the operator at install | Operators pick weak or reused passwords; a generated one meets the rules by construction and is shown once. |

## References

- FR-017, FR-026 (`specs/001-b2-clearance/spec.md`); Constitution VII.
- #114 (LAN binding), #125 (password out of shipped docs), #128 (data-directory ACL).
