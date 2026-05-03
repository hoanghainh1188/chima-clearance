# ADR-0002: SQLite as the primary persistence store

**Status**: Accepted
**Date**: 2026-05-03
**Supersedes**: none

## Context

The system needs a primary store for ~248 declarations / day (~3 500 / month),
3-year online retention (`spec.md` FR-021), and ~5 concurrent users (constraint C7).
The store must run in-process on the entry-gate PC with **no external dependencies**
(C1 — no internet, no client/server RDBMS) and must be operable by non-IT staff (C3).

## Decision

Adopt **SQLite** in WAL mode as the single primary store, accessed through EF Core 8
+ `Microsoft.Data.Sqlite`. PRAGMAs applied at startup
(`04-implementation-plan.md § 5.1`):

```sql
PRAGMA journal_mode = WAL;
PRAGMA synchronous  = NORMAL;
PRAGMA temp_store   = MEMORY;
PRAGMA mmap_size    = 268435456;  -- 256 MB
PRAGMA busy_timeout = 5000;
```

Concurrency tokens (`RowVersion BLOB(8)`) are populated by a `BEFORE UPDATE` trigger
using `randomblob(8)` and configured as EF Core `IsRowVersion()`.

## Consequences

**Positive**:

- Zero-config: no DBA, no service to start, file lives at
  `C:\ProgramData\B2\db\b2.db` (constraint C3).
- Footprint: < 1 MB DB engine; expected DB size < 100 MB after 3 years.
- WAL mode supports the F-08 cross-client exit race serialization (see
  `research.md § 2.8`) without a separate lock manager.
- Continuous backup via Litestream (ADR-0005) replicates WAL frames every 60 s.
- Repository-pattern abstraction (`B2.Application/Repositories/`) keeps a clean
  exit ramp to PostgreSQL if Phase 3 ever needs it.

**Negative / accepted**:

- Single-writer for write transactions — fine at ~5 users; benchmarked in S1.
- No native full-text search beyond FTS5; not needed for this surface.
- File-system corruption is the main failure mode; mitigated by ADR-0005.

## Alternatives considered

- **PostgreSQL / SQL Server** — rejected: requires a server process + DBA-ish
  operations + network exposure → violates C1, C3.
- **LiteDB / RavenDB embedded** — rejected: smaller community, no Litestream-grade
  continuous backup story.
- **File-based JSON / per-day Excel** — rejected: no transactions, no concurrency
  control, no integrity constraints — exactly what got us here.

## References

- `specs/01-overview.md § 6` tech-stack table
- `specs/04-implementation-plan.md § 5` database & migrations
- `specs/02-architecture.md § 4` ERD
- ADR-0005 (Litestream)
