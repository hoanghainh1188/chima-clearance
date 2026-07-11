# Bug-finding automation notes

## 2026-07-11 run (`cursor/critical-bug-investigation-5a14` @ 5c8b9d7 → cba3202)

### Critical fixes (PR #95)

**1. Annual archive verify gate (FR-021 / INV-12 — data loss)**

- **Trigger**: Annual archive job exports year N with `count` declarations; workbook is corrupt/header-only; `VerifyExcel(path, count)` returned true because it only checked `dataRows >= 1`; live rows deleted.
- **Fix**: Count STT rows in column A; require `declarationRows >= minimumRows`.
- **Tests**: `FileSystemArchiveWriterTests` (3 cases).

**2. Un-exit vs cancel TOCTOU (FR-022)**

- **Trigger**: Admin concurrent `CancelDeclarationAsync` + `UnExitVehicleAsync` on same declaration; cancel used Serializable tx, un-exit did not → cancelled declaration could end up with `ExitMarked=false`.
- **Fix**: Serializable transaction on `UnExitVehicleAsync`.
- **Tests**: `Should_not_un_exit_when_concurrent_cancel_commits`.

**3. Un-exit on KVGS-confirmed (F-08 — ledger corruption)**

- **Trigger**: Admin `POST /api/vehicles/{id}/un-exit` on KVGS-confirmed declaration; `RecomputeStatus` no-ops for KVGSConfirmed → `ExitMarked=false` but `Status=KVGSConfirmed`.
- **Fix**: Domain guard `DECLARATION_LOCKED` in `Vehicle.UnExit()`.
- **Tests**: domain + `Should_reject_un_exit_on_kvgs_confirmed_declaration`.

### Reviewed, below PR bar

- UAT commits #64/#65 re-reviewed — no additional critical issues.
- `IdempotencyKeyEvictionJob` fix in #65 was operational (job failing every run), not data loss.

## 2026-06-02 run (`cursor/critical-bug-investigation-25fa` @ 53b663b + fix)

### Critical fix (PR opened)

**F-15 restore write window (data loss)**

- **Trigger**: Admin calls `POST /api/admin/restore` → `DatabaseRestoreService.Stage` writes `b2.db.restore-pending` → `StopApplication()` → graceful shutdown (seconds). Operators can still POST declarations/exit during that window; on next startup `ApplyPending` replaces the DB with the snapshot — post-stage writes vanish.
- **Spec**: `specs/03-functional-spec.md` F-15 / `specs/02-architecture.md` § 9 — "service stops accepting writes".
- **Fix**: `RestoreWriteBlockMiddleware` returns 503 `RESTORE_PENDING` for mutating verbs while pending file exists; `ApplyPending` uses `File.Replace` instead of delete-all-then-move.
- **Tests**: `Should_reject_mutating_requests_while_restore_is_pending` + existing restore suite (10 tests).

### Reviewed, below PR bar

- **Idempotency parallel race** (commit `408fe67` on orphan branch): Desktop uses new `Guid` per submit; concurrent same-key retry is rare.
- **ApplyPending** crash between delete and move — mitigated by `File.Replace` in same fix.
- **BackupSecretsStore** regenerates secrets if DPAPI load fails — requires machine/key corruption.
- **DailyHeader catch-up** does not fill internal date gaps — Admin recompute exists.
