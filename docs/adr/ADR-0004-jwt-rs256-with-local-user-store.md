# ADR-0004: JWT (RS256) with a local user store

**Status**: Accepted
**Date**: 2026-05-03
**Supersedes**: none

## Context

The system runs LAN-only with no internet (constraint C1) and has no Active
Directory available at the customs unit. Authentication must be self-contained,
operable without an external IdP, and protect every API endpoint server-side
(Constitution VII; FR-001 / FR-002). Three roles exist: `Admin`, `Operator`,
`Supervisor` (FR-027 role matrix).

## Decision

Adopt **JWT with RS256** signatures + a **local user store** in SQLite, with
**BCrypt.Net-Next** for password hashing:

- RSA 2048-bit private signing key generated at install, persisted to
  `C:\ProgramData\B2\keys\b2-jwt.pem`, **sealed with Windows DPAPI** (per-machine
  scope). Key never exists in plaintext on disk.
- JWT lifetime: **8 hours**. Refresh-token lifetime: **7 days**, rotation on every
  use, revocation on logout.
- BCrypt cost = 10. Password policy: ≥ 10 characters, 3 of 4 character classes.
- Default Admin (`username = "admin"`, password = `"Change-Me-Now-1!"`) ships with
  `MustChangePassword = true` (FR-017) — **superseded by ADR-0009**: the password is now
  generated per install by the installer and the shared seed hash is retired (migration 0004); the system stays in a degraded state until
  the password is rotated, surfaced on the Health page (F-18).
- Account lockout after 5 failed logins within 10 min, lasting 15 min (FR-018).
- Idle-session timeout: 30 min (FR-018).
- Authorization is policy-based (`AdminOnly`, `OperatorOrAbove`,
  `SupervisorOrAbove`); the policy matrix mirrors the wireframe sidebar exactly
  (FR-027 — single source of truth).

## Consequences

**Positive**:

- Self-contained: no IdP, no external network calls (C1).
- DPAPI-sealed key means an attacker who reads the disk doesn't get the
  signing key.
- Stateless verification — server doesn't keep a session table; only the
  refresh-token row exists in DB.
- Standard format → standard tooling (Refit + the standard JWT bearer
  middleware).

**Negative / accepted**:

- DPAPI keys cannot be re-used on a different machine. Acceptable: the install
  step regenerates a fresh keypair on the spec VM. A snapshot restore re-uses the
  *data*, not the signing key — clients must re-authenticate after restore (DoD
  documents this in the runbook).
- BCrypt cost 10 is a 2026 baseline; revisit in 2 years.

## Alternatives considered

- **Cookie session + sliding expiration** — rejected: server-state-y; harder to
  share between Desktop + future API integrations (Phase 3).
- **HS256 with shared secret** — rejected: a leaked secret invalidates every
  client; RS256's public/private split limits blast radius.
- **OIDC with an external IdP** — rejected: violates C1 (no internet) and C3 (no
  IT team to operate the IdP).
- **Windows Auth / Negotiate / Kerberos** — rejected: requires AD which the
  customs unit doesn't run.

## References

- `specs/01-overview.md § 6` tech-stack table
- `specs/02-architecture.md § 8` cross-cutting concerns
- `specs/04-implementation-plan.md § 10` security checklist
- `.specify/memory/constitution.md` Principle VII
