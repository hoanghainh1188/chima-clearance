# ADR-0011: Streaming backup container (snapshot format v2)

**Status**: **Proposed** — awaiting owner ratification.
**Date**: 2026-10-24
**Relates to**: F-15, FR-026, ADR-0005 (Litestream), `specs/02-architecture.md` § 9, issue #150

## Context

The F-15 on-demand snapshot (`POST /api/admin/backup-now`) and restore
(`POST /api/admin/restore`) were built around a **v1 JSON container**:

```json
{ "Version": 1,
  "Wraps":   [ { "Salt", "Nonce", "WrappedKey", "Tag" }, { … } ],
  "Payload": { "Nonce", "Ciphertext", "Tag" } }      // every field Base64
```

`BackupSnapshotService` copied the SQLite DB to a temp file, read it fully into a `byte[]`,
encrypted it with **one** AES-256-GCM call, Base64-encoded the ciphertext into the JSON and
returned the whole container as `byte[]`; restore did the reverse and the endpoint copied the
upload into a `MemoryStream`. For a DB of *N* bytes that is roughly plaintext + ciphertext +
Base64 (4/3 *N*) + UTF-8 JSON + the response buffer — well over 2 GB of RAM for a few-hundred-MB
database, on gate PCs with 4–8 GB. A single GCM call also caps the payload at what fits in one
array. Separately, Kestrel's default 30 MB request-body limit and the 128 MB multipart limit
already rejected restore uploads of a realistic database.

Constraints: crypto stays on the .NET BCL (`System.Security.Cryptography`, Constitution II —
no new dependency); the FR-026 dual-wrap (install password **or** recovery code) and its KDF
parameters are unchanged; existing v1 `.bak` files in the field must still restore.

## Decision

### 1. Binary v2 container, written and read as a stream

All integers little-endian unless noted. The header is small and fixed except for the wrap count.

| Field          | Size        | Value / meaning                                                      |
|----------------|-------------|----------------------------------------------------------------------|
| `magic`        | 4           | ASCII `B2BK`                                                         |
| `version`      | 1           | `0x02`                                                               |
| `reserved`     | 1           | `0x00` (reader rejects anything else)                                |
| `chunkSize`    | 4 (u32)     | plaintext bytes per chunk — **1 MiB** (reader accepts 4 KiB–16 MiB)  |
| `iterations`   | 4 (u32)     | PBKDF2 iterations — **100 000** (reader accepts 100 000–10 000 000)  |
| `noncePrefix`  | 8           | random, per snapshot                                                 |
| `wrapCount`    | 1           | `2` (reader accepts 1–8)                                             |
| `wraps[]`      | 76 each     | `salt(16) ‖ nonce(12) ‖ wrappedKey(32) ‖ tag(16)`                    |

The header is followed by chunk records until — and including — the one flagged final:

| Field        | Size     | Meaning                                                 |
|--------------|----------|---------------------------------------------------------|
| `final`      | 1        | `0x00` more chunks follow, `0x01` last chunk            |
| `length`     | 4 (u32)  | plaintext length; `= chunkSize` unless final            |
| `ciphertext` | `length` | AES-256-GCM ciphertext                                  |
| `tag`        | 16       | GCM tag                                                 |

### 2. Keys — unchanged from v1

A random 32-byte data key (DK) per snapshot. DK is wrapped twice with AES-256-GCM under a KEK
derived by **PBKDF2-HMAC-SHA256, 16-byte random salt, 100 000 iterations, 32-byte output** —
once from the install password, once from the FR-026 recovery code. Either secret unwraps DK.
The iteration count is now written into the header so it can be raised later without another
format version; the reader enforces a floor so a tampered header cannot weaken the KDF.

### 3. Chunk encryption (STREAM construction)

For chunk *i* (0-based, u32):

- **nonce** = `noncePrefix(8) ‖ BE-u32(i)` — unique per chunk; DK is also fresh per snapshot.
- **AAD** = `header bytes ‖ BE-u32(i) ‖ final`.

Binding the index and final flag into the AAD (and the index into the nonce) means a reordered,
duplicated, dropped or re-flagged chunk fails authentication; binding the whole header means any
header edit (chunk size, wraps, prefix) fails the first chunk. **Truncation** is detected because
the stream must end with a record whose authenticated `final` byte is `0x01`; bytes after the
final record are rejected too.

### 4. Fail closed, stream end-to-end

- Backup: `CreateSnapshotAsync(…, Stream destination)` copies the DB with the SQLite online
  backup API to a temp file, then encrypts it chunk by chunk into `destination`. The endpoint
  writes to a `DeleteOnClose` temp file and returns it with `Results.File(stream)`, so the
  client gets a complete file or an error status — never a 200 with a half-written body.
- Restore: `DecryptSnapshotAsync(container, secret, Stream destination)`. The endpoint raises
  the per-request body limit to 2 GiB for this route only, and `ReadFormAsync` spools the
  upload to disk (above 64 KiB). The handler decrypts straight into
  `{db}.restore-pending.tmp`, checks the SQLite magic, and only then renames it to
  `{db}.restore-pending`. Any authentication failure, truncation or bad magic throws
  (`BACKUP_CORRUPT` / `BACKUP_INVALID_SECRET` / `BACKUP_NOT_SQLITE`), the temp file is deleted,
  and nothing is staged.
- Peak memory is a few chunk buffers (≈ 3 MiB), independent of DB size.

### 5. v1 compatibility

Restore inspects the first bytes: `B2BK` → v2; `{` → legacy v1 JSON, decoded by the original
algorithm (whole file in memory — acceptable for the existing, bounded set of v1 files);
anything else → `BACKUP_CORRUPT`. New backups are always v2. The `.bak` extension and the
download/upload API surface are unchanged.

## Consequences

- **+** Memory use on backup/restore no longer scales with the database size.
- **+** Truncation, reordering and header tampering are detected per chunk, before staging.
- **+** No new dependency; same secrets, same KDF, same API routes; v1 files still restore.
- **+** Restores larger than the old 30 MB Kestrel default now work.
- **−** v2 files cannot be restored by a build older than this change (one-way format bump —
  roll the server forward before relying on new backups).
- **−** The v1 decoder stays in the codebase until v1 files are retired.
- **−** Restore uploads are spooled to the service account's temp directory before decryption,
  so a restore needs roughly 2× the snapshot size in free disk space.

## Alternatives considered

- **Keep JSON, stream the Base64** — still a single GCM call (no chunk authentication, whole
  ciphertext needed before the tag can be checked), and 33 % size overhead.
- **`System.IO.Compression.ZipArchive` with AES** — the BCL has no AES zip support; would need
  a third-party library (Constitution II).
- **One GCM stream over the whole file via a custom `Stream`** — GCM only authenticates at the
  end, so plaintext would reach the staging file unauthenticated; chunking gives early, per-chunk
  failure and bounded memory.
