# Bug-finding notes

## 2026-06-05 — PR #58

### Critical: silent backup-secrets rotation (F-15)

**File:** `src/B2.Infrastructure/Backup/BackupSecretsStore.cs`

**Trigger:** Sealed secrets file exists (`backup-secrets.dpapi` / dev JSON) but DPAPI unwrap, JSON parse, or IO fails. Common after OS restore, truncated file, or AV lock.

**Impact:** `GetOrCreate()` generated new password + recovery code and overwrote the file. All existing encrypted `.bak` snapshots permanently undecryptable.

**Fix:** Throw `BACKUP_SECRETS_UNREADABLE` when file present but unreadable; atomic persist.

### Not fixed (below bar)

- **Cancel vs exit race:** concurrent admin cancel + operator exit can last-write-win; `RowVersion` concurrency token never updated.
- **Restore window:** staged restore applied on restart; brief window for writes before `StopApplication()`.
