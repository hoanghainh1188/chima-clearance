# Bug-finding automation notes

## 2026-06-09 run (`cursor/critical-bug-investigation-a90d` @ e4da343)

### Critical fixes (PR #68)

1. **FR-021 VerifyExcel bypass (data loss)** — `FileSystemArchiveWriter.VerifyExcel` ignored `minimumRows`; header-only archive passed verification → `DeleteAsync` wiped live data. Fixed by counting declaration rows (column C).
2. **F-08 KVGS un-exit** — Admin un-exit on `KVGSConfirmed` declaration left inconsistent state. Fixed domain guard `DECLARATION_LOCKED`.

`dotnet test B2.NoMaui.slnf`: 119 passed, 2 skipped.

### Below PR bar (not fixed)

- `ConfirmKvgs` + concurrent `UnExit` TOCTOU (no Serializable on either path)
- Idempotency middleware: handler commits before idempotency row persisted
- Annual archive export/delete not single DB snapshot

## 2026-06-06 run (branch `cursor/critical-bug-investigation-b9dd`)

Reviewed S5 merges on `main` (head `fcb0412`, PRs #40–#55).

### Fixed (PR #60)

1. **FR-022 cancel TOCTOU** — cancel could overwrite concurrent `AllExited` because handler loaded outside tx and `RowVersion` never bumps. Fix: `CancelDeclarationAsync` under `IsolationLevel.Serializable`.
2. **F-15 backup secrets** — DPAPI read failure caused silent secret rotation, undecrypting all `.bak` files. Fix: throw `BACKUP_SECRETS_UNREADABLE`.

### Validation

`dotnet test B2.NoMaui.slnf`: 102 passed, 2 skipped.

## 2026-06-02 run (`cursor/critical-bug-investigation-25fa` @ 53b663b + fix)

### Critical fix

**F-15 restore write window (data loss)** — `RestoreWriteBlockMiddleware` + `File.Replace` in `ApplyPending`.

### Reviewed, below PR bar

- Idempotency parallel race; DailyHeader catch-up gaps.
