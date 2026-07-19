# ADR-0007: Evaluate replacing the .NET backend with Go (keep MAUI desktop)

**Status**: **Accepted** — keep .NET + MAUI + SQLite (stack unchanged, Constitution II
intact). Ratified by the owner 2026-07-19. Deployment concern addressed via
**self-contained single-file publish** (see `docs/deployment-offline.md`); a Go port is
**not** adopted. (Feasibility validated by spike; retained as evidence for a possible
future full-product move off .NET.)
**Date**: 2026-07-19
**Supersedes**: none
**Relates to**: ADR-0001 (modular monolith), ADR-0002 (SQLite), ADR-0003 (MAUI desktop),
ADR-0004 (JWT RS256)

## Context

The project owner (solo maintainer) raised two motivations for a stack change:

1. **Deployment**: concern that installing the **.NET runtime** on the gate PC could
   conflict with other software.
2. **Maintainability**: preference for a **more familiar stack to self-maintain** long
   term.

The proposal explored was: **replace the .NET backend (Windows Service hosting the
Minimal API + SignalR) with a Go backend, while keeping the existing .NET MAUI desktop
client unchanged, and keeping SQLite.** Constitution II locks the tech stack
(`specs/01-overview.md § 6`), so any deviation requires an ADR — hence this record.

Two premises were examined and found weak, plus one hard architectural fact:

- **Deployment premise is solvable without a rewrite.** .NET 8 supports **self-contained,
  single-file publish** (`SelfContained=true` + `PublishSingleFile=true`), which bundles
  the runtime into the executable — the gate PC installs nothing globally and cannot
  conflict with other apps. Delivered as a plain offline `.zip` + `install.ps1` xcopy
  deployment (`deploy/`, `src/B2.Installer` as a standalone CLI; Velopack removed for the
  internet-free single-site — see `docs/deployment-offline.md`). This yields the same
  "single .exe, no runtime install" property that Go's static binary offers; the only
  practical difference (binary size ~70–150 MB vs ~15 MB) is irrelevant for one gate.
- **Database is out of scope of the concern.** The owner's DB priority is *"lightweight,
  no server to install"* — which is **exactly** SQLite (ADR-0002). PostgreSQL would be
  *heavier* (a service to install + operate) and would also break Litestream (ADR-0005),
  which is SQLite-only. SQLite stays. (Note: Litestream is a standalone binary and is
  language-agnostic, so it survives a backend-language change too.)
- **Hard fact: the desktop is also .NET.** The MAUI client (all XAML pages, ViewModels,
  Vietnamese resources) is .NET. A **backend-only** port to Go does not remove .NET from
  the maintenance surface — it turns **one stack into two** (Go server + .NET desktop),
  which *increases* maintenance burden, contradicting motivation (2).

## Spike — the one genuine unknown, retired

The only part of a Go port with no off-the-shelf guarantee is **SignalR**: Go has no
first-class SignalR server, and the desktop uses `Microsoft.AspNetCore.SignalR.Client`
with a specific protocol (negotiate handshake, JSON hub protocol, token auth,
auto-reconnect). A time-boxed spike (`spikes/go-backend/`) was built to answer: *can a Go
server speak SignalR well enough that the unchanged .NET client connects and receives
events?*

- Go server: `philippseith/signalr` hub at `/hubs/events`, RS256 login mirroring
  `JwtTokenService`, health + push.
- A .NET probe (`spikes/go-backend/clients/signalr-probe/`) using the **same SignalR
  client library the MAUI desktop uses** — cross-platform, so the protocol risk is
  measurable on macOS without Windows/MAUI.

**Result (2026-07-19, macOS): PASS (exit 0).** The probe logged in (RS256), the hub
reached `Connected` carrying the bearer token, and a server→client `LedgerChanged` event
was received and deserialized into `LedgerChangedPayload { declarationId }` correctly —
with **no change to the desktop's connectivity wiring**.

➡️ **Feasibility of keeping MAUI unchanged over a Go backend is confirmed for the
realtime channel** (the riskiest part). The rest of the contract (REST DTOs camelCase,
`JsonStringEnumConverter`, JWT claims `sub`/`role`, `GET /api/admin/health`,
`GET /api/declarations`) is mechanically reproducible.

## Decision (recommended)

**Do not port the backend to Go at this time. Keep .NET + SQLite + MAUI (Constitution II
unchanged).** Instead, address the real motivations directly:

1. **Deployment robustness** — enable **self-contained single-file publish** for
   `B2.Server.Service` and `B2.Desktop`, so the gate PC needs no global .NET runtime
   (removes the conflict risk that motivated the proposal). *(Follow-up task, not part of
   this ADR's ratification.)*
2. **Maintainability** — invest in `docs/maintenance.md` (Vietnamese) + AI-assisted
   maintenance leveraging the existing `specs/`, Constitution, and 145 green tests, so the
   owner can maintain the .NET codebase confidently without deep C# expertise.

This ADR **records feasibility** so that a future decision to port (e.g. Phase 3, or if
the desktop is ever rebuilt as web/Electron and the whole product moves off .NET) can
start from a validated baseline rather than re-litigating the SignalR unknown.

## Consequences

**Positive**:

- Preserves a **released, tested `v1.0.0`** at the UAT finish line — no months-long
  rewrite, no loss of the 115-endpoint test safety net.
- Keeps a single, coherent stack for a solo maintainer (LTS, single vendor, static
  typing) — the genuinely lower-maintenance option once familiarity is gained.
- The spike (`spikes/go-backend/`) remains as executable evidence + a starting skeleton.

**Negative / accepted**:

- The owner's "familiar stack" preference is **not** satisfied — mitigated by the
  maintenance-doc + AI-assisted path, and by the fact a backend-only port would not have
  satisfied it either (desktop stays .NET).
- If the product later moves fully off .NET (backend **and** desktop), that is a separate,
  larger decision requiring its own ADR; this ADR does not pre-approve it.

## Alternatives considered

- **Backend-only Go port, keep MAUI** — *feasible* (spike-proven) but **not recommended**:
  yields a two-stack maintenance surface, discards the .NET test suite, and costs months
  to reach parity for a *less* featured result at the UAT finish line.
- **Full rewrite (Go backend + web/Electron desktop)** — coherent way to truly leave .NET,
  but effectively rebuilds the entire product; out of scope now, would need its own ADR.
- **Switch DB to PostgreSQL** — rejected: heavier, needs a server, breaks Litestream;
  contradicts the "no server to install" priority. SQLite already satisfies it (ADR-0002).
- **Status quo, no deployment change** — rejected in favor of adding self-contained
  publish, which resolves the deployment-conflict concern cheaply.

## References

- Spike: `spikes/go-backend/README.md` (result + how to reproduce)
- `.specify/memory/constitution.md` II (locked tech stack)
- `specs/01-overview.md § 6` tech-stack table
- ADR-0001 (modular monolith), ADR-0002 (SQLite), ADR-0003 (MAUI), ADR-0004 (JWT RS256),
  ADR-0005 (Litestream)
- Desktop realtime wiring: `src/B2.Desktop/Services/ConnectivityService.cs`
