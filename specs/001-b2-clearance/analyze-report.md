# Analysis Report — Cross-Artifact Audit

**Feature**: `001-b2-clearance` · **Date**: 2026-05-03 · **Mode**: read-only
**Inputs analysed**:

- `specs/001-b2-clearance/spec.md` (28 FRs, 16 SCs, 6 NFRs, 18 user stories)
- `specs/001-b2-clearance/plan.md`, `research.md`, `data-model.md` (12 invariants — INV-1..INV-12)
- `specs/001-b2-clearance/contracts/openapi.yaml` (42 documented operations: 35 `locked`, 7 `pending-adr-0006`)
- `specs/001-b2-clearance/tasks.md` (113 tasks across 7 sprint phases)
- Source: `specs/01-overview.md`, `specs/02-architecture.md`, `specs/03-functional-spec.md`, `specs/04-implementation-plan.md`, `specs/05-ui-wireframes.md`
- Constitution: `.specify/memory/constitution.md` v1.1.0

---

## Verdict

**Status**: ⚠️ **PROCEED WITH WARNINGS**. **No BLOCKERS.** Seven WARNINGS (M1..M7),
three HIGH (W1..W3), one LOW (L1). Implementation may begin in **Sprint S0** without
delay; the highest-impact item (W2 — undeclared library) should be resolved during
S0 alongside ADR-0006.

---

## Detection Pass Results

### A. Coverage

| Check | Result | Evidence |
| --- | --- | --- |
| A.1 — Every F-01..F-18 has ≥ 1 implement task | ✅ PASS | All 18 IDs appear as `[F-XX]` story labels across tasks.md (46 label occurrences) |
| A.2 — Every per-feature Gherkin scenario has a test task | ✅ PASS | One "Write Gherkin tests for F-XX" task per feature (T-S1-10, T-S1-17, T-S1-21, T-S2-06, T-S2-13, T-S2-17, T-S2-19, T-S3-01, T-S3-05, T-S4-02, T-S4-05, T-S4-08, T-S4-11, T-S5-01, T-S5-07, T-S5-14, T-S5-16, T-S5-18, T-S6-01, T-S6-05, T-S6-08) |
| A.2 — Cross-Feature ACs (3 scenarios in spec.md) have system-wide tests | ⚠️ WARNING M4 | Audit-on-every-write is enforced per task by DoD-8 + AuditMiddleware (T-S1-07), but no single sweep test asserts FR-003 globally. Same gap for "Every error surfaces correlationId". |
| A.3 — Every locked endpoint in `02-architecture.md § 6` is in `contracts/openapi.yaml` | ✅ PASS | All 29 locked § 6 endpoints present, plus 7 `pending-adr-0006` extensions justified in `research.md § 8` |
| A.4 — Every entity in `02-architecture.md § 4` is in `data-model.md` | ✅ PASS | All 7 source entities (User, Processor, Yard, Declaration, Vehicle, DailyHeader, AuditLog) + IdempotencyKey (justified in research § 2.7) |
| A.5 — All INV-1..INV-8 enforced at Domain layer | ✅ PASS | Per `data-model.md § 4` table: T-S2-07 enforces INV-1/2/3, T-S2-20 INV-4, T-S2-07/T-S2-20 INV-5, T-S3-02 INV-6, T-S2-09 INV-7 (Application layer — see note), T-S1-11/T-S2-07 INV-8 |

> **Note on INV-7**: enforced in `B2.Application` validator rather than `B2.Domain`
> because Domain entities cannot query repositories. Acknowledged in
> `data-model.md § 4` and consistent with Constitution V (no rule violation).

### B. Consistency

| Check | Result | Evidence |
| --- | --- | --- |
| B.1 — Tech stack in `plan.md` matches `01-overview.md § 6` | ⚠️ LOW L1 | Matches the *combined* source (01-overview § 6 + 04-impl-plan § 6). 01-overview § 6 alone is curiously incomplete (omits NSubstitute, NBomber). Plan correctly uses 04-impl-plan as the broader source. |
| B.2 — Repo layout in `plan.md` matches `04-implementation-plan.md § 3` | ✅ PASS | All 8 production projects + 5 test projects match. `B2.Installer` is in `02-architecture.md § 3`; `B2.Desktop.Tests` and `B2.Performance.Tests` extend the test layer per `04-impl-plan § 6`. |
| B.3 — Sprint sequence in `tasks.md` matches `04-implementation-plan.md § 2` | ⚠️ MEDIUM M1 | Sprint headers and feature mapping match exactly. But F-03 / F-04 *implementation* tasks carry `T-S2-01..05` IDs (S2 phase) instead of `T-S1-22..26`. Tests for F-03/F-04 are in S1 (T-S1-21). The deferral is documented in tasks.md but the ID prefix drifts the sprint label. |
| B.4 — Dependency rules violations (Domain → outward) | ✅ PASS | T-S0-04 ArchUnit assertion + plan project structure + data-model.md § 4 layer-ownership notes. No violations. |

### C. Cross-drift

| Check | Result | Evidence |
| --- | --- | --- |
| C.1 — Tasks reference non-existent endpoints | ⚠️ HIGH W3 | T-S4-07 references `POST /api/daily-headers/{date}/recompute` — **not** in `contracts/openapi.yaml` and not in `02-architecture.md § 6` or ADR-0006 draft. F-12 spec asks for "Re-run on demand" but the endpoint shape is undefined. |
| C.2 — Spec mentions things plan/tasks don't cover | ⚠️ MEDIUM M2, M3, M5, M6 | (M2) NFR-001 design tokens have no first-class "Create `Resources/Styles/Colors.xaml` + `Typography.xaml` with all tokens from § 2-3" bootstrap task — implicit in T-S1-15 onward. (M3) NFR-003 four list-view states have no reusable component task — each list view implements its own. (M5) Edge case "User deactivated mid-session → next call returns 401" has no explicit test. (M6) Edge case "Cancellation race vs exit" has no explicit concurrency test (FR-024 covers cross-client exit race; cancel-vs-exit is its own race). |
| C.3 — Plan introduces library outside locked stack without ADR | ⚠️ HIGH W2 | `ArchUnitNET` is referenced by plan.md, T-S0-04, T-S4-15. Not in `01-overview.md § 6`, not in `04-impl-plan § 6`, not in research.md § 8 ADR-0006 draft. Constitution II requires an ADR for any dependency outside § 6. |

### D. Risk surface

| Check | Result | Evidence |
| --- | --- | --- |
| D.1 — Tasks with dependency chain > 5 hops | ✅ PASS (with caveat) | Longest intra-feature chain is 5 hops (T-S2-22 → T-S2-21 → T-S2-20 → T-S2-08 → T-S2-07 → T-S0-04). T-S6-12 (UAT) has unbounded depth by nature — accepted. |
| D.2 — Sprints with > 2 000 LOC budget | ⚠️ HIGH W1 | 5 of 7 sprints exceed: S1 ~3 500, S2 ~4 500, S3 ~2 500, S4 ~2 800, S5 ~3 800. Within the 12-week timeline this means each sprint averages 4.4 LOC/dev-hour assuming a 2-dev team — aggressive but defensible if every task ships PR-sized as required. |
| D.3 — Vague task descriptions ("Implement reports") | ✅ PASS | Every task names file paths, dependencies, and DoD pointer. T-S5-19 is the largest at ~450 LOC across HealthQueryHandler + LogsQueryHandler + endpoints + page — at the edge of the 400-LOC PR cap, recommend splitting in execution. |
| D.4 — Invariants enforced at wrong layer | ✅ PASS | INV-1..INV-6 + INV-8 in Domain (T-S2-07, T-S2-20, T-S3-02). INV-7 in Application validator (T-S2-09) — justified above. INV-9..INV-12 added during clarify all live in Domain (Declaration.Cancel) or Infrastructure (AnnualArchiveJob). |

### E. Security

| Check | Result | Evidence |
| --- | --- | --- |
| E.1 — Every write endpoint has a 403-policy test | ⚠️ MEDIUM M7 | Coverage is partial: T-S1-17 (Operator-on-Users), T-S3-01 (Operator-on-KVGS), T-S4-11 (Operator-on-Reports/Export), T-S5-14 (Operator/Supervisor-on-Cancel) all explicit. **Missing explicit tests** for Supervisor-on-Entry (T-S2-06), Supervisor-on-Add-Vehicle (T-S2-13), Supervisor-on-Mark-Exit (T-S2-19). T-S4-15 ArchUnit asserts the policy is wired; it does not assert the 403 response payload. |
| E.2 — AuditLog enforcement has a dedicated task | ✅ PASS | T-S1-07 (AuditMiddleware) + DoD-8 per task. (System-wide assertion gap noted in M4.) |
| E.3 — Backup AES-256 + DPAPI key sealing have dedicated tasks | ✅ PASS | T-S5-09 (AES-256 + key-wrap with FR-026 dual-secret) + T-S1-12 (JWT signing key sealed via DPAPI). |

---

## Findings table (12 findings)

| ID | Category | Severity | Location | Summary | Recommendation |
| --- | --- | --- | --- | --- | --- |
| **W1** | Risk surface | HIGH | `tasks.md` overview table | Five sprints exceed 2 000 LOC budget (S1 3 500, S2 4 500, S3 2 500, S4 2 800, S5 3 800) | Re-estimate after S0 actuals; consider splitting T-S2-07 (~400) and T-S5-19 (~450) into smaller tasks; track LOC/sprint as a sprint-review KPI. **Do not** dilute scope to fit the budget — the schedule will need re-baselining if the budget binds. |
| **W2** | Cross-drift | HIGH | `plan.md` Constitution Check, `tasks.md` T-S0-04, T-S4-15 | `ArchUnitNET` referenced as an architecture-test library but is **not** in the locked stack (`01-overview.md § 6` / `04-impl-plan § 6`) and not in ADR-0006 | Either (a) extend ADR-0006 to add ArchUnitNET as an approved test-only dependency (smallest change), (b) substitute manual reflection-based architecture tests in xUnit (no library), or (c) author ADR-0007. **Block merge of T-S0-04 until resolved.** |
| **W3** | Cross-drift | HIGH | `tasks.md` T-S4-07 | References `POST /api/daily-headers/{date}/recompute` — **not** in `contracts/openapi.yaml` or ADR-0006 draft | Either add the endpoint to ADR-0006 + `contracts/openapi.yaml` (Admin-only per FR-027 admin-overrides row), or recast F-12 "Re-run on demand" as a Quartz manual trigger from the Admin Health page (no new endpoint). Recommend the former — it's tiny (one endpoint, Admin policy, 1 handler). |
| **M1** | Consistency | MEDIUM | `tasks.md` T-S2-01..05 | F-03 / F-04 implementation tasks carry S2 IDs; their tests are in S1 | Either renumber to T-S1-22..26 (clean), or keep the current layout and add a clearer banner — the directive forbids "đổi thứ tự sprint" but allows distributing tasks within a sprint. The current explanatory note is adequate; this is a documentation-only nit. |
| **M2** | Cross-drift | MEDIUM | NFR-001 binding (`spec.md`) vs `tasks.md` | No first-class "Create `Resources/Styles/Colors.xaml` and `Typography.xaml` with all tokens from wireframes § 2-3" task | Insert **T-S1-09a** (or fold into T-S1-15) as: "Author Colors.xaml + Typography.xaml from `05-ui-wireframes.md § 2-3`; one StaticResource per token; verified-contrast comments". Required before the first XAML page that uses any token. |
| **M3** | Cross-drift | MEDIUM | NFR-003 (`spec.md`) vs `tasks.md` | No task creates a *reusable* four-state list component; each list view implements its own | Add T-S3-09a: "Implement reusable `ListStateView<T>` user-control rendering Loading / Empty / Error / Offline per § 8" before T-S3-10 (Ledger). Saves duplication in T-S4-14 (Reports), T-S5-17 (Audit), T-S5-19 (Logs). |
| **M4** | Coverage | MEDIUM | spec.md "Cross-Feature Acceptance Criteria" | "Every successful POST/PUT/DELETE/PATCH writes exactly one AuditLog row" has no system-wide assertion test | Add to T-S5-17 or as new T-S5-17a: "Sweep test in `B2.Server.Api.Tests` enumerates all endpoints via `EndpointDataSource`, makes one successful call to each write endpoint, asserts `AuditLog` row count delta = 1". Same pattern for "Every error surfaces correlationId". |
| **M5** | Coverage | MEDIUM | spec.md Edge Cases — "User deactivated mid-session" | No explicit test for "next API call after deactivation returns 401" | Add to T-S1-17 (UsersEndpointTests): one new scenario `Should_Return_401_On_Next_Call_After_Deactivation`. Tag `[Trait("Feature","F-02")]`. |
| **M6** | Coverage | MEDIUM | spec.md Edge Cases — "Cancellation race vs exit" | No explicit test for the cancel-vs-exit race (separate from FR-024 cross-client exit race) | Add to T-S5-14 (CancelDeclarationEndpointTests): scenario `Should_Reject_Exit_When_Declaration_Cancelled_Concurrently` returning `DECLARATION_NOT_OPEN`. Tag `[Trait("Requirement","FR-022")]`. |
| **M7** | Security | MEDIUM | FR-027 role matrix vs `tasks.md` | Supervisor-on-Entry / Supervisor-on-Add-Vehicle / Supervisor-on-Mark-Exit 403 tests not explicit | Extend T-S2-06, T-S2-13, T-S2-19 each with one Supervisor-403 scenario per FR-027 row. Three small additions; no new tasks needed. |
| **L1** | Consistency | LOW | `01-overview.md § 6` | NSubstitute and NBomber not listed in § 6, but listed in `04-impl-plan § 6` and used in plan/tasks | Source-doc-only inconsistency. Either patch `01-overview.md § 6` to mention them, or accept that `04-impl-plan § 6` is the broader source (which is how plan.md treats it). No code impact. |
| **D-caveat** | Risk surface | LOW | `tasks.md` T-S6-12 | UAT task has unbounded dependency depth (depends on "all prior") | Inherent to the last task of the project; not a real risk. Recorded for completeness. |

---

## Coverage summary table (top requirements)

| Requirement key | Has task? | Task IDs | Notes |
| --- | --- | --- | --- |
| F-01 Login | ✅ | T-S1-10..16 | Forced password change (FR-017) covered by T-S1-15 |
| F-02 Users | ✅ | T-S1-17..20 | M5: add 401-after-deactivation test |
| F-03 Processors | ✅ | T-S1-21, T-S2-01,02,05 | M1: implementation tasks carry S2 IDs |
| F-04 Yards | ✅ | T-S1-21, T-S2-03,04,05 | Same as F-03 |
| F-05 Entry | ✅ | T-S2-06..12 | M7: add Supervisor-403 to T-S2-06 |
| F-06 Add child vehicle | ✅ | T-S2-13..16 | M7: add Supervisor-403 to T-S2-13 |
| F-07 Suffix search | ✅ | T-S2-17..18, T-S2-22 | Performance scenario covered (50 k row test) |
| F-08 Mark exit | ✅ | T-S2-19..22 | M7: add Supervisor-403 to T-S2-19; FR-024 covered |
| F-09 KVGS | ✅ | T-S3-01..04 | F-09 Operator-403 already covered ✓ |
| F-10 Ledger | ✅ | T-S3-05..12, T-S5-15 | Writes-during-🟡 covered via T-S3-05 |
| F-11 Daily header | ✅ | T-S4-02..04, T-S4-08..10 | Inline officer name (FR-025) included |
| F-12 Daily summary | ✅ | T-S4-05..07 | W3: T-S4-07 references undeclared endpoint |
| F-13 Excel export | ✅ | T-S5-01..06, T-S5-15 | Đã bật badges per Q3; cancellation strike-through per T-S5-15 |
| F-14 Reports | ✅ | T-S4-11..14 | Operator-403 covered ✓ |
| F-15 Backup & restore | ✅ | T-S5-07..13, T-S6-08..09 | FR-026 dual-secret + annual archive included |
| F-16 Audit log viewer | ✅ | T-S5-16..17 | M4: add system-wide audit-coverage sweep here |
| F-17 Smart suggestions | ✅ | T-S6-01..04 | Simple-v1 scope per Q2 |
| F-18 Health & logs | ✅ | T-S5-18..19 | Recovery-code-acknowledged flag included |
| FR-022 Cancellation | ✅ | T-S5-14..15 | M6: add cancel-vs-exit race test |
| FR-024 Cross-client exit race | ✅ | T-S2-19..20 | AlreadyExitedProblem schema in openapi.yaml |
| FR-025 Officer name | ✅ | T-S4-08..10 | INV-11 (today only) covered |
| FR-026 Recovery code | ✅ | T-S5-07, T-S5-09, T-S5-13 | Install-time generator + dual-wrap |
| FR-027 Role matrix | ✅ | T-S4-15 (ArchUnit) + per-endpoint | M7: 3 missing 403 tests |
| FR-028 Tri-state connectivity | ✅ | T-S3-09 | Writes-in-🟡 covered |
| NFR-001 Design tokens binding | ⚠️ | implicit in T-S1-15+ | M2: add first-class bootstrap task |
| NFR-002 Wireframe page fidelity | ✅ | every UI task references its § 6.X | — |
| NFR-003 Four list-view states | ⚠️ | per-list-view (no reusable) | M3: extract `ListStateView<T>` |
| NFR-004 Status indicators (icon+color+text) | ✅ | T-S3-10 + every status badge | — |
| NFR-005 Reusable Toast/Modal | ✅ | T-S3-12 | — |
| NFR-006 Vietnamese copy verbatim | ✅ | DoD-6 + per-page tasks | — |

---

## Constitution alignment

| Principle | Status | Evidence |
| --- | --- | --- |
| I — Constraint-Driven Architecture (C1–C8) | ✅ | All eight constraints mapped to plan + tasks |
| II — Locked Tech Stack & ADR Governance | ⚠️ W2 | ArchUnitNET added without ADR — must resolve |
| III — Code Quality (Nullable + TWAE) | ✅ | T-S0-02 enforces via Directory.Build.props |
| IV — Test-First Discipline | ✅ | Every feature has a test-first task; coverage thresholds binding |
| V — Layered Dependency Rules | ✅ | T-S0-04 ArchUnit assertion (subject to W2 resolution) |
| VI — Vietnamese-First UX & Accessibility | ✅ | DoD-6 + every UI task; T-S6-05/06 a11y audit; M2 noted |
| VII — Security & Auditability | ⚠️ M4, M7 | All controls present; gap is in test coverage of two cells + system-wide audit sweep |
| VIII — UI Wireframes Binding | ⚠️ M2, M3 | Per-page tasks reference wireframes; bootstrap-token task and reusable list-state component absent |

**No principle is violated outright.** All deviations are gaps in test coverage or
scaffolding tasks, not architectural violations.

---

## Unmapped tasks

None. Every one of the 113 tasks maps to either a Feature ID, a cross-cutting
FR/NFR, infrastructure (S0), or quality gates (S6 polish/UAT/runbook).

---

## Metrics

- **Total Requirements (FR + SC + NFR + INV)**: 28 + 16 + 6 + 12 = **62**
- **Total Tasks**: **113**
- **Coverage**: 18/18 features = **100 %**; 12/12 invariants = **100 %**;
  6/6 NFRs = **100 % covered** (NFR-001 + NFR-003 with caveats — see M2/M3)
- **Endpoints**: 35 locked + 7 ADR-0006 = **42 documented operations**
- **Ambiguity Count**: 0 vague task descriptions
- **Duplication Count**: 0 duplicated requirements
- **Critical Issues**: **0**
- **High Issues**: 3 (W1, W2, W3)
- **Medium Issues**: 7 (M1..M7)
- **Low Issues**: 1 (L1)

---

## Next Actions

### Before `/speckit.implement` (Sprint S0)

1. **Resolve W2 (ArchUnitNET)**. Pick (a) extend ADR-0006, (b) drop ArchUnitNET
   for hand-rolled reflection tests, or (c) author ADR-0007. **Recommended**:
   (a) — smallest change, keeps the plan honest about what S0 will install.
2. **Resolve W3 (T-S4-07 endpoint)**. Pick (a) add `POST /api/daily-headers/{date}/recompute`
   to ADR-0006 + openapi.yaml, or (b) recast F-12 "Re-run on demand" as a Quartz
   manual trigger from the Health page. **Recommended**: (a).
3. **Acknowledge W1 (sprint LOC)**. Re-baseline at S0 actuals; track LOC/sprint as a
   sprint-review KPI; no scope dilution.

### During Sprint S1 (small ergonomic fixes)

4. **M2** — fold "Create Colors.xaml + Typography.xaml from § 2-3" into S1 (e.g., as
   a new T-S1-09a or before T-S1-15).
5. **M5** — add 401-after-deactivation scenario to T-S1-17.
6. **M7** — extend T-S2-06 / T-S2-13 / T-S2-19 with Supervisor-403 cases.

### During Sprints S3 / S5 / S6

7. **M3** — add T-S3-09a "ListStateView<T> reusable component" before T-S3-10.
8. **M4** — extend T-S5-17 with the audit-sweep + correlation-id-sweep system tests.
9. **M6** — add cancel-vs-exit race scenario to T-S5-14.

### No action required

10. **L1** is documentation-only.
11. **M1** is a documentation nit (the deferral note in tasks.md is adequate).

---

## Per-directive verdict (PASS / WARNING / BLOCKER)

### ✅ PASS items (10)

1. All 18 features (F-01..F-18) have ≥ 1 implementation task.
2. Every feature has a "Write Gherkin tests" task scheduled before its
   implementation tasks (TDD enforced).
3. All 29 locked endpoints from `02-architecture.md § 6` present in
   `contracts/openapi.yaml`.
4. All 7 source entities present in `data-model.md` (plus IdempotencyKey
   justified in research § 2.7).
5. INV-1..INV-8 enforced at correct layer (Domain except INV-7 in Application
   validator, justified).
6. Tech stack in plan matches the combined source.
7. Repo layout matches `04-impl-plan § 3`.
8. Sprint sequence (S0..S6) and feature-to-sprint mapping match
   `04-impl-plan § 2`.
9. No Domain → outward dependency violations.
10. AES-256 backup + DPAPI key sealing both have dedicated tasks (T-S5-09,
    T-S1-12).

### ⚠️ WARNINGS (10)

| ID | Summary | Severity | Recommended fix |
| --- | --- | --- | --- |
| W1 | 5 sprints exceed 2 000 LOC budget | HIGH | Track as KPI; split T-S2-07 / T-S5-19 in execution |
| W2 | ArchUnitNET added without ADR | HIGH | Extend ADR-0006 to include it (Sprint S0 task) |
| W3 | T-S4-07 references undeclared endpoint | HIGH | Add `POST /api/daily-headers/{date}/recompute` to ADR-0006 + openapi.yaml |
| M1 | F-03/F-04 impl tasks labeled S2 | MEDIUM | Doc nit — current note is adequate |
| M2 | NFR-001 design tokens lack bootstrap task | MEDIUM | Insert T-S1-09a or fold into T-S1-15 |
| M3 | NFR-003 list-view states not reusable | MEDIUM | Insert T-S3-09a `ListStateView<T>` |
| M4 | No system-wide audit-sweep test | MEDIUM | Extend T-S5-17 |
| M5 | "User deactivated mid-session" untested | MEDIUM | Extend T-S1-17 |
| M6 | "Cancellation race vs exit" untested | MEDIUM | Extend T-S5-14 |
| M7 | 3 missing Supervisor-403 tests on Entry/Add-Vehicle/Mark-Exit | MEDIUM | Extend T-S2-06 / T-S2-13 / T-S2-19 |

### ❌ BLOCKERS

**None.** Implementation may proceed to `/speckit.implement` Sprint S0. W2 and W3
should be resolved during S0 (alongside the existing ADR-0006 authoring task
T-S0-11) before any task that depends on them ships.

---

## Resolution Log (updated 2026-05-05 — post-S4 merge)

The findings below have a status snapshot now that Sprints S0–S4 have all
merged to `main`. The original 2026-05-03 audit text is left intact above for
historical traceability; this section is the authoritative current state.

| ID | Original verdict | Status (2026-05-05) | Evidence |
| --- | --- | --- | --- |
| **W1** | HIGH — 5 sprints exceed 2 000 LOC budget | 🟢 ACCEPTED | S0–S4 actuals tracked. Re-baseline KPI per-sprint review; no scope dilution. |
| **W2** | HIGH — ArchUnitNET added without ADR | 🟢 RESOLVED (alt) | T-S0-04 + T-S4-15 shipped using `Microsoft.AspNetCore.Routing.EndpointDataSource` reflection in `tests/B2.Server.Api.Tests/Architecture/{LayerRulesTests,EndpointPolicyAlignmentTests}.cs`. ArchUnitNET dependency was **not** added; no new ADR needed. The "ArchUnit" label in `tasks.md` is naming-only. (Strengthening to FR-027 specificity is tracked as `T-S4-CO-01`.) |
| **W3** | HIGH — T-S4-07 endpoint undeclared | 🟢 RESOLVED | `POST /api/daily-headers/{date}/recompute` shipped in PR #18 (`9f0d15b`) under `B2Policies.AdminOnly`. Endpoint lives in `src/B2.Server.Api/Endpoints/DailyHeadersEndpoints.cs`; openapi.yaml + ADR-0006 still need a parity update — captured below as **doc-A1**. |
| **M1** | MEDIUM — F-03/F-04 impl tasks labeled S2 | 🟢 ACCEPTED | Doc nit. Tasks shipped on schedule under S2 IDs. |
| **M2** | MEDIUM — NFR-001 design tokens lack bootstrap task | 🟢 RESOLVED | `Resources/Styles/Colors.xaml` + `Typography.xaml` shipped in S3 (PRs #6, #7). Constitution VI compliance asserted by manual review — no hard-coded hex/sizes outside the token files. |
| **M3** | MEDIUM — NFR-003 list-view states not reusable | 🟡 PARTIAL | PR #16 (R3e) shipped `Views/Common/SkeletonRows` + `Views/Common/ErrorCodeBanner` as reusable building blocks across Ledger/Kvgs/Users/MasterData/Exit. A single end-to-end `ListStateView<T>` wrapper is **not** present; each page composes the four states manually. Consider follow-up under `T-S5-CO` if Audit/Logs lists in S5 grow the duplication. |
| **M4** | MEDIUM — system-wide audit-sweep test | 🔴 OPEN | Targeted at S5 audit-viewer task T-S5-17. Carry-over. |
| **M5** | MEDIUM — 401-after-deactivation untested | 🔴 OPEN | Carry-over to S5 hardening. |
| **M6** | MEDIUM — cancel-vs-exit race untested | 🔴 OPEN | Targeted at S5 cancel task T-S5-14. Carry-over. |
| **M7** | MEDIUM — 3 missing Supervisor-403 tests | 🔴 OPEN | Extend T-S2-06 / T-S2-13 / T-S2-19. Sprint S5 hardening. |
| **L1** | LOW — `01-overview.md § 6` doc nit | 🟢 ACCEPTED | No code impact. |
| **D-caveat** | LOW — UAT depth | 🟢 ACCEPTED | Inherent. |

### S4 carry-overs surfaced after the original audit

These items came out of the PR #18 code-review pass and live as
`T-S4-CO-01..09` in `tasks.md` Phase 5 tail. They are slated for early
Sprint S5:

- **T-S4-CO-01** Strengthen `EndpointPolicyAlignmentTests` to verify each
  route maps to the *specific* FR-027 policy (not just any auth attribute).
- **T-S4-CO-02** Add F-11 catch-up-on-startup Gherkin test.
- **T-S4-CO-03** Multi-day catch-up in `DailyHeaderCatchupHostedService`
  (currently catches up *today* only).
- **T-S4-CO-04** `ReportsRepository.GetByOfficerAsync` — wrap two
  `ToListAsync` calls in a snapshot transaction; surface officers who only
  KVGS-confirmed (currently dropped from the projection).
- **T-S4-CO-05** Drop redundant `.Include(v => v.Declaration)` from
  `BaseVehicleQuery`.
- **T-S4-CO-06** Consolidate `ResolveVietnamTimeZone` (3 duplicates → 1).
- **T-S4-CO-07** Move `UpdateOfficerNameCommandValidator` English literal
  to a resource key (Constitution VI).
- **T-S4-CO-08** Parallelise `ReportsViewModel.LoadAsync` with `Task.WhenAll`.
- **T-S4-CO-09** `ConnectivityService.Dispose` should also dispose the
  `HubConnection`.

### Doc-only follow-ups

- **doc-A1** Update `contracts/openapi.yaml` and `ADR-0006` to declare the
  three S4 endpoints (`POST /api/daily-headers/{date}/recompute`,
  `PATCH /api/daily-headers/{date}/officer`, `GET /api/daily-headers`) as
  shipped. Currently they live in code only. Cleanup, no behavioural impact.

### Sprint completion status (2026-05-05)

| Sprint | Tasks | PRs | Status |
| --- | --- | --- | --- |
| S0 | T-S0-01..11 | #2 (`fab8c55`) | ✅ Merged |
| S1 | T-S1-01..21 | #4 (`3407a5c`) | ✅ Merged |
| S2 | T-S2-01..22 | #5 (`de24ec5`) | ✅ Merged |
| S3 | T-S3-01..12 | #6, #7, #8, #9, #11, #12, #15, #16 (+ #13 CI fix) | ✅ Merged |
| **S4** | **T-S4-01..15** | **#18 (`9f0d15b`) + #19 docs (`6514c2e`)** | ✅ **Merged** |
| S5 | T-S5-01..19 | — | ⏳ Pending |
| S6 | T-S6-01..12 | — | ⏳ Pending |

Carry-overs (`T-S4-CO-01..09`) are tracked at the tail of Phase 5 in
`tasks.md` and will be picked up alongside Sprint S5 planning.
