# Chima Customs B2 — System Specification: Architecture

> Read `01-overview.md` first. Constraint references like (C1), (C5) refer to that file.

---

## 1. Logical Architecture

```mermaid
flowchart TB
    subgraph Presentation["Presentation Tier — runs on every PC"]
        MAUI["B2.Desktop<br/>(.NET MAUI 8 — Windows target)<br/>MVVM + CommunityToolkit.Mvvm"]
    end

    subgraph API["API Tier — runs only on Entry-Gate PC, hosted by Windows Service"]
        WEBAPI["B2.Server.Api<br/>(ASP.NET Core 8 Minimal API)"]
        SIGNALR["SignalR Hub<br/>/hubs/events"]
        AUTH["JWT Middleware<br/>(RS256)"]
        AUDIT["Audit Middleware"]
    end

    subgraph Application["Application Tier"]
        USECASES["Use Cases (MediatR)<br/>Commands + Queries"]
        VAL["FluentValidation"]
        DOMEV["Domain Event Dispatcher"]
    end

    subgraph Domain["Domain Tier"]
        AGG["Aggregates<br/>Declaration ◄ Vehicle"]
        VO["Value Objects<br/>PlateNumber, ContainerNumber, DeclarationNumber"]
        RULES["Domain Services<br/>Daily Header, KVGS state machine"]
    end

    subgraph Infra["Infrastructure Tier"]
        EF["EF Core 8 Repositories"]
        SQLITE[("SQLite + WAL")]
        QUARTZ["Quartz.NET<br/>Background Jobs"]
        EXCEL["ClosedXML<br/>Export/Import"]
        LITESTREAM["Litestream<br/>Continuous Backup"]
        SERILOG["Serilog<br/>File + In-app sink"]
    end

    MAUI -->|Refit / HttpClient| WEBAPI
    MAUI -->|WebSocket| SIGNALR
    WEBAPI --> AUTH --> AUDIT --> USECASES
    SIGNALR --> USECASES
    USECASES --> VAL
    USECASES --> AGG
    USECASES --> EF
    AGG --> RULES
    AGG --> DOMEV
    DOMEV --> SIGNALR
    EF --> SQLITE
    QUARTZ --> USECASES
    USECASES --> EXCEL
    SQLITE -.->|continuous| LITESTREAM
    USECASES --> SERILOG

    classDef pres fill:#f3e5f5,stroke:#7b1fa2
    classDef api fill:#e1f5ff,stroke:#0288d1
    classDef app fill:#e8f5e9,stroke:#388e3c
    classDef dom fill:#fff3e0,stroke:#f57c00
    classDef inf fill:#fce4ec,stroke:#c2185b
    class MAUI pres
    class WEBAPI,SIGNALR,AUTH,AUDIT api
    class USECASES,VAL,DOMEV app
    class AGG,VO,RULES dom
    class EF,SQLITE,QUARTZ,EXCEL,LITESTREAM,SERILOG inf
```

---

## 2. Process & Deployment View

```mermaid
flowchart LR
    subgraph EntryHost["Entry-Gate Windows 10 Host (Server + Client)"]
        direction TB
        SVC["B2.Server.Service<br/>(Windows Service, auto-start)<br/>port 5001 (HTTPS)"]
        DESK1["B2.Desktop.exe<br/>(user session)"]
        FILES[("📁 C:\ProgramData\B2\<br/>db, logs, backups")]
        SVC -.read/write.-> FILES
        DESK1 -.localhost:5001.-> SVC
    end

    subgraph ExitHost["Exit-Gate Windows 10 Host"]
        DESK2["B2.Desktop.exe"]
    end

    subgraph SupHost["Supervisor Hosts (×3)"]
        DESK3["B2.Desktop.exe"]
    end

    USB[("💾 USB Drive<br/>D:\B2-Backup")]
    NAS[("🗂️ \\\\entry-pc\\B2-Backup<br/>(LAN share)")]

    DESK2 -->|"https://entry-pc:5001"| SVC
    DESK3 -->|"https://entry-pc:5001"| SVC
    SVC -.Litestream replicate.-> USB
    SVC -.Litestream replicate.-> NAS

    classDef svc fill:#e1f5ff,stroke:#0288d1,stroke-width:2px
    classDef cli fill:#f3e5f5,stroke:#7b1fa2
    classDef stor fill:#fff3e0,stroke:#f57c00
    class SVC svc
    class DESK1,DESK2,DESK3 cli
    class FILES,USB,NAS stor
```

### Host configuration

| Item | Value |
|------|-------|
| Service identity | `LocalSystem` (so it runs without a logged-in user — required by C6) |
| Service recovery | First / Second / Subsequent failure → Restart Service after 30 s |
| Listen address | `https://0.0.0.0:5001` with self-signed cert installed to LocalMachine/Root by installer |
| Firewall rule | Inbound TCP 5001 allowed only on the LAN profile, blocked on Public |
| Data directory | `C:\ProgramData\B2\` (subfolders: `db/`, `logs/`, `backups/`, `keys/`) |
| Permissions on `ProgramData\B2\` | Service account: full control. Authenticated users: read-only on `logs/` |

---

## 3. Project / Solution Layout

```
chima-customs/
└── src/
    ├── B2.Domain/                    # Entities, Value Objects, Domain Services. No EF, no MAUI.
    ├── B2.Application/               # Use cases (MediatR), DTOs, Validators, Interfaces.
    ├── B2.Infrastructure/            # EF Core, Repositories, Quartz jobs, Litestream wrapper, Excel I/O.
    ├── B2.Server.Api/                # ASP.NET Core Minimal API, SignalR Hub, Auth, Audit middleware.
    ├── B2.Server.Service/            # Windows Service host that hosts B2.Server.Api.
    ├── B2.Shared.Contracts/          # DTOs shared between Server and Desktop (no logic).
    ├── B2.Desktop/                   # .NET MAUI app (Windows target only for Phase 1).
    │   ├── Views/                    # XAML pages
    │   ├── ViewModels/               # CommunityToolkit.Mvvm
    │   ├── Services/                 # API client (Refit), Auth state, SignalR client
    │   ├── Resources/                # Styles, colors (WCAG AA), fonts
    │   └── Platforms/Windows/        # Windows-specific entry point
    └── B2.Installer/                 # Velopack config / Inno Setup script
└── tests/
    ├── B2.Domain.Tests/              # xUnit
    ├── B2.Application.Tests/         # xUnit + FluentAssertions
    ├── B2.Server.Api.Tests/          # WebApplicationFactory integration tests
    └── B2.Desktop.Tests/             # MAUI handler tests
└── docs/
    ├── adr/                          # Architecture Decision Records
    └── runbook.md                    # 5-page operator runbook
└── specs/                            # ← THIS DIRECTORY (the docs you are reading)
```

> **AI agents**: respect this layout. New code must be placed in the correct project. Cross-tier references must follow Domain ← Application ← Infrastructure / API; Domain has zero outward dependencies.

---

## 4. Domain Model (ERD)

```mermaid
erDiagram
    USER ||--o{ DECLARATION : "entered_by"
    USER ||--o{ VEHICLE : "exit_confirmed_by"
    USER ||--o{ DECLARATION : "kvgs_confirmed_by"

    PROCESSOR ||--o{ DECLARATION : "handled_by"
    YARD ||--o{ DECLARATION : "stored_at"

    DECLARATION ||--|{ VEHICLE : "contains 1..N"
    DAILY_HEADER ||--o{ DECLARATION : "groups by date"

    DECLARATION ||--o{ AUDIT_LOG : "history"
    VEHICLE ||--o{ AUDIT_LOG : "history"

    USER {
        guid Id PK
        string Username UK
        string PasswordHash "BCrypt"
        string FullName
        string Role "Admin|Operator|Supervisor"
        bool IsActive
        datetime CreatedAt
    }

    PROCESSOR {
        guid Id PK
        string FullName UK "Master data"
        bool IsActive
    }

    YARD {
        guid Id PK
        string Code UK
        string Name
        bool IsActive
    }

    DAILY_HEADER {
        guid Id PK
        date Date UK
        guid MainOfficerId FK
        int TotalVehicles "filled at 23:00"
        datetime CreatedAt
    }

    DECLARATION {
        guid Id PK
        string DeclarationNumber UK "12 digits"
        guid ProcessorId FK
        string CompanyName
        string GoodsName
        guid YardId FK
        guid EnteredByUserId FK
        datetime EnteredAt "immutable"
        string Status "Open|AllExited|KVGSConfirmed"
        guid KvgsConfirmedByUserId FK "nullable"
        datetime KvgsConfirmedAt "nullable"
        date BusinessDate "for header grouping"
    }

    VEHICLE {
        guid Id PK
        guid DeclarationId FK
        string PlateNumber "uppercased"
        string TrailerOrContainerNumber "uppercased, nullable"
        datetime EntryTime "immutable"
        bool ExitMarked
        datetime ExitTime "immutable, nullable"
        guid ExitConfirmedByUserId FK "nullable"
        string Note
    }

    AUDIT_LOG {
        guid Id PK
        string EntityType
        guid EntityId
        string Action "Create|Update|Delete"
        string ChangesJson
        guid ActorUserId FK
        datetime At
    }
```

### Aggregate boundaries

- **Declaration** is the aggregate root. **Vehicle** is its child entity. All writes to a vehicle go through its declaration.
- **DailyHeader**, **User**, **Processor**, **Yard** are independent aggregates.
- **AuditLog** is append-only, written by middleware, never modified.

### Invariants enforced in Domain layer

| ID | Invariant | Source |
|----|-----------|--------|
| INV-1 | `DeclarationNumber` is exactly 12 digits and unique among non-cancelled declarations | Business rule cột C + FR-022 |
| INV-2 | `PlateNumber` and `TrailerOrContainerNumber` are stored upper-cased **and separator-stripped** (`29c-12345` → `29C12345`) | Business rule cột E, F |
| INV-3 | `EntryTime` is set on creation by the server clock and is immutable thereafter | Business rule cột I |
| INV-4 | `ExitTime` is set when `ExitMarked` flips false→true and is immutable thereafter | Business rule cột K |
| INV-5 | A declaration becomes `AllExited` automatically when every child vehicle has `ExitMarked=true` | Business rule (dòng cha xanh) |
| INV-6 | `AllExited → KVGSConfirmed` requires the declaration to be `AllExited`; any gate role may confirm (business rule cột L has the exit-gate cán bộ tick it) | Business rule cột L, § B.2 |
| INV-7 | `Processor.FullName` and `Yard.Code` referenced by a Declaration must exist and be active at write time | Business rule cột B, H |
| INV-8 | "Người nhập sổ" and "Người xác nhận" fields are write-once; never editable | Business rule cột M, N |

---

## 5. Key Sequences

### 5.1 Vehicle entry (at Entry Gate)

```mermaid
sequenceDiagram
    actor Op as Operator
    participant UI as B2.Desktop
    participant API as B2.Server.Api
    participant App as Application Layer
    participant DB as SQLite
    participant Hub as SignalR Hub
    participant Other as Other Clients

    Op->>UI: Fill declaration + vehicle form, press Enter
    UI->>UI: Client-side validation (12 digits, required fields)
    UI->>API: POST /api/declarations<br/>(idempotency-key header)
    API->>App: CreateDeclarationCommand
    App->>App: FluentValidation
    App->>App: Check Processor & Yard exist (master data)
    App->>DB: BEGIN TX<br/>INSERT Declaration<br/>INSERT Vehicle (uppercased plate, server timestamp)<br/>INSERT AuditLog (×2)<br/>COMMIT
    DB-->>App: ok
    App->>Hub: Publish DeclarationCreated event
    Hub-->>Other: Push "new row" to subscribed clients
    App-->>API: DeclarationDto
    API-->>UI: 201 Created
    UI->>Op: Show success toast, focus next entry field
```

### 5.2 Vehicle exit search & confirmation (at Exit Gate)

```mermaid
sequenceDiagram
    actor Op as Exit Operator
    participant UI as B2.Desktop
    participant API as B2.Server.Api
    participant App as Application Layer
    participant DB as SQLite
    participant Hub as SignalR Hub

    Op->>UI: Type last 3-4 digits of plate
    UI->>API: GET /api/vehicles/search?suffix=5769&unexited=true
    API->>App: SearchVehicleQuery
    App->>DB: SELECT … WHERE plate LIKE '%5769'<br/>AND ExitMarked=false<br/>ORDER BY EntryTime ASC LIMIT 10
    DB-->>App: rows
    App-->>API: results
    API-->>UI: 200 OK (list)
    UI->>Op: Highlight matching row(s)

    Op->>UI: Click "Mark exit" on the right row
    UI->>API: POST /api/vehicles/{id}/exit
    API->>App: MarkVehicleExitCommand
    App->>DB: BEGIN TX<br/>UPDATE Vehicle SET ExitMarked=true, ExitTime=now()<br/>recompute Declaration.Status (AllExited?)<br/>INSERT AuditLog<br/>COMMIT
    DB-->>App: ok
    App->>Hub: Publish VehicleExited (+ DeclarationReadyForKvgs if applicable)
    Hub-->>UI: Push update to all clients (color flips)
    API-->>UI: 200 OK
    UI->>Op: Show success, clear search box
```

### 5.3 KVGS confirmation (at Supervisor PC)

```mermaid
sequenceDiagram
    actor Sup as Supervisor
    participant UI as B2.Desktop
    participant API as B2.Server.Api
    participant App as Application Layer
    participant DB as SQLite
    participant Hub as SignalR Hub

    Note over Sup: Declaration row is currently green<br/>(AllExited, awaiting KVGS)
    Sup->>UI: Tick "Xác nhận KVGS" on parent row
    UI->>API: POST /api/declarations/{id}/kvgs-confirm
    API->>App: ConfirmKvgsCommand
    App->>App: Authorize (Supervisor or Admin)
    App->>App: Check current Status == AllExited
    App->>DB: BEGIN TX<br/>UPDATE Declaration SET Status='KVGSConfirmed',<br/>KvgsConfirmedByUserId=current, KvgsConfirmedAt=now()<br/>INSERT AuditLog<br/>COMMIT
    App->>Hub: Publish DeclarationKvgsConfirmed
    Hub-->>UI: Push (color reverts to normal)
    API-->>UI: 200 OK
```

### 5.4 Daily header creation & summary (Background)

```mermaid
sequenceDiagram
    participant Q as Quartz Scheduler
    participant App as Application Layer
    participant DB as SQLite
    participant Log as Serilog

    Note over Q: Cron 0 1 0 * * ? (00:01 daily)
    Q->>App: CreateDailyHeaderJob
    App->>DB: SELECT today's header — exists?
    alt missing
        App->>DB: INSERT DailyHeader (date=today, MainOfficer=on-shift user or null)
        App->>Log: log "header created"
    else exists
        App->>Log: log "header already exists, skip"
    end

    Note over Q: Cron 0 0 23 * * ? (23:00 daily)
    Q->>App: GenerateDailySummaryJob
    App->>DB: COUNT vehicles per declaration for today<br/>UPDATE DailyHeader.TotalVehicles
    App->>Log: log "summary generated"

    Note over Q,App: On service startup, both jobs check for missed runs<br/>(if server was down at 00:01) and execute catch-up
```

### 5.5 Excel export

```mermaid
sequenceDiagram
    actor U as User
    participant UI as B2.Desktop
    participant API as B2.Server.Api
    participant App as Application Layer
    participant DB as SQLite
    participant XL as ClosedXML

    U->>UI: Open Reports → Export → Pick date range
    UI->>API: GET /api/reports/excel?from=...&to=...
    API->>App: ExportExcelQuery
    App->>DB: SELECT declarations + vehicles in range<br/>(joined with master data)
    App->>XL: Open template "Mẫu sổ B2.xlsx"<br/>Fill rows by column letter (A..O)<br/>Apply conditional formatting:<br/>- red text if ExitMarked=false<br/>- green parent row if AllExited & not KVGS-confirmed
    XL-->>App: byte[]
    App-->>API: FileResult
    API-->>UI: application/vnd.openxmlformats…
    UI->>U: Save dialog
```

---

## 6. API Surface (locked for Phase 1)

All endpoints are under `/api`. JSON only. JWT in `Authorization: Bearer …` header.

| Method | Path | Auth | Purpose |
|--------|------|------|---------|
| POST | `/api/auth/login` | anonymous | Issue JWT (8 h) + refresh token (7 d) |
| POST | `/api/auth/refresh` | refresh token | Rotate JWT |
| POST | `/api/auth/logout` | any | Revoke refresh token |
| GET | `/api/users` | Admin | List users |
| POST | `/api/users` | Admin | Create user |
| PUT | `/api/users/{id}` | Admin | Update user (name, role, active) |
| POST | `/api/users/{id}/reset-password` | Admin | Reset password |
| GET | `/api/master/processors` | any | List processors |
| POST/PUT/DELETE | `/api/master/processors[/{id}]` | Admin | CRUD |
| GET | `/api/master/yards` | any | List yards |
| POST/PUT/DELETE | `/api/master/yards[/{id}]` | Admin | CRUD |
| GET | `/api/declarations` | any | Paginated list with filters (date, status, plate, processor, yard) |
| POST | `/api/declarations` | Operator+ | Create declaration with initial vehicle(s) |
| GET | `/api/declarations/{id}` | any | Detail + child vehicles |
| POST | `/api/declarations/{id}/vehicles` | Operator+ | Add child vehicle to existing declaration |
| POST | `/api/declarations/{id}/kvgs-confirm` | Supervisor+ | Confirm KVGS |
| GET | `/api/vehicles/search?suffix=…&unexited=…` | any | Search by plate suffix |
| POST | `/api/vehicles/{id}/exit` | Operator+ | Mark exit |
| GET | `/api/reports/by-processor?from=…&to=…` | Supervisor+ | Vehicle count per processor |
| GET | `/api/reports/by-yard?from=…&to=…` | Supervisor+ | Per yard |
| GET | `/api/reports/by-hour?date=…` | Supervisor+ | Per exit hour bucket |
| GET | `/api/reports/by-officer?from=…&to=…` | Supervisor+ | Per entered-by / confirmed-by |
| GET | `/api/reports/pending?asOf=…` | Supervisor+ | Vehicles still inside |
| GET | `/api/reports/excel?from=…&to=…` | Supervisor+ | Template-based Excel |
| GET | `/api/admin/audit?…` | Admin | Read audit log |
| POST | `/api/admin/backup-now` | Admin | Trigger one-shot backup |
| POST | `/api/admin/restore` | Admin (multipart upload) | Restore from a backup file |
| GET | `/api/admin/health` | any | Health check (DB + disk + service uptime) |
| WS | `/hubs/events` | any | SignalR hub for push updates |

> **API stability promise**: paths and DTOs in this list are stable for Phase 1. Breaking changes require an ADR.

---

## 7. State Machines

### Declaration status

```mermaid
stateDiagram-v2
    [*] --> Open: Created at entry gate
    Open --> AllExited: Last child vehicle marked exited
    AllExited --> Open: Vehicle un-exited (admin only, audited)
    AllExited --> KVGSConfirmed: Supervisor confirms KVGS
    KVGSConfirmed --> [*]: Terminal — no further edits
```

### Vehicle exit flag

```mermaid
stateDiagram-v2
    [*] --> Inside: Created at entry gate
    Inside --> Outside: ExitMarked=true,<br/>ExitTime=now()
    Outside --> Inside: Admin override only,<br/>requires reason in audit
```

---

## 8. Cross-Cutting Concerns

| Concern | Implementation |
|---------|---------------|
| **Authentication** | JWT RS256. Private key stored in `C:\ProgramData\B2\keys\` protected by Windows DPAPI (per-machine). Tokens 8 h; refresh 7 d. Logout = revoke refresh. |
| **Authorization** | Policy-based (`[Authorize(Policy = "AdminOnly")]`, `"OperatorOrAbove"`, `"SupervisorOrAbove"`). Enforced server-side. |
| **Validation** | FluentValidation on every command. Common rules in `B2.Application/Validation/Common.cs`. |
| **Audit log** | Middleware captures `(actor, action, entity, before, after)` JSON-serialized. Immutable (no UPDATE/DELETE allowed by app). |
| **Logging** | Serilog → file `C:\ProgramData\B2\logs\b2-yyyymmdd.log` (rolling 30 days) + in-memory sink readable by Admin in-app. |
| **Error handling** | `ProblemDetails` per RFC 7807. Domain exceptions mapped to 400/409. Unhandled → 500 + log + correlation id shown to user. |
| **Idempotency** | `POST /api/declarations` and `POST /api/vehicles/{id}/exit` accept `Idempotency-Key` header; server stores last 24 h of keys to dedupe retries from Polly. |
| **Concurrency** | EF Core optimistic concurrency token (`RowVersion BLOB` in SQLite via trigger). 409 on conflict; client refetches and retries. |
| **Time** | All timestamps stored as UTC; UI renders in `Asia/Ho_Chi_Minh`. Format displayed: `dd/MM/yyyy HH:mm`. |
| **i18n** | Vietnamese only for Phase 1. All strings in `Resources/Strings/AppResources.vi.resx`. |
| **Accessibility** | Min font 16 sp; colors meet 4.5:1 contrast (test with axe-style audit); full keyboard navigation; focus rings always visible. |

---

## 9. Backup & Restore Architecture

```mermaid
flowchart LR
    SQLITE[("SQLite WAL")] -->|continuous| LITE["Litestream Daemon<br/>(child of Windows Service)"]
    LITE -->|every 60 s| USB[("💾 USB D:\B2-Backup\replica\")]
    LITE -->|every 60 s| LAN[("🗂️ LAN share \\\\entry-pc\\B2-Backup\\")]
    ADMIN[Admin] -->|"POST /api/admin/backup-now"| API[API]
    API -->|"sqlite3 .backup"| SNAP[("📦 Snapshot file<br/>b2-YYYYMMDD-HHMM.bak.zip<br/>AES-256")]

    SNAP -.->|stored beside replicas| USB
    SNAP -.->|stored beside replicas| LAN
```

- **Continuous**: Litestream replicates SQLite WAL frames every 60 s.
- **On-demand snapshot**: Admin clicks "Backup now" → API runs `sqlite3 .backup`, zips with AES-256 (password = the one set during install), drops a copy at every configured backup target.
- **Restore**: Admin uploads a snapshot via `POST /api/admin/restore`. Service stops accepting writes → swaps DB file → restarts. Operation logged + audit-trailed.

---

## 10. Risks Tracked at Architecture Level

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Entry-gate PC hardware failure | M | H | Litestream replica on USB + LAN; runbook to spin up replica on a supervisor PC within 30 min |
| SQLite write contention spikes | L | M | WAL mode; benchmark in Sprint 1; migration path to PostgreSQL kept clean (repository pattern) |
| .NET MAUI Windows-target tooling instability | M | M | Pin .NET SDK version; CI build on the same SDK; weekly smoke test on a real low-spec VM |
| Self-signed cert distribution | M | M | Installer pushes cert to LocalMachine/Root on every client during install |
| Background jobs miss schedule (server off) | L | L | Catch-up logic on startup (see § 5.4) |
| Audit log unbounded growth | M | L | Quarterly archive job (Phase 2) |
| Operator forgets backup password | L | H | Password stored sealed in a printed envelope per runbook; escrow procedure documented |
