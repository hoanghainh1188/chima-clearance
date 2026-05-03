# Phase 1 — Data Model

**Feature**: `001-b2-clearance` · **Date**: 2026-05-03 · **Plan**: [`plan.md`](./plan.md)
**Source ERD**: [`specs/02-architecture.md § 4`](../02-architecture.md). This file
restates the ERD with attribute-level detail, the eight invariants `INV-1..INV-8`,
the new fields surfaced by spec clarifications (FR-022, FR-025, FR-026), and the
two state machines.

> **Layer ownership**: every entity below lives in **`B2.Domain`** as a class (entities)
> or `record` (value objects). EF Core mapping configurations live in
> **`B2.Infrastructure/Persistence/Configurations/`** and are the **only** outward
> coupling — `B2.Domain` itself remains free of EF Core types per Constitution V.

---

## 1. Aggregate Map

```
[Aggregate root]                  [Child entity]                         [Aggregate root]
DECLARATION  ◄────── contains 1..N ────── VEHICLE                       USER
   ├─ DeclarationNumber (unique, 12 digits)                                ├─ Username (unique)
   ├─ ProcessorId (→ PROCESSOR)                                            ├─ Role (Admin|Operator|Supervisor)
   ├─ YardId (→ YARD)                                                      └─ IsActive
   ├─ Status (Open | AllExited | KVGSConfirmed | Cancelled)
   ├─ EnteredByUserId (→ USER)                            [Aggregate root]
   ├─ KvgsConfirmedByUserId (→ USER, nullable)            PROCESSOR
   ├─ CancelledByUserId (→ USER, nullable)                  └─ FullName (unique)
   ├─ CancellationReason (nullable, required if Cancelled)
   ├─ EnteredAt, KvgsConfirmedAt, CancelledAt             [Aggregate root]
   ├─ BusinessDate (date, for header grouping)            YARD
   └─ RowVersion (concurrency token)                        ├─ Code (unique)
                                                            └─ Name
[Aggregate root]                                          [Aggregate root]
DAILY_HEADER                                              AUDIT_LOG (append-only)
   ├─ Date (unique)                                         ├─ EntityType
   ├─ OnDutyOfficerName (free text, FR-025)                ├─ EntityId
   ├─ TotalVehicles (filled at 23:00, FR-012)              ├─ Action (Create|Update|Delete|Cancel|UnExit|Restore)
   └─ CreatedAt                                             ├─ ChangesJson (before/after snapshots)
                                                            ├─ Reason (nullable)
                                                            ├─ ActorUserId (→ USER)
                                                            └─ At (server clock)

[Projection / file artifact — not in live DB]
BACKUP_SNAPSHOT (file on disk + Litestream replicas)
ARCHIVE          (Excel + PDF in sibling folder for data > 3 years; FR-021)
```

The eight aggregate roots are: **Declaration**, **DailyHeader**, **User**, **Processor**,
**Yard**, **AuditLog**. **Vehicle** is the *only* child entity (lives inside the
Declaration aggregate). **BackupSnapshot** and **Archive** are file-system artifacts
described separately in § 8 — they are not EF Core entities.

---

## 2. Entities

### 2.1 `User`

| Field | Type | Constraints | Source |
| --- | --- | --- | --- |
| `Id` | `Guid` PK | server-assigned | spec key entity |
| `Username` | `string(50)` | UK, lowercase, regex `^[a-z][a-z0-9_]{2,49}$` | F-02 |
| `PasswordHash` | `string(60)` | BCrypt cost 10 | Constitution VII |
| `FullName` | `string(200)` | required, trimmed | F-02 |
| `Role` | `enum {Admin, Operator, Supervisor}` | required | F-02, FR-027 |
| `IsActive` | `bool` | default `true` | F-02 |
| `MustChangePassword` | `bool` | default `true` for the seeded admin and any reset password | FR-017 |
| `FailedLoginCount` | `int` | default 0; reset on success | FR-018 |
| `LockedUntil` | `DateTimeOffset?` | non-null when account is locked | FR-018 |
| `LastLoginAt` | `DateTimeOffset?` | UTC | F-01 |
| `CreatedAt` | `DateTimeOffset` | UTC, immutable | — |

**Notes**: deletion forbidden (FR-014). Deactivation flips `IsActive`. The seeded
default admin user (`username = "admin"`) ships with `MustChangePassword = true`
(FR-017); unchanged-password state is surfaced on the Health page (F-18).

### 2.2 `Processor` (Người làm thủ tục)

| Field | Type | Constraints | Source |
| --- | --- | --- | --- |
| `Id` | `Guid` PK | — | F-03 |
| `FullName` | `string(200)` | UK, trimmed (`Trim` then collation-aware compare) | F-03, INV-7 |
| `IsActive` | `bool` | default `true` | F-03 |
| `CreatedAt` | `DateTimeOffset` | UTC | — |

**Notes**: deactivation hides from new-entry dropdowns; existing declarations keep
the historical reference (F-03 "Deactivate without breaking history").

### 2.3 `Yard` (Tên bãi)

| Field | Type | Constraints | Source |
| --- | --- | --- | --- |
| `Id` | `Guid` PK | — | F-04 |
| `Code` | `string(20)` | UK, uppercase, regex `^[A-Z0-9]{1,20}$` | F-04, INV-7 |
| `Name` | `string(200)` | required | F-04 |
| `IsActive` | `bool` | default `true` | F-04 |
| `CreatedAt` | `DateTimeOffset` | UTC | — |

### 2.4 `Declaration` (Tờ khai) — aggregate root

| Field | Type | Constraints | Source |
| --- | --- | --- | --- |
| `Id` | `Guid` PK | — | spec key entity |
| `DeclarationNumber` | `string(12)` | UK, regex `^[0-9]{12}$` | INV-1, F-05 |
| `ProcessorId` | `Guid` FK → `Processor.Id` | required; processor must be Active at write time | INV-7 |
| `CompanyName` | `string(200)` | required | F-05 |
| `GoodsName` | `string(200)` | required | F-05 |
| `YardId` | `Guid` FK → `Yard.Id` | required; yard must be Active at write time | INV-7 |
| `Status` | `enum {Open, AllExited, KVGSConfirmed, Cancelled}` | default `Open`; transitions per § 5.1 | F-05 / F-08 / F-09 / FR-022 |
| `EnteredByUserId` | `Guid` FK → `User.Id` | required, write-once (INV-8) | F-05 |
| `EnteredAt` | `DateTimeOffset` | UTC, server clock, immutable | INV-3 |
| `BusinessDate` | `date` | UTC date of `EnteredAt` projected to `Asia/Ho_Chi_Minh`; immutable | F-11 grouping |
| `KvgsConfirmedByUserId` | `Guid?` FK → `User.Id` | non-null iff `Status = KVGSConfirmed` (INV-8 once set) | F-09 |
| `KvgsConfirmedAt` | `DateTimeOffset?` | UTC, server clock; non-null iff `KVGSConfirmed` | F-09 |
| `CancelledByUserId` | `Guid?` FK → `User.Id` | non-null iff `Status = Cancelled` | FR-022 |
| `CancelledAt` | `DateTimeOffset?` | UTC, server clock; non-null iff `Cancelled` | FR-022 |
| `CancellationReason` | `string(500)?` | required iff `Cancelled` | FR-022 |
| `RowVersion` | `byte[8]` | concurrency token (set by `BEFORE UPDATE` trigger) | § 2.8 research |

**Indexes** (per `04-implementation-plan.md § 5.3`):

```sql
CREATE UNIQUE INDEX IX_Declaration_Number       ON Declaration(DeclarationNumber);
CREATE INDEX        IX_Declaration_BusinessDate ON Declaration(BusinessDate);
CREATE INDEX        IX_Declaration_Status       ON Declaration(Status);
CREATE INDEX        IX_Declaration_Processor    ON Declaration(ProcessorId);
CREATE INDEX        IX_Declaration_Yard         ON Declaration(YardId);
```

### 2.5 `Vehicle` (Xe) — child entity inside Declaration aggregate

| Field | Type | Constraints | Source |
| --- | --- | --- | --- |
| `Id` | `Guid` PK | — | spec key entity |
| `DeclarationId` | `Guid` FK → `Declaration.Id` | required; cascade-delete forbidden (Declarations are never hard-deleted) | F-05 / F-06 |
| `PlateNumber` | `string(15)` | required, uppercased, regex `^[0-9]{2}[A-Z]{1,2}[0-9]{4,5}$` | INV-2, FR-006 |
| `PlateLast4` | `string(4)` GENERATED `ALWAYS AS (substr(PlateNumber, -4))` STORED | covered by `IX_Vehicle_PlateLast4` | F-07 perf |
| `TrailerOrContainerNumber` | `string(20)?` | nullable, uppercased | INV-2, FR-006 |
| `EntryTime` | `DateTimeOffset` | UTC, server clock, immutable | INV-3 |
| `ExitMarked` | `bool` | default `false` | F-08 |
| `ExitTime` | `DateTimeOffset?` | UTC, server clock; non-null iff `ExitMarked = true`; immutable once set unless Admin un-exit | INV-4 |
| `ExitConfirmedByUserId` | `Guid?` FK → `User.Id` | non-null iff `ExitMarked` | INV-8 |
| `Note` | `string(500)?` | nullable | F-05, F-08 |
| `EnteredByUserId` | `Guid` FK → `User.Id` | required, write-once (INV-8) | F-05 |
| `RowVersion` | `byte[8]` | concurrency token | § 2.8 research |

**Indexes**:

```sql
CREATE INDEX IX_Vehicle_DeclarationId ON Vehicle(DeclarationId);
CREATE INDEX IX_Vehicle_PlateLast4    ON Vehicle(PlateLast4);
CREATE INDEX IX_Vehicle_ExitMarked    ON Vehicle(ExitMarked);
CREATE INDEX IX_Vehicle_EntryTime     ON Vehicle(EntryTime);
CREATE INDEX IX_Vehicle_PlateEntry    ON Vehicle(PlateNumber, EntryTime); -- F-17 simple v1
```

### 2.6 `DailyHeader`

| Field | Type | Constraints | Source |
| --- | --- | --- | --- |
| `Id` | `Guid` PK | — | spec key entity |
| `Date` | `date` | UK | F-11 |
| `OnDutyOfficerName` | `string(200)?` | nullable; inline-editable on today's row only by any authenticated user | FR-025 |
| `TotalVehicles` | `int?` | nullable until 23:00 summary fires | F-12 |
| `CreatedAt` | `DateTimeOffset` | UTC, immutable | F-11 |
| `LastSummaryComputedAt` | `DateTimeOffset?` | UTC, set by `GenerateDailySummaryJob` and by Admin "Recompute" (F-12) | F-12 |

### 2.7 `AuditLog` (append-only)

| Field | Type | Constraints | Source |
| --- | --- | --- | --- |
| `Id` | `Guid` PK | — | spec key entity |
| `EntityType` | `string(50)` | e.g., `"Vehicle"`, `"Declaration"`, `"User"`, `"DailyHeader"`, `"BackupSnapshot"` | FR-003 |
| `EntityId` | `Guid` | the affected aggregate's id | FR-003 |
| `Action` | `enum {Create, Update, Delete, Cancel, UnExit, Restore, OfficerNameChanged}` | enum kept narrow | FR-003 / FR-022 / FR-025 |
| `ChangesJson` | `string` (TEXT) | JSON `{"before": {...}, "after": {...}}`; for create: `before = null`; for delete-equivalents: `after = null` | FR-003 |
| `Reason` | `string(500)?` | nullable; required for `Cancel`, `UnExit`, `Restore` | FR-022, F-08 |
| `ActorUserId` | `Guid?` FK → `User.Id` | nullable for system-triggered (Quartz jobs use a sentinel `system` user) | F-12 |
| `At` | `DateTimeOffset` | UTC, immutable | FR-003 |

**Indexes**:

```sql
CREATE INDEX IX_AuditLog_EntityIdAt ON AuditLog(EntityType, EntityId, At);
CREATE INDEX IX_AuditLog_ActorAt    ON AuditLog(ActorUserId, At);
```

**Mutability**: enforced **append-only** by application convention — no UPDATE or
DELETE endpoint exists, EF Core's `ChangeTracker` only tracks inserts on this table,
and the integration test sweep verifies that every successful write produces exactly
one row (FR-003).

### 2.8 `IdempotencyKey` (infrastructure-only — not in spec ERD)

Used by the Idempotency middleware (research § 2.7). Not user-visible.

| Field | Type | Constraints |
| --- | --- | --- |
| `Key` | `string(64)` PK | client-supplied via `Idempotency-Key` header |
| `RequestHash` | `string(64)` | SHA-256 of `(method, path, normalized body)` |
| `ResponseStatus` | `int` | HTTP status of the original response |
| `ResponseBodyHash` | `string(64)` | SHA-256 of the original response body (for forensic compare) |
| `CreatedAt` | `DateTimeOffset` | UTC; rows older than 24 h evicted by `IdempotencyKeyEvictionJob` |

---

## 3. Value Objects (in `B2.Domain`)

| VO | Underlying type | Validation |
| --- | --- | --- |
| `DeclarationNumber` | `string` | regex `^[0-9]{12}$`; throws `DomainException` otherwise |
| `PlateNumber` | `string` | regex `^[0-9]{2}[A-Z]{1,2}[0-9]{4,5}$`; constructor uppercases input |
| `ContainerNumber` | `string` | regex `^[A-Z0-9]{1,20}$`; constructor uppercases input |
| `YardCode` | `string` | regex `^[A-Z0-9]{1,20}$` |
| `Username` | `string` | regex `^[a-z][a-z0-9_]{2,49}$` |
| `RoleEnum` | `enum {Admin, Operator, Supervisor}` | strongly typed; never a magic string |
| `DeclarationStatusEnum` | `enum {Open, AllExited, KVGSConfirmed, Cancelled}` | strongly typed |

VOs are immutable `record struct` or `record class`. They protect entities from
invalid raw strings at construction time (Constitution III).

---

## 4. Enforced Invariants

> Every invariant maps to (a) the layer that enforces it, (b) the test project that
> proves it, and (c) the corresponding spec / business-rule citation.

| ID | Invariant | Enforced in | Test in | Source |
| --- | --- | --- | --- | --- |
| **INV-1** | `Declaration.DeclarationNumber` is exactly 12 digits and unique across all declarations | `B2.Domain` (`DeclarationNumber` VO) + DB unique index | `B2.Domain.Tests`, `B2.Server.Api.Tests` (409 on duplicate) | Business rule cột C, F-05 |
| **INV-2** | `Vehicle.PlateNumber` and `Vehicle.TrailerOrContainerNumber` are stored upper-cased | `B2.Domain` (`PlateNumber`, `ContainerNumber` VO constructors) | `B2.Domain.Tests` | Business rule cột E, F; FR-006 |
| **INV-3** | `Vehicle.EntryTime` and `Declaration.EnteredAt` are set on creation by the **server** clock and are immutable thereafter | `B2.Domain` (private setters; no public setter on these fields) + `IClock` injected from Application | `B2.Domain.Tests`, `B2.Server.Api.Tests` (F-05 "Server timestamp") | Business rule cột I; FR-004 |
| **INV-4** | `Vehicle.ExitTime` is set when `ExitMarked` flips false→true and is immutable thereafter (Admin un-exit clears it back to null) | `B2.Domain` (`Vehicle.MarkExit(...)`, `Vehicle.UnExit(...)` aggregate methods) | `B2.Domain.Tests`, `B2.Server.Api.Tests` | Business rule cột K; F-08 |
| **INV-5** | A declaration becomes `AllExited` automatically when every child Vehicle has `ExitMarked = true`; reverts to `Open` if Admin un-exits any child | `B2.Domain` (`Declaration.RecomputeStatus()` invoked by aggregate methods) | `B2.Domain.Tests` | Business rule (dòng cha xanh); F-08, F-09 |
| **INV-6** | Only `Supervisor` or `Admin` can transition `AllExited → KVGSConfirmed` | `B2.Domain` (`Declaration.ConfirmKvgs(actorRole)` checks role) **and** `B2.Server.Api` policy `SupervisorOrAbove` | `B2.Domain.Tests`, `B2.Server.Api.Tests` (F-09 Operator-403) | Business rule cột L; F-09; FR-027 |
| **INV-7** | `Declaration.ProcessorId` and `Declaration.YardId` MUST reference a Processor / Yard that exists and is `IsActive = true` at write time | `B2.Application` (validator queries master data) | `B2.Application.Tests`, `B2.Server.Api.Tests` (F-03 unknown-processor) | Business rule cột B, H; FR-007 |
| **INV-8** | "Người nhập sổ" (`EnteredByUserId`) and "Người xác nhận" (`ExitConfirmedByUserId`, `KvgsConfirmedByUserId`) are write-once — never editable by any role except via Admin un-exit which resets `ExitConfirmedByUserId` together with `ExitTime` | `B2.Domain` (no public setters; aggregate methods reset paired fields atomically) | `B2.Domain.Tests` | Business rule cột M, N |

**Additional invariants surfaced by post-clarify FRs** (not in the original
INV-1..INV-8 list — to be added to the same test sweep):

| ID | Invariant | Enforced in | Source |
| --- | --- | --- | --- |
| **INV-9** | `Declaration.Cancel(reason)` is allowed **only** from `Status = Open` and requires a non-empty `reason` | `B2.Domain` (`Declaration.Cancel`) | FR-022 |
| **INV-10** | `Declaration.Cancelled` rows are **never** mutated further (no add-vehicle, no exit-mark, no KVGS) | `B2.Domain` aggregate methods reject; API returns 409 `DECLARATION_NOT_OPEN` | FR-022 |
| **INV-11** | `DailyHeader.OnDutyOfficerName` is editable only on **today's** header (server-clock comparison) | `B2.Application` validator (F-25 PATCH endpoint) | FR-025 |
| **INV-12** | Records older than 3 years are absent from live tables after the annual archive job runs **and** their archive file exists in the sibling folder | `AnnualArchiveJob` (`B2.Infrastructure`) | FR-021 |

---

## 5. State Machines (restated, see also `research.md § 1`)

### 5.1 Declaration

```
Open ───── last child Vehicle.ExitMarked = true ─────► AllExited
 │                                                      │
 │                                          Supervisor /│Admin
 │                                          confirms KVGS
 │                                                      ▼
 │                                                KVGSConfirmed (terminal)
 │
 │ Admin un-exit child (Outside→Inside): may revert to Open
 │
 └── Admin cancel + reason ──► Cancelled (terminal; visible-but-locked)
```

State changes are written together with their AuditLog row inside one EF Core
transaction. `Declaration.RecomputeStatus()` runs at the end of every `Vehicle`
mutation and is idempotent.

### 5.2 Vehicle

```
Inside ──── ExitMarked=true, ExitTime=server clock ────► Outside
   ▲                                                       │
   │                                                       │
   └────── Admin un-exit (audited reason; ExitTime=null) ──┘
```

---

## 6. Retention & Archive (FR-021 + INV-12)

### 6.1 Live retention window

The live database holds the **most recent 3 years** of `Declaration`, `Vehicle`,
`DailyHeader`, `AuditLog` rows on a rolling window. Master data (`User`, `Processor`,
`Yard`) is exempt and retained as long as it remains in use.

### 6.2 Annual archive job (`AnnualArchiveJob`)

- **Schedule**: cron `0 0 1 1 2 ?` — 01:00 on 1 February by default (Admin-configurable
  per `FR-021`).
- **Algorithm** (single transaction):
  1. Compute `cutoff = today - 3 years`.
  2. Read every Declaration whose `BusinessDate < cutoff` and its child Vehicles +
     associated DailyHeader + AuditLog rows.
  3. Write `Sổ_B2_archive_{year}.xlsx` and `Sổ_B2_archive_{year}.pdf` to the archive
     folder (sibling of the live DB) using the Sổ B2 template.
  4. **Verify**: re-open both files programmatically, sample 10 rows, compare hashes
     to source rows.
  5. If verification passes → delete the archived rows in the same transaction.
     If verification fails → rollback the delete, log Error, raise an in-app
     `Health` warning ("Archive run {year} failed; live data preserved").

- **Visibility**: archived data is **not** loaded into the app at runtime; in-app
  search and reports return only live rows. Admin opens the archive folder directly
  to consult historical data.

### 6.3 Cancellation interaction

`Cancelled` declarations also age out via the 3-year window; they are archived
(strike-through formatting preserved in the Excel archive) and then deleted from the
live tables exactly like any other declaration.

---

## 7. Cross-Cutting DB Conventions

- **PRIMARY KEYs**: every aggregate root and every child entity uses a server-
  generated `Guid` (sequential GUID via `NewGuid().ToOrderedGuid()` if EF Core
  benchmarks reveal index churn — defer to Sprint S2 review).
- **Foreign keys**: enforced (`PRAGMA foreign_keys = ON`) per
  `04-implementation-plan.md § 5.1`.
- **Timestamps**: `DateTimeOffset` columns serialized as ISO-8601 with explicit `Z`.
- **Soft delete**: only `Declaration` (via `Status = Cancelled`); no other entity has
  a soft-delete flag.
- **Concurrency tokens**: `RowVersion BLOB(8)` on `Declaration` and `Vehicle`,
  populated by a `BEFORE UPDATE FOR EACH ROW` trigger using `randomblob(8)`. EF Core
  configures it via `IsRowVersion()` + `ValueGeneratedOnAddOrUpdate()`.
- **Migrations**: EF Core Migrations (`dotnet ef migrations add <Name> -p
  src/B2.Infrastructure -s src/B2.Server.Service`); applied on service startup;
  reversible OR documented as forward-only with a backup-before-upgrade banner.

---

## 8. File-System Artifacts (not EF Core entities)

### 8.1 `BackupSnapshot`

| Attribute | Value |
| --- | --- |
| File-name pattern | `b2-YYYYMMDD-HHmm.bak.zip` |
| Format | AES-256-GCM-encrypted zip containing `b2.db` (`sqlite3 .backup` output) + a JSON header `{"created":"...","actor":"...","wrappedDk":[...]}` |
| Decryption secrets | data key (DK) wrapped twice — once with `KDF(install-password)`, once with `KDF(recovery-code)` (FR-026) |
| Storage targets | every entry in the configured `BackupTargets` (default: USB + LAN folder) |
| Lifetime | infinite (Admin manages disk by hand or via a future cleanup job) |
| Audit | one `AuditLog` row per snapshot (`Action = Create`, `EntityType = "BackupSnapshot"`, `EntityId = snapshotId`, `ChangesJson = {"after": {...}}`) |

### 8.2 `Archive` (per year, after `AnnualArchiveJob` runs)

| Attribute | Value |
| --- | --- |
| File-name pattern | `Sổ_B2_archive_{year}.xlsx` and `Sổ_B2_archive_{year}.pdf` |
| Storage | sibling folder `C:\ProgramData\B2\archive\{year}\` |
| Editability | none — files are produced read-only and are not loaded back into the app |
| Audit | one `AuditLog` row per successful archive (`EntityType = "Archive"`, `Action = Create`); failed runs log Error and skip the delete |

---

## 9. Connection String

Per `04-implementation-plan.md § 5.1`:

```
Data Source=C:\ProgramData\B2\db\b2.db;
Cache=Shared;
Foreign Keys=True;
Pooling=True;
```

PRAGMAs applied on startup by `B2.Infrastructure/Persistence/SqliteStartup.cs`:

```sql
PRAGMA journal_mode = WAL;
PRAGMA synchronous  = NORMAL;
PRAGMA temp_store   = MEMORY;
PRAGMA mmap_size    = 268435456;  -- 256 MB
PRAGMA busy_timeout = 5000;
```

---

## 10. Mapping Cheat Sheet (Spec → Data Model)

| Spec citation | Where in this document |
| --- | --- |
| `spec.md` Key Entities (User, Role, Processor, Yard, Declaration, Vehicle, DailyHeader, AuditLog, BackupSnapshot, Archive) | §§ 2, 8 |
| `spec.md` FR-001..FR-026 | §§ 2 (entity fields), 4 (invariants), 6 (retention), 8 (BackupSnapshot / Archive) |
| `spec.md` FR-027 (role matrix) | § 4 INV-6 + § 9 of `research.md`; enforced at `B2.Server.Api` (out of scope for this file) |
| `spec.md` FR-028 (tri-state connectivity) | client-side state; no DB schema impact (out of scope for this file) |
| `02-architecture.md § 4` ERD | § 1 aggregate map + § 2 entity tables |
| `02-architecture.md § 7` state machines | § 5 |
| Business rules cột A–P | INV-1..INV-8 in § 4 |
| `04-implementation-plan.md § 5.3` indexes | § 2 (per-entity index blocks) |
| `01-overview.md § 6` tech stack | § 9 connection string + PRAGMAs |
