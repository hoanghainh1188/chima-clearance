# Prompts — Spec Kit Slash Commands cho Chima Customs B2

> Copy-paste nguyên văn các prompt bên dưới vào Claude Code (hoặc Cursor) tương ứng với từng slash command. Đã được tinh chỉnh để tận dụng tối đa bộ spec viết tay trong `specs/`.
>
> **Đọc trước**: `BOOTSTRAP.md` để hiểu workflow tổng và thứ tự chạy.

---

## Prompt 1 — `/speckit.constitution`

> **Mục đích**: Sinh `memory/constitution.md` — văn bản nguyên tắc dự án mà mọi bước sau phải tuân thủ.

```
/speckit.constitution Tham chiếu các nguồn sau để rút ra Constitution của project Chima Customs B2:

1. specs/01-overview.md — đặc biệt § 2 (Constraints C1-C8 immutable), § 6 (Tech Stack đã chốt), § 7 (Non-Functional Targets).
2. specs/04-implementation-plan.md — § 1 (Definition of Done), § 3.3 (Code Style), § 3.4 (Dependency Rules), § 9 (ADR Process), § 10 (Security Checklist), § 11 (Performance Budget).
3. Business rules - Hướng dẫn điền sổ B2.md — toàn bộ business rules cột A-P.

Constitution phải bao gồm các nguyên tắc bắt buộc sau (KHÔNG bỏ sót):

A. CODE QUALITY
- Nullable reference types ENABLED toàn project.
- TreatWarningsAsErrors = true.
- dotnet format CI-enforced.
- Async by default cho mọi I/O; cấm .Result và .Wait().
- One public type per file.
- record cho DTOs/Value Objects; class cho Entities.

B. TESTING STANDARDS
- xUnit + FluentAssertions cho mọi test.
- Domain coverage ≥ 90%.
- Application coverage ≥ 80%.
- Mọi Gherkin scenario trong specs/03-functional-spec.md PHẢI có test tương ứng, đặt tên Should_<criterion>, gắn [Trait("Feature","F-XX")].
- API endpoint test bằng WebApplicationFactory với SQLite in-memory shared cache.

C. SECURITY
- Server-side authorization cho mọi endpoint write.
- AuditLog row cho mọi successful write command.
- BCrypt cost = 10; password min 10 ký tự, 3/4 char classes.
- JWT signing key seal bằng DPAPI.
- Backup zip AES-256.
- Default admin credentials force change ở first login.

D. ACCESSIBILITY (WCAG 2.1 AA bắt buộc)
- Min font 16sp; có toggle large text mode (20sp).
- Contrast ≥ 4.5:1 cho mọi text/background.
- Full keyboard navigation, focus ring luôn visible.
- Mọi user-facing string nằm trong Resources/Strings/AppResources.vi.resx — KHÔNG hard-code tiếng Việt trong code.

D-bis. UI WIREFRAMES BINDING (specs/05-ui-wireframes.md là tài liệu bắt buộc)
- Mọi page XAML phải bám sát layout, tab order, keyboard shortcut được định nghĩa trong specs/05-ui-wireframes.md § 6.
- Design tokens (Color.*, Font.*, Spacing.*) tại § 2-3 PHẢI khai báo dưới dạng StaticResource trong Resources/Styles/Colors.xaml và Typography.xaml — KHÔNG hard-code hex/size ngoài 2 file này.
- Mọi list view PHẢI có 4 states: Loading (skeleton), Empty (icon + action), Error (red banner + retry + correlationId), Offline (yellow banner + last-success timestamp) — theo § 8.
- Status indicators (🔴/🟢/🔵/📋) PHẢI có icon + color + text label đồng thời (D5 — không bao giờ chỉ dùng màu).
- Toast & Modal là service tái sử dụng trong B2.Desktop/Services/, không reimplement per-page.
- Vietnamese copy trong wireframes là final — copy nguyên văn vào AppResources.vi.resx.

E. ARCHITECTURE INTEGRITY
- Dependency rules theo specs/04-implementation-plan.md § 3.4 — Domain có ZERO outward references (không cả Microsoft.Extensions.*).
- Tech stack đã chốt trong specs/01-overview.md § 6 là IMMUTABLE — bất kỳ thay đổi nào (đổi library, thêm dependency lớn, đổi DB, đổi UI framework) PHẢI sinh ra ADR theo template specs/04-implementation-plan.md § 9 trước khi code.
- Không phát minh endpoint, DTO, hay feature ngoài specs/02-architecture.md § 6 và specs/03-functional-spec.md.

F. OBSERVABILITY
- Serilog mọi tier; correlation id xuyên suốt request.
- ProblemDetails (RFC 7807) cho mọi API error.
- Log levels: Information cho command success, Warning cho retried errors, Error cho unhandled exceptions.

G. OPERATIONAL CONSTRAINTS
- Footprint server < 250 MB RAM sau 24h.
- Cold start desktop app < 5s trên i5-3570/4GB.
- Client retry policy với Polly v8 (3 lần, exponential backoff).
- Idempotency-Key support cho POST /api/declarations và POST /api/vehicles/{id}/exit.

Sinh constitution.md với mỗi nguyên tắc có:
- Tên rule
- Mô tả ngắn
- Lý do (why) — trích dẫn từ spec
- Cách verify (how to enforce)
```

---

## Prompt 2 — `/speckit.specify`

> **Mục đích**: Bridge spec viết tay vào format Spec Kit. Spec Kit sẽ tự tạo branch theo feature.

```
/speckit.specify Đây là dự án PHẦN MỀM QUẢN LÝ THÔNG QUAN HẢI QUAN CỬA KHẨU CHI MA — thay thế quy trình quản lý xe nhập/xuất bằng Excel chia sẻ qua LAN bằng một hệ thống client-server modular monolith chạy trên LAN.

Toàn bộ functional specification ĐÃ ĐƯỢC VIẾT chi tiết trong specs/03-functional-spec.md với 18 features từ F-01 đến F-18, mỗi feature có Gherkin acceptance criteria. Bối cảnh và non-functional targets nằm trong specs/01-overview.md.

YÊU CẦU sinh spec.md theo format Spec Kit như sau:

1. Đọc TOÀN BỘ specs/01-overview.md, specs/03-functional-spec.md, và "Business rules - Hướng dẫn điền sổ B2.md" trước khi viết bất cứ thứ gì.

2. Phần "User Scenarios & Goals" — mô tả 3 actors (Admin / Operator / Supervisor) và goals chính của mỗi actor, lấy từ specs/01-overview.md § 3.

3. Phần "Functional Requirements" — ánh xạ 1-1 với 18 feature ID (F-01 đến F-18) trong specs/03-functional-spec.md. KHÔNG đổi tên feature, KHÔNG gộp, KHÔNG tách. Mỗi feature giữ nguyên Gherkin scenarios đã có (copy-paste, không paraphrase).

4. Phần "Non-Functional Requirements" — copy nguyên bảng từ specs/01-overview.md § 7.

5. Phần "Constraints" — copy 8 constraints C1-C8 từ specs/01-overview.md § 2.

6. Phần "Out of Scope" — copy từ specs/01-overview.md § 8.

7. Phần "Glossary" — copy từ specs/01-overview.md § 9, BỔ SUNG các thuật ngữ business từ "Business rules - Hướng dẫn điền sổ B2.md" (KVGS, dòng cha, dòng con, Sổ B2, Cổng B2).

8. Phần "Open Questions" — để trống nếu không phát hiện gap; nếu phát hiện gap so với spec hiện tại thì liệt kê rõ ràng dưới dạng câu hỏi.

QUAN TRỌNG:
- KHÔNG phát minh thêm feature ngoài 18 feature đã có.
- KHÔNG đề cập đến tech stack ở bước này (đó là việc của /speckit.plan).
- Giữ ngôn ngữ tiếng Việt cho user-facing terminology, tiếng Anh cho technical sections.
- Nếu thấy mâu thuẫn giữa spec viết tay và business rules, FLAG ra ở phần Open Questions thay vì tự quyết.
```

---

## Prompt 3 — `/speckit.clarify`

> **Mục đích**: Hỏi đáp lấp các gap còn lại. Vì spec đã chi tiết, phần này nên ngắn.

```
/speckit.clarify Đọc spec.md vừa sinh và đối chiếu với specs/01-overview.md, specs/02-architecture.md, specs/03-functional-spec.md, specs/04-implementation-plan.md.

Quy tắc clarify:
1. KHÔNG hỏi lại các câu đã được chốt trong specs/01-overview.md § 2 (Constraints) hoặc § 6 (Tech Stack).
2. KHÔNG hỏi lại các câu đã được trả lời trong Gherkin acceptance criteria của specs/03-functional-spec.md.
3. CHỈ hỏi nếu phát hiện gap thật sự — ví dụ: "Khi 2 vehicles cùng plate suffix khớp search, UI hiển thị thế nào?" (nếu spec chưa rõ).

Nếu KHÔNG có gap thực sự, ghi rõ "No clarifications needed — spec is complete" thay vì hỏi câu thừa.

Mọi câu hỏi (nếu có) phải:
- Reference rõ section trong spec gây ambiguity.
- Đưa 2-3 lựa chọn cụ thể để tôi pick, không hỏi mở.
- Ghi recommendation kèm rationale.

Sau khi tôi trả lời, ghi câu trả lời vào section "Clarifications" của spec.md với timestamp và rationale.
```

---

## Prompt 4 — `/speckit.plan`

> **Mục đích**: Sinh implementation plan. Đây là bước quan trọng nhất — phải đảm bảo agent dùng đúng kiến trúc đã thiết kế.

```
/speckit.plan Tech stack và architecture của project ĐÃ ĐƯỢC THIẾT KẾ và CHỐT trong specs/01-overview.md § 6 và specs/02-architecture.md. Hãy đọc TOÀN BỘ 2 file đó (cùng specs/04-implementation-plan.md) trước khi sinh plan.

LOCKED TECH STACK (không được thay đổi, không được suggest alternative):
- Desktop UI: .NET MAUI 8 (Windows target only Phase 1, WindowsPackageType=None để ship plain exe)
- UI Toolkit: CommunityToolkit.Maui + CommunityToolkit.Mvvm (source generators)
- Theming: Custom resource dictionaries (KHÔNG dùng third-party material lib MAUI — chưa mature)
- API: ASP.NET Core 8 Minimal API
- Background jobs: Quartz.NET hosted service
- Realtime: SignalR over LAN, fallback polling 3s
- ORM: EF Core 8 + Microsoft.Data.Sqlite
- DB: SQLite WAL mode (PRAGMA journal_mode=WAL, synchronous=NORMAL, busy_timeout=5000)
- Continuous backup: Litestream replicate ra USB + LAN folder mỗi 60s
- Auth: JWT RS256 + BCrypt.Net-Next; key seal bằng DPAPI
- Excel I/O: ClosedXML template-based
- Validation: FluentValidation
- Logging: Serilog (file + in-memory sink cho admin viewer)
- HTTP client: Refit + Polly v8 retry
- Installer: Velopack (preferred) hoặc Inno Setup
- Test: xUnit + FluentAssertions + WebApplicationFactory

REPO LAYOUT BẮT BUỘC (specs/04-implementation-plan.md § 3):
src/
- B2.Domain/                — Entities, VOs, Domain Services. Zero outward refs.
- B2.Application/           — Use cases (MediatR), DTOs, Validators, Interfaces.
- B2.Infrastructure/        — EF Core, Repos, Quartz jobs, Litestream wrapper, Excel.
- B2.Server.Api/            — ASP.NET Core Minimal API, SignalR Hub, middleware.
- B2.Server.Service/        — Windows Service host hosts B2.Server.Api.
- B2.Shared.Contracts/      — DTOs shared Server↔Desktop.
- B2.Desktop/               — .NET MAUI app.
- B2.Installer/             — Velopack/Inno Setup config.
tests/
- B2.Domain.Tests/
- B2.Application.Tests/
- B2.Server.Api.Tests/
- B2.Desktop.Tests/

DEPENDENCY RULES (specs/04-implementation-plan.md § 3.4):
Domain ← Application ← Infrastructure / Server.Api
Server.Service → Server.Api
Desktop ↔ Shared.Contracts ↔ Application
KHÔNG project nào reference Desktop. KHÔNG cycle.

DOMAIN MODEL: Sinh data-model.md theo ERD trong specs/02-architecture.md § 4. Aggregate roots: Declaration (root) ◄─ Vehicle (child), DailyHeader, User, Processor, Yard, AuditLog. 8 invariants INV-1..INV-8 phải enforced ở Domain layer.

API CONTRACTS: Sinh contracts/openapi.yaml theo bảng API surface trong specs/02-architecture.md § 6. KHÔNG thêm endpoint mới. KHÔNG đổi path. JWT trong Authorization: Bearer header. ProblemDetails (RFC 7807) cho error response.

STATE MACHINES: Sinh research.md mô tả 2 state machines từ specs/02-architecture.md § 7 (Declaration: Open → AllExited → KVGSConfirmed; Vehicle: Inside → Outside).

CROSS-CUTTING: Plan phải cover authentication, authorization, validation, audit log, logging, error handling, idempotency, concurrency (RowVersion), time (UTC storage / VN display), i18n (vi-only Phase 1), accessibility (WCAG AA) — chi tiết trong specs/02-architecture.md § 8.

UI/WIREFRAMES (BẮT BUỘC): Plan PHẢI tham chiếu specs/05-ui-wireframes.md cho phần Desktop:
- 12 page wireframes tại § 6 → mỗi page = 1 XAML + 1 ViewModel + 1 navigation route trong B2.Desktop.
- Design tokens (§ 2-3) → khai báo dưới dạng StaticResource trong Resources/Styles/Colors.xaml và Typography.xaml.
- Tab order và keyboard shortcuts → enforce qua TabIndex và keyboard accelerator (Platforms/Windows/).
- 4 states cho mọi list view (loading/empty/error/offline) → § 8.
- Toast & Modal services tái sử dụng → B2.Desktop/Services/.
- Vietnamese copy trong wireframes là final → AppResources.vi.resx.
- Accessibility AC tại § 9 → trở thành Gherkin tests trong B2.Desktop.Tests.

BACKGROUND JOBS: 2 cron jobs Quartz.NET — `0 1 0 * * ?` (CreateDailyHeader) và `0 0 23 * * ?` (GenerateDailySummary). Plan phải mô tả catch-up logic khi service vừa khởi động sau giờ scheduled.

BACKUP/RESTORE: Litestream daemon as child of Windows Service. On-demand snapshot AES-256. Restore swaps DB file với downtime ≤30s. Plan mô tả flow đầy đủ.

KHÔNG ĐƯỢC:
- Suggest đổi MAUI sang WPF/Avalonia.
- Suggest đổi SQLite sang PostgreSQL/SQL Server.
- Suggest microservices.
- Thêm library lớn ngoài stack (vd: Dapper, Mediator, MassTransit, NLog) — bất kỳ đề xuất nào phải kèm ADR template để tôi review trước.

Output: plan.md, data-model.md, contracts/openapi.yaml, research.md.
```

---

## Prompt 5 — `/speckit.tasks`

> **Mục đích**: Sinh task list theo sprint plan đã có.

```
/speckit.tasks Sinh task list theo sprint plan ĐÃ ĐƯỢC THIẾT KẾ trong specs/04-implementation-plan.md § 2 (S0-S6, 12 tuần, 6 sprints × 2 tuần).

Sprint mapping (KHÔNG đổi thứ tự, KHÔNG gộp sprint):
- S0 — Bootstrap: solution scaffold, CI pipeline, installer skeleton, 5 ADRs (ADR-0001..0005 theo § 9).
- S1 — Core domain + auth: F-01, F-02, F-03, F-04.
- S2 — Entry & exit happy path: F-05, F-06, F-07, F-08.
- S3 — KVGS + ledger + realtime: F-09, F-10.
- S4 — Background jobs + reports: F-11, F-12, F-14.
- S5 — Export, backup, audit: F-13, F-15, F-16, F-18.
- S6 — Hardening: F-17 + accessibility audit + runbook + UAT.

QUY TẮC TASK:
1. Mỗi task PR-sized (≤ 1 ngày dev, hoặc ≤ 400 LOC diff).
2. Mỗi task gắn 1 hoặc nhiều feature ID.
3. Mỗi task có acceptance criteria copy từ Gherkin tương ứng trong specs/03-functional-spec.md (KHÔNG paraphrase).
4. Mỗi task có Definition of Done check-list từ specs/04-implementation-plan.md § 1 (10 items).
5. Task phụ thuộc nhau phải mark dependency rõ ràng (vd: T-S2-03 depends on T-S1-04).
6. Task viết test phải đứng TRƯỚC task implement (TDD): vd T-S2-01 = "Write Gherkin tests for F-05", T-S2-02 = "Implement F-05 to pass tests".

TASK NUMBERING: T-S{sprint}-{seq} (vd: T-S2-01, T-S2-02, ...).

PARALLEL EXECUTION: Mark task có thể chạy song song bằng tag [P]. Task tuần tự không có tag.

OUTPUT: tasks.md với:
- Bảng tổng quan: Sprint | Task ID | Title | Feature | Estimated LOC | Dependencies | [P]?
- Chi tiết từng task: Description, Files affected, Acceptance criteria, DoD check-list.

KHÔNG SINH:
- Task bao trùm cả sprint (quá lớn).
- Task không liên quan feature nào.
- Task implement library/feature ngoài stack đã chốt.
```

---

## Prompt 6 — `/speckit.analyze`

> **Mục đích**: Cross-check spec ↔ plan ↔ tasks, phát hiện gap trước khi code.

```
/speckit.analyze Phân tích cross-artifact giữa:
1. spec.md (sinh từ /speckit.specify)
2. plan.md, data-model.md, contracts/openapi.yaml (sinh từ /speckit.plan)
3. tasks.md (sinh từ /speckit.tasks)
4. specs/01-overview.md, specs/02-architecture.md, specs/03-functional-spec.md, specs/04-implementation-plan.md (spec viết tay nguồn)

KIỂM TRA:

A. COVERAGE
- Mọi feature F-01 đến F-18 có ít nhất 1 task implement?
- Mọi Gherkin scenario có test task tương ứng?
- Mọi API endpoint trong specs/02-architecture.md § 6 có trong contracts/openapi.yaml?
- Mọi entity trong specs/02-architecture.md § 4 có trong data-model.md?
- Mọi 8 invariants INV-1..INV-8 có task enforce ở Domain layer?

B. CONSISTENCY
- Tech stack trong plan.md có khớp specs/01-overview.md § 6?
- Repo layout trong plan.md có khớp specs/04-implementation-plan.md § 3?
- Sprint sequence trong tasks.md có khớp specs/04-implementation-plan.md § 2?
- Dependency rules có vi phạm không (Domain reference outward)?

C. CROSS-DRIFT
- Task nào reference feature/endpoint không tồn tại?
- Spec mention thứ gì nhưng plan/tasks không cover?
- Plan đưa ra library/pattern ngoài stack đã chốt mà không có ADR?

D. RISK SURFACE
- Task nào có dependency chain quá dài (>5)?
- Sprint nào quá nặng (LOC tổng > 2000)?
- Có task nào mơ hồ ("Implement reports") thay vì cụ thể?
- 8 invariants Domain có bị enforce ở layer sai (vd: chỉ ở API thay vì Domain)?

E. SECURITY
- Mọi write endpoint có task viết test cho authorization policy?
- Audit log enforcement có task riêng?
- Backup AES-256 + DPAPI key sealing có task riêng?

OUTPUT: analyze-report.md gồm:
- ✅ PASS items (liệt kê ngắn gọn)
- ⚠️ WARNINGS (gap nhỏ, có thể tiếp tục nhưng cần lưu ý)
- ❌ BLOCKERS (gap nghiêm trọng, PHẢI fix trước /speckit.implement)
- Recommended fixes cho mỗi WARNING/BLOCKER.

NẾU phát hiện BLOCKER, KHÔNG được tiếp tục sang /speckit.implement. Báo rõ "BLOCKED. Fix các items sau rồi chạy lại /speckit.analyze."
```

---

## Prompt 7 — `/speckit.implement`

> **Mục đích**: Code thật. **Chạy theo từng sprint một**, KHÔNG chạy toàn bộ.

### 7.1 — Sprint S0 (Bootstrap)

```
/speckit.implement Chỉ thực hiện các task thuộc Sprint S0 (Bootstrap) trong tasks.md.

Sprint S0 deliverables (theo specs/04-implementation-plan.md § 2):
1. Solution scaffold theo specs/04-implementation-plan.md § 3.5 (chạy nguyên bộ lệnh dotnet new đã liệt kê).
2. .editorconfig với Nullable enabled, TreatWarningsAsErrors=true.
3. Wire dependency theo specs/04-implementation-plan.md § 3.4.
4. CI pipeline GitHub Actions theo specs/04-implementation-plan.md § 7 (windows-2022 runner, dotnet workload install maui).
5. Sinh 5 ADRs trong docs/adr/ theo specs/04-implementation-plan.md § 9:
   - ADR-0001: Modular Monolith
   - ADR-0002: SQLite as primary store
   - ADR-0003: .NET MAUI as desktop UI
   - ADR-0004: JWT RS256 with local user store
   - ADR-0005: Litestream for continuous backup
   Mỗi ADR phải có Context/Decision/Consequences/Alternatives đầy đủ, dựa trên rationale trong specs/01-overview.md § 4 và specs/02-architecture.md.
6. Installer skeleton (chỉ scaffold, chưa build ra installer thật).
7. README.md root project mô tả how-to-build.
8. CHANGELOG.md với entry v0.0.1.

QUY TẮC:
- Mỗi task → 1 commit theo Conventional Commits (vd: chore(s0): scaffold solution).
- Sau mỗi task: dotnet build phải xanh.
- Sau cả sprint: dotnet test (rỗng nhưng phải xanh), dotnet format --verify-no-changes phải pass.
- KHÔNG implement business logic gì ở S0.

DỪNG sau khi xong S0 và báo "Sprint S0 done. Run smoke checklist before S1." để tôi review.
```

### 7.2 — Template cho Sprint S1 đến S6

Mỗi sprint sau dùng template này, thay {SPRINT} và mô tả:

```
/speckit.implement Chỉ thực hiện các task thuộc Sprint {SPRINT} trong tasks.md.

Sprint {SPRINT} deliverables (theo specs/04-implementation-plan.md § 2):
{COPY GOAL VÀ FEATURES TỪ BẢNG SPRINT PLAN}

QUY TẮC chung cho mọi sprint implementation:

1. THỨ TỰ implement mỗi feature: Domain → Application → Infrastructure → Server.Api → Server.Service wiring → Desktop UI.

2. TDD: Mỗi Gherkin scenario trong specs/03-functional-spec.md cho features sprint này phải được viết thành failing test TRƯỚC khi implement.

3. Mỗi task → 1 commit theo Conventional Commits (vd: feat(F-05): create declaration with first vehicle).

4. AuditLog row insert cho mọi successful write — verify bằng integration test.

5. FluentValidation cho mọi command — viết validator trước implementation.

6. Vietnamese strings PHẢI vào Resources/Strings/AppResources.vi.resx — KHÔNG hard-code tiếng Việt.

7. Sau mỗi feature:
   - dotnet build -c Release /p:TreatWarningsAsErrors=true phải xanh.
   - dotnet test -c Release phải xanh, coverage Domain ≥ 90%, Application ≥ 80%.
   - dotnet format --verify-no-changes phải pass.

8. KHÔNG được:
   - Phát minh endpoint ngoài specs/02-architecture.md § 6.
   - Thêm library ngoài stack đã chốt mà không có ADR mới (theo specs/04-implementation-plan.md § 9).
   - Skip Gherkin acceptance criteria nào.
   - Hard-code timestamp/UUID trong test (dùng test fixture).

9. Sau cả sprint, chạy smoke checklist 10 bước theo specs/04-implementation-plan.md § 8 (mô tả các bước nếu chưa thực thi được trên VM).

DỪNG sau khi xong sprint và báo "Sprint {SPRINT} done. Test results: {summary}. Ready for review." để tôi review.
```

### 7.3 — Ví dụ điền cho Sprint S2

```
/speckit.implement Chỉ thực hiện các task thuộc Sprint S2 (Entry & exit happy path) trong tasks.md.

Sprint S2 deliverables:
- F-05: Vehicle entry tại Entry Gate (cột A-H, I theo Sổ B2)
- F-06: Add child vehicle vào declaration đã tồn tại
- F-07: Vehicle search by plate suffix (3-4 số cuối)
- F-08: Mark vehicle exit tại Exit Gate (cột J, K)

[GIỮ NGUYÊN MỤC "QUY TẮC chung" Ở TRÊN]

DỪNG sau khi xong sprint và báo "Sprint S2 done." để tôi review.
```

---

## Phụ lục — Re-prompts khi cần

### Re-run sau khi sửa spec

```
Tôi vừa sửa specs/03-functional-spec.md (cụ thể: F-XX). Hãy re-sync spec.md, plan.md, tasks.md với thay đổi này. Chỉ thay đổi phần liên quan, không re-generate toàn bộ.
```

### Re-sync sau khi BỔ SUNG file wireframe (specs/05-ui-wireframes.md)

> **Tình huống**: Đã chạy /speckit.constitution → /speckit.specify → /speckit.clarify, sau đó MỚI bổ sung specs/05-ui-wireframes.md. Cần inject UI constraint vào artifacts đã sinh, không re-run toàn bộ workflow.

**Bước 1** — Update Constitution để đưa wireframes vào ràng buộc (re-run constitution với scope nhỏ):

```
/speckit.constitution Update mode (KHÔNG re-generate toàn bộ). Chỉ THÊM 1 nguyên tắc mới vào memory/constitution.md hiện có:

D-bis. UI WIREFRAMES BINDING
- Mọi page XAML phải bám sát layout, tab order, keyboard shortcut định nghĩa trong specs/05-ui-wireframes.md § 6 (12 page wireframes).
- Design tokens (Color.*, Font.*, Spacing.*) tại § 2-3 PHẢI khai báo StaticResource trong Resources/Styles/Colors.xaml và Typography.xaml — KHÔNG hard-code hex/size ngoài 2 file này.
- Mọi list view PHẢI có 4 states: Loading (skeleton) / Empty (icon + action) / Error (red banner + retry + correlationId) / Offline (yellow banner + last-success timestamp) — § 8.
- Status indicators PHẢI dùng icon + color + text label đồng thời (không bao giờ chỉ màu — D5).
- Toast & Modal là service tái sử dụng trong B2.Desktop/Services/.
- Vietnamese copy trong wireframes là final — copy nguyên văn vào AppResources.vi.resx.

KHÔNG đụng vào các nguyên tắc A-G hiện có. KHÔNG re-format các section khác. Chỉ append nguyên tắc D-bis.
```

**Bước 2** — Re-sync spec.md (chỉ thêm UI references, không re-generate):

```
Tôi vừa bổ sung specs/05-ui-wireframes.md (12 page wireframes, design tokens, accessibility AC).

Đọc file đó và update spec.md hiện có (sinh từ /speckit.specify trước đó) như sau:

1. Trong section "User Scenarios & Goals" — bổ sung dòng: "UI wireframes detailed in specs/05-ui-wireframes.md § 6."

2. Trong section "Functional Requirements" — với mỗi feature F-XX, thêm dòng "UI: see specs/05-ui-wireframes.md § 6.X" theo bảng cross-reference tại § 10 của wireframe doc.

3. THÊM section mới "UI Acceptance Criteria" — copy 5 Gherkin scenarios từ specs/05-ui-wireframes.md § 9 (WCAG 2.1 AA compliance).

4. Trong section "Non-Functional Requirements" — bổ sung: "Design tokens (colors, typography, spacing) defined in specs/05-ui-wireframes.md § 2-3 are binding."

KHÔNG re-generate spec.md từ đầu. Chỉ APPEND/INSERT các điểm trên. Giữ nguyên Gherkin scenarios feature đã có.
```

**Bước 3** — Re-run /speckit.clarify với scope giới hạn UI:

```
/speckit.clarify Đã bổ sung specs/05-ui-wireframes.md sau khi /speckit.clarify lần đầu chạy.

QUY TẮC re-clarify:
1. Đọc spec.md hiện có VÀ specs/05-ui-wireframes.md.
2. CHỈ hỏi câu liên quan tới UI/wireframe — bỏ qua mọi câu đã clarified ở lần đầu.
3. Nếu wireframe không có gap so với spec, ghi "No additional UI clarifications needed" và dừng.
4. Câu hỏi (nếu có) phải reference rõ section của wireframe gây ambiguity.

Mọi câu trả lời mới ghi vào "Clarifications" section của spec.md với prefix "[UI]" để phân biệt với clarification lần đầu.
```

**Bước 4** — Tiếp tục workflow bình thường:

```
/speckit.plan
```

Plan prompt đã được update để tham chiếu wireframes (xem Prompt 4 phía trên — section "UI/WIREFRAMES (BẮT BUỘC)"). Sau plan thì /speckit.tasks → /speckit.analyze → /speckit.implement như bình thường.

### Sửa task cụ thể

```
Task T-S2-04 trong tasks.md cần update vì spec F-07 đã rõ hơn về behavior khi có nhiều match (xem specs/03-functional-spec.md F-07 scenario "Multiple matches"). Update task để bao gồm yêu cầu UI hiển thị list selectable thay vì auto-pick.
```

### Tạo ADR mới

```
Tôi cần ADR mới cho việc {decision}. Tham khảo template ở specs/04-implementation-plan.md § 9 và format giống các ADR-0001..0005 đã có. Đặt tên file docs/adr/ADR-{NNNN}-{short-title}.md với NNNN là số tiếp theo trong dãy.
```

### Force re-read specs khi agent quên

```
Trước khi tiếp tục, hãy đọc LẠI specs/01-overview.md § 6 (locked tech stack) và specs/04-implementation-plan.md § 3.4 (dependency rules). Confirm rằng implementation hiện tại không vi phạm bất kỳ ràng buộc nào trong 2 mục này.
```

### Khi agent đề xuất library ngoài stack

```
Bạn vừa đề xuất {library}. Library này KHÔNG có trong stack đã chốt (specs/01-overview.md § 6). Hoặc:
(A) Sinh ADR theo specs/04-implementation-plan.md § 9 giải thích lý do BẮT BUỘC đổi, để tôi review.
(B) Refactor lại dùng library trong stack hiện có.

Default chọn (B) trừ khi bạn có lập luận thật mạnh cho (A).
```
