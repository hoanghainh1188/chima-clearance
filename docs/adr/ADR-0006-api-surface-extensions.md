# ADR-0006: API surface extensions for FR-022 / FR-025 / FR-026 / FR-027 / F-12 / F-17 + ArchUnitNET dependency

**Status**: Accepted
**Date**: 2026-05-03
**Supersedes**: none
**Supplements**: ADR-0001..0005, `02-architecture.md § 6`

## Context

The locked API surface in `specs/02-architecture.md § 6` predates the Specify /
Clarify rounds that added:

- **FR-022 / FR-023** (declaration cancellation, Admin only)
- **FR-025** (daily-header on-duty officer name, inline-editable, any role)
- **FR-026** (recovery-code-aware backup payload)
- **FR-027** (full role × endpoint authorisation matrix — single source of truth)
- **F-12** "Re-run on demand" Admin recompute action
- **F-17** simple-v1 smart-suggestion endpoint
- F-18 in-app log viewer (read-only)
- The Operator first-login forced password change (FR-017)
- Admin un-exit override (already implied by F-08 "Admin un-exit override"
  scenario)

Implementation needs server endpoints to satisfy these requirements; ignoring the
spec or breaking the API-stability promise both create churn. The
`docs/prompts.md:213` directive forbids inventing new endpoints **without** an
ADR — this ADR is that authorisation.

The analyse pass (`specs/001-b2-clearance/analyze-report.md`) also flagged
**ArchUnitNET** as an undeclared dependency (W2). It is added here for the same
single-ADR-per-deviation reason.

## Decision

### A — Extend the API surface

Add the following endpoints. All keep the path prefix `/api/`, JWT bearer auth,
ProblemDetails error shape, and Idempotency-Key behaviour of the existing
surface. None alters or removes an existing endpoint. The full list is reflected
in `specs/001-b2-clearance/contracts/openapi.yaml` with `x-source:
pending-adr-0006` (this ADR makes the marker accurate).

| Method | Path | Auth | Source | Notes |
| --- | --- | --- | --- | --- |
| POST | `/api/auth/change-password` | any (self) | F-01 / FR-017 | First-login forced change |
| PATCH | `/api/daily-headers/{date}/officer` | any | FR-025 | Today only (INV-11) |
| POST | `/api/daily-headers/{date}/recompute` | Admin | F-12 | "Re-run on demand" — addresses analyse pass W3 |
| POST | `/api/declarations/{id}/cancel` | Admin | FR-022 / FR-023 | Mandatory reason |
| POST | `/api/vehicles/{id}/un-exit` | Admin | F-08 admin override | Mandatory reason |
| GET | `/api/vehicles/suggest?plate=…&field=…` | any | F-17 simple v1 | Top-1, last 90 days |
| GET | `/api/admin/logs` | Admin | F-18 | Read-only in-memory sink |

Plus two **payload extensions** to existing locked endpoints:

| Endpoint | Extension |
| --- | --- |
| `POST /api/admin/restore` | Multipart form gains optional `recoveryCode` field; either `password` or `recoveryCode` decrypts the snapshot (FR-026) |
| `POST /api/admin/backup-now` | Optional `targets: string[]` body — defaults to all configured targets |

The full role × endpoint matrix is `spec.md` FR-027. **This ADR makes that matrix
the authoritative authorisation source** and supersedes the per-row `Auth` column
in `02-architecture.md § 6`.

### B — Add ArchUnitNET as a test-only dependency

Add `TngTech.ArchUnitNET.xUnit` to `tests/B2.Server.Api.Tests` to enforce
Constitution V layer rules
(`tests/B2.Server.Api.Tests/Architecture/LayerRulesTests.cs`):

- Domain MUST NOT depend on `Microsoft.Extensions.*`,
  `Microsoft.EntityFrameworkCore`, `Microsoft.AspNetCore`.
- Application MUST NOT depend on `Microsoft.EntityFrameworkCore` or
  `Microsoft.AspNetCore.Http.HttpContext`.

The library is **test-only** (no production binary impact); the alternative would
have been a hand-rolled reflection scan, which would re-implement ArchUnit at
worse quality. Approved as a Constitution II exception.

## Consequences

**Positive**:

- `contracts/openapi.yaml` is the new single source of truth for API surface.
- Future spec rounds add endpoints by amending FR-027 + this ADR; no duplication
  in `02-architecture.md`.
- Refit (`B2.Desktop`) gains seven typed methods + two field additions — small
  diff for the client.
- ArchUnit catches Constitution V leaks at test time, not at code review.

**Negative / accepted**:

- `02-architecture.md § 6` becomes the *original* surface, no longer the *current*
  surface — readers must consult `contracts/openapi.yaml` for the live list. Noted
  in the openapi.yaml header.
- ArchUnitNET adds ~300 KB to the test binaries. Negligible.

## Alternatives considered

- **Refuse the extensions** and push back on FR-022 / FR-025 / FR-026 — rejected:
  those FRs were stakeholder-approved during /speckit.clarify; reverting them
  would re-open closed clarifications.
- **Wedge the new behaviour into existing endpoints** (e.g., cancellation as a
  PUT on the declaration) — rejected: muddies the audit trail and breaks the
  principle of one endpoint = one command.
- **Hand-rolled architecture test in xUnit** — rejected: re-implements ArchUnit
  at worse quality; the standard library is widely used and well-documented.

## References

- `specs/001-b2-clearance/spec.md` FR-022, FR-025, FR-026, FR-027
- `specs/001-b2-clearance/research.md § 4 (cancellation), § 5 (recovery), § 6
  (officer name), § 7 (F-17), § 8 (this ADR's draft)`
- `specs/001-b2-clearance/contracts/openapi.yaml` — `x-source` markers
- `specs/001-b2-clearance/analyze-report.md` W2 (ArchUnitNET) and W3 (recompute
  endpoint) findings
- `tests/B2.Server.Api.Tests/Architecture/LayerRulesTests.cs`
