# ADR-0001: Modular Monolith over Microservices

**Status**: Accepted
**Date**: 2026-05-03
**Supersedes**: none

## Context

Customs Gate B2 must replace a shared-Excel "Sổ B2" workflow that today runs across
two gate PCs (entry + exit) and ~3 supervisor PCs over LAN, with **no internet
connectivity** (constraint C1), **no dedicated IT operations team** (constraint C3),
and ~5 concurrent users (constraint C7). The architecture must support 24/7 operation
on i5-3570 / 4 GB hardware (constraints C2, C6) and remain operable by non-IT staff.

The candidate patterns surveyed:

| Option | Verdict |
| --- | --- |
| **A. Modular Monolith C/S** + SQLite + .NET MAUI | Chosen |
| B. Microservices | Rejected |
| C. Excel + macros | Rejected |
| D. P2P sync (CouchDB / PouchDB) | Rejected |
| E. Pure web app in browser | Rejected |

## Decision

Adopt **Option A — Modular Monolith client/server over LAN**:

- Single `B2.Server.Service` (Windows Service) on the entry-gate PC hosts an
  ASP.NET Core 8 Minimal API + SignalR hub in-process.
- Up to five `B2.Desktop` (.NET MAUI 8 Windows) clients consume the API over the
  LAN at `https://entry-pc:5001`.
- Modules are enforced as **eight production projects** with strict dependency
  layering (see ADR-0003 for the Desktop project; Constitution V for the rules).
- Persistence is local SQLite with WAL mode (see ADR-0002), continuously replicated
  by Litestream (see ADR-0005).

## Consequences

**Positive**:

- Single installer ships everything — installable by non-IT staff per C3.
- Low memory / CPU footprint feasible on the spec hardware (C2).
- Modular project layout preserves the option to extract services in a future
  Phase 3 without rewriting the core (Constitution V protects the seams).
- Operational simplicity: one process to start / stop / monitor.

**Negative / accepted**:

- Single point of failure on the entry-gate PC. Mitigated by Litestream
  replication to USB + LAN folder (ADR-0005) and a runbook procedure to spin a
  replica up on a supervisor PC within 30 min (`02-architecture.md § 10`).
- No horizontal scale — fine at ~5 concurrent users (C7).

## Alternatives considered

- **B. Microservices** — rejected: overkill for 5 users; multiple services need IT
  capacity that the unit doesn't have (C3); inter-service network calls add latency
  on a flaky LAN.
- **C. Excel + macros** — rejected: doesn't solve the root concurrency / data-quality
  problem (the entire spec exists *because* this approach failed).
- **D. P2P sync (CouchDB / PouchDB)** — rejected: steep learning curve; conflict
  resolution is a UX nightmare for non-IT operators; offline-first guarantees not
  required (C8 says we accept the system stops when the server is down).
- **E. Pure web app in browser** — rejected: poor desktop UX; users prefer native
  apps (C4); browser-tab metaphor confuses operators on a 24/7 gate.

## References

- `specs/01-overview.md § 4` (architecture rationale)
- `specs/02-architecture.md §§ 1–2` (logical + process view)
- `.specify/memory/constitution.md` Principle I (constraint-driven)
