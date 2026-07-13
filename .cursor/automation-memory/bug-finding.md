# Bug-finding automation notes

## 2026-07-13 run (branch `cursor/critical-bug-investigation-87ce` @ 5c8b9d7 → be59edb)

- No new commits on `main` since 2026-07-12 run.
- Re-validated three critical bugs still unfixed on main; cherry-picked verified fixes from `cursor/critical-bug-investigation-b5f7`.
- **PR #97 opened**:
  1. `FileSystemArchiveWriter.VerifyExcel` ignored `minimumRows` — annual archive data loss (FR-021).
  2. `UnExitVehicleAsync` lacked Serializable isolation — cancel/un-exit TOCTOU (FR-022).
  3. `Vehicle.UnExit` allowed KVGS-confirmed — ledger corruption (F-08).
- Tests: 120 passed, 2 skipped, 0 failed (`B2.NoMaui.slnf`).

### Reviewed, below PR bar (not fixed)

- **JWT session invalidation** — deactivated users / role changes / password reset do not revoke outstanding JWTs until expiry (spec M5). Real security gap; needs middleware design.
- **KVGS confirm vs add-vehicle race** — `ConfirmKvgsCommandHandler` lacks Serializable tx; concurrent add-vehicle on AllExited declaration can yield KVGSConfirmed with unexited vehicles.
- **Idempotency parallel race** — documented as below bar; Desktop uses fresh Guid per submit.

## 2026-07-12 run (branch `cursor/critical-bug-investigation-b5f7` @ 5c8b9d7 → a2f6c50)

Same three bugs; PR opened but not merged to main.

## 2026-06-24 run (`cursor/critical-bug-investigation-0dcf`)

### Critical fix (PR opened)

**FR-021 annual archive verification stub (data loss)**

- **Trigger**: `AnnualArchiveJob` runs on 1 Feb. `FileSystemArchiveWriter.VerifyExcel` ignored `minimumRows` and returned true whenever the workbook had ≥1 row (header only). A failed/partial export passed verification → `DeleteAsync` removed all declarations for that year from live DB while the archive file was empty or incomplete.
- **Root cause**: S6 T-S6-08 shipped a placeholder verifier (`return dataRows >= 1`) while the handler already passed the DB declaration count as `minimumRows`.
- **Fix**: Count declaration parent rows (column A STT) and require `declarationRows >= minimumRows`.
- **Tests**: `FileSystemArchiveWriterTests` (3 cases) + existing `AnnualArchiveJobTests`.

## 2026-06-06 run (branch `cursor/critical-bug-investigation-b9dd`)

Reviewed S5 merges on `main` (head `fcb0412`, PRs #40–#55).

### Fixed (PR #60)

1. **FR-022 cancel TOCTOU** — cancel could overwrite concurrent `AllExited` because handler loaded outside tx and `RowVersion` never bumps. Fix: `CancelDeclarationAsync` under `IsolationLevel.Serializable`.
2. **F-15 backup secrets** — DPAPI read failure caused silent secret rotation, undecrypting all `.bak` files. Fix: throw `BACKUP_SECRETS_UNREADABLE`.

### Also fixed later (PR #54/#57)

- Restore write-loss window — `RestoreWriteBlockMiddleware` + `File.Replace` in `ApplyPending`.
