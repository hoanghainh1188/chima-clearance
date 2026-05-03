# ADR-0005: Litestream for continuous backup

**Status**: Accepted
**Date**: 2026-05-03
**Supersedes**: none

## Context

The entry-gate PC is a single point of failure (constraint C5; per ADR-0001 +
ADR-0002 we accept this and mitigate via continuous backup). The customs unit has
**no IT team** to operate a backup orchestrator (constraint C3), and **no internet**
to upload off-site (constraint C1). Recovery objective: latest backup ≤ 60 s old,
restore complete in ≤ 5 min (`SC-009`).

## Decision

Adopt **Litestream** as the continuous backup mechanism, running as a **child
process supervised by `B2.Server.Service`**:

- Replicates SQLite WAL frames every **60 s** to two configured targets:
  - `D:\B2-Backup\replica\` (USB stick / external drive)
  - `\\entry-pc\B2-Backup\` (LAN folder on a supervisor PC)
- On-demand snapshots are produced by the API (`POST /api/admin/backup-now`) using
  `sqlite3 .backup`, packaged as `b2-YYYYMMDD-HHmm.bak.zip`, encrypted with
  **AES-256-GCM** (a single random data key wrapped twice — once with the
  install-time password, once with the printable recovery code per FR-026), and
  written to every backup target.
- Restore (`POST /api/admin/restore`) accepts either secret, swaps the DB file,
  restarts the service. Downtime ≤ 30 s (`SC-009`).
- Health page (F-18) surfaces the last-replica timestamp per target.

## Consequences

**Positive**:

- Zero per-second writes lost beyond the 60-s replication window.
- Two physically distinct targets (USB + LAN) tolerate single-target loss.
- Single binary, single supervised process — operable by non-IT staff (C3).
- AES-256 + key-wrap means the backup zip is unreadable without one of the two
  install-time secrets (FR-026).

**Negative / accepted**:

- Litestream is a separate Go binary bundled by the installer. The team must
  pin the version (`docs/deps/litestream.version`) and update via a planned
  release.
- Recovery code lost AND install password lost = backups unrecoverable. This is
  by design (`FR-026` + spec Clarifications round 1).
- Restore-while-other-users-logged-in causes a forced reconnect. Acceptable per
  spec Edge Case "Restore while another user is logged in".

## Alternatives considered

- **rsync / robocopy of the .db file** — rejected: SQLite WAL means a hot-copy
  is inconsistent. Litestream understands WAL frames and produces consistent
  replicas.
- **Daily `.dump` script** — rejected: too coarse-grained (worst-case 24 h of
  data loss) and re-replays every row on restore (slow).
- **External managed backup** — rejected: violates C1.
- **Manual backup only** — rejected: human forgets; violates C3.

## References

- `specs/01-overview.md § 6` tech-stack table
- `specs/02-architecture.md § 9` backup/restore architecture
- `specs/04-implementation-plan.md § 12` runbook (S6 deliverable)
- `spec.md` FR-026 (recovery code)
- `.specify/memory/constitution.md` Principles I + VII
