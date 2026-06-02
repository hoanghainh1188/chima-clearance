# Bug-finding automation notes

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
