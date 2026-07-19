# Spike: Go backend ↔ MAUI desktop client (unchanged)

> **Status: ✅ core risk RETIRED.** This is a **time-boxed spike**, not production code.
> Its job was to de-risk the single riskiest unknown of a possible .NET→Go backend port:
> **can a Go server speak the SignalR protocol well enough that the existing .NET
> SignalR client connects and receives events, so the MAUI desktop stays unchanged?**
>
> This directory is isolated from the .NET build (`B2.NoMaui.slnf` / `B2.sln` do not
> reference it). Deleting it changes nothing in the product.

## Result (verified 2026-07-19, macOS)

The `.NET` probe (`clients/signalr-probe/`, using the **same**
`Microsoft.AspNetCore.SignalR.Client` the MAUI desktop uses) run against the Go server
exited **0 / PASS**:

```
[probe] logged in as Quản trị viên (role=Admin); token len=548
[probe] connecting to http://127.0.0.1:5000/hubs/events ...
[probe] hub state = Connected
[probe] <-- LedgerChanged declarationId=2c39ba52-...
[probe] PASS: SignalR protocol-compatible.
```

What this proves, end-to-end, with **no change to the desktop client's wiring**:
1. **RS256 login** — Go issues a token the flow accepts (`role`/`sub` claims, camelCase `LoginResponse`).
2. **SignalR handshake** — the .NET client's `negotiate` + WebSocket upgrade against the
   Go `philippseith/signalr` hub reaches **Connected**, carrying the bearer token.
3. **Event push + payload shape** — a server→client `LedgerChanged` broadcast is received
   and deserialized into `LedgerChangedPayload { declarationId }` correctly.

➡️ **Conclusion:** keeping the MAUI desktop unchanged while replacing the backend with Go
is **technically feasible** for the realtime channel — the part with no off-the-shelf
guarantee. This does **not** by itself justify a rewrite (see repo-root discussion: the
desktop staying .NET means a backend-only port leaves you maintaining two stacks). It
only removes the "will SignalR even work" unknown.

## How to run the proof yourself

```bash
# terminal 1 — Go server (needs Go 1.21+)
cd spikes/go-backend && go run ./cmd/server      # listens on 127.0.0.1:5000

# terminal 2 — .NET probe (needs .NET 8 SDK); prints PASS/FAIL, exit 0 = PASS
cd spikes/go-backend/clients/signalr-probe && dotnet run -c Release
```

Optional final confirmation on Windows: point the real MAUI desktop at
`http://127.0.0.1:5000/` (already its default `apiBase`) with the Go server running; the
connectivity indicator should turn 🟢 and the ledger should refresh on a push.

## Why this spike exists

The desktop (`src/B2.Desktop`) depends only on `B2.Shared.Contracts` and talks to the
server over **HTTP + SignalR**. It does not know what language the server is written in.
So a Go server keeps the MAUI client unchanged **iff** it reproduces the wire contract
exactly. The one part that is not trivially reproducible is SignalR (Go has no
first-class SignalR server — we use the community `philippseith/signalr` library).

## Pass / fail criterion

A small **.NET console client using the same `Microsoft.AspNetCore.SignalR.Client`
library the MAUI app uses** (`clients/signalr-probe/`) must, against the Go server:

1. `POST /api/auth/login` → receive a usable access token.
2. Open the SignalR hub at `/hubs/events` (carrying the token) → connection **Connected**.
3. Receive a server-pushed `LedgerChanged` event with the correct payload.

That library is cross-platform, so this runs on macOS/Linux — **no Windows/MAUI needed
to prove the protocol risk**. Running the real MAUI client on Windows is an optional
final confirmation (see `docs/run-on-windows.md`, TBD).

## Verified contract (from the .NET code)

Citations are `file:line` in the repo root.

### Auth — `POST /api/auth/login`
- Request JSON: `{ "username": string, "password": string }`
  (`src/B2.Shared.Contracts/Auth/LoginRequest.cs:3`, System.Text.Json default camelCase).
- Response JSON `LoginResponse` (`src/B2.Shared.Contracts/Auth/LoginResponse.cs:5`):
  `{ accessToken, refreshToken, accessTokenExpiresAt (DateTimeOffset), mustChangePassword (bool), role (UserRoleDto), userId (Guid), fullName }`.

### JWT (`src/B2.Infrastructure/Auth/JwtTokenService.cs`)
- Alg **RS256** (`SecurityAlgorithms.RsaSha256`).
- Claims: `sub` = userId (GUID), `role` = Role.ToString(), `must_change_password` = "true"/"false".
- `notBefore` = now, `expires` = now + **8h**. No issuer/audience set.
- **Spike simplification:** the Go server both issues and validates tokens with its
  own RSA keypair (generated at startup). The client only stores + echoes the token;
  only the server validates it, so a self-consistent key is sufficient for the spike.

### SignalR (client wiring: `src/B2.Desktop/Services/ConnectivityService.cs:146`)
- `HubConnectionBuilder().WithUrl(HubUri, o => o.AccessTokenProvider = () => accessToken).WithAutomaticReconnect().Build()`.
- Hub is **push-only** — server-side `EventsHub` is empty
  (`src/B2.Server.Api/Realtime/EventsHub.cs:5`), the client invokes **no** hub methods.
- Client subscribes to exactly two server→client events:
  - `LedgerChanged` → payload `{ "declarationId": Guid }`
    (`src/B2.Desktop/Services/LedgerChangedPayload.cs:7`).
  - `DailyHeaderOfficerChanged` → payload `{ "date": string, "officerName": string }`.
- Health ping: client treats reachable when `GET <health>` returns `{ status: "OK" }`
  (`ConnectivityService.cs:289`). Exact route + ledger endpoint + DB schema: **TBD**
  (being extracted).

## Layout (planned)

```
spikes/go-backend/
├── README.md                 # this file
├── go.mod
├── cmd/server/main.go        # wires everything
├── internal/auth/            # RS256 issue+validate, BCrypt verify
├── internal/hub/             # philippseith/signalr hub + push helpers
├── internal/ledger/          # read b2.db (same SQLite file) → ledger DTO
├── internal/httpapi/         # /health, /api/auth/login, ledger GET
└── clients/signalr-probe/    # .NET console using Microsoft.AspNetCore.SignalR.Client
```

## Explicitly OUT of scope for the spike
Quartz jobs, Litestream (keep as-is — language-agnostic), AES-256-GCM backup + DPAPI,
audit/authz middleware, Excel export, full endpoint surface, the 115 .NET tests.
These are real work for a full port but do **not** affect the SignalR risk this spike
answers.
