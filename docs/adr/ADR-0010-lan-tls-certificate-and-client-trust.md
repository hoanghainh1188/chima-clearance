# ADR-0010: LAN TLS — per-install certificate and client trust by the OS store

**Status**: **Proposed** — awaiting owner ratification with the PR that implements it.
**Date**: 2026-10-24
**Relates to**: #147 (cleartext HTTP over LAN), #176 (certificate SAN = localhost only),
`specs/01-overview.md` C1 ("self-signed TLS only"), `specs/02-architecture.md` § 2 host
configuration, ADR-0009 (initial Admin password, which assumed the LAN was trusted).

## Context

The specs always said the service listens on `https://0.0.0.0:5001` with a self-signed certificate.
The shipped build did not: `appsettings.json` bound `http://0.0.0.0:5000` (#114, so the exit-gate
PC could reach it), and the certificate `B2Clearance.exe install` issued named only `localhost`,
`127.0.0.1` and `::1`. Over the LAN, login passwords, JWT access/refresh tokens and every clearance
record travelled in cleartext; switching the URL to HTTPS alone would not have helped, because no
remote PC could validate a certificate that does not name the address it dials.

Constraints: LAN only, no internet, no PKI / AD CS at the gate (C1); offline xcopy install by a
non-specialist operator; no new dependency (Constitution II); the Desktop (`B2.Desktop`) cannot be
built or tested off Windows, so Desktop code changes are expensive to verify.

## Decision

1. **HTTPS only.** Kestrel listens on `https://0.0.0.0:5001` (`appsettings.json` `Urls`) with the
   certificate from `{dataDir}\keys\server.pfx` (`KestrelHttpsSetup`, TLS 1.2/1.3). Plain HTTP is
   **removed**, not kept for loopback by default: the entry-gate Desktop uses
   `https://127.0.0.1:5001/` like any other client. At startup the service **refuses** an
   `http://` URL on any non-loopback address, so a stray `ASPNETCORE_URLS` cannot reopen the hole;
   `http://127.0.0.1:…` is tolerated for troubleshooting only. A missing certificate outside
   Development is a startup error, not a silent fallback. No HTTP→HTTPS redirect: nothing listens
   on HTTP to redirect from.
2. **Certificate names the machine.** The installer issues a 10-year self-signed RSA-2048 /
   SHA-256 serverAuth leaf (BasicConstraints CA=false) whose SANs are `localhost`, `127.0.0.1`,
   `::1`, `Environment.MachineName`, the DNS host name (+ FQDN when domain-joined) and every
   unicast IPv4 on up, non-loopback, non-tunnel adapters (APIPA excluded). `install` and the new
   `renew-certificate` command re-issue **only** when the existing certificate is missing,
   unreadable, within 30 days of expiry, or no longer covers the current names/IPs — so routine
   upgrades keep the same certificate and clients keep working. A re-issue removes the old one
   from the local Root store.
3. **Client trust through the Windows certificate store, not pinning.** The installer exports the
   public part to `C:\ProgramData\B2\b2-server.cer` and prints its Thumbprint. The operator
   carries the `.cer` (public, safe to copy) to each exit-gate / supervisor PC on the same USB used
   for the payload; `install.ps1 -ClientOnly -ServerUrl … -ServerCertificate <file>` rejects a file
   with a private key or a non-B2 subject, removes older B2 certificates, imports it into
   LocalMachine\Root and prints the Thumbprint for the operator to compare. The Desktop keeps the
   framework's default validation for both Refit's `HttpClient` and the SignalR WebSocket.
4. **`ApiEndpointResolver` defaults to HTTPS.** A bare host / IP becomes `https://<host>:5001/`;
   the loopback default is `https://127.0.0.1:5001/`. Explicit `http://` URLs still parse (dev),
   but no production server answers them.

## Alternatives considered

- **Thumbprint pinning in `b2-client.json`** (custom `ServerCertificateCustomValidationCallback`
  on the HttpClient and SignalR handlers). Avoids touching the client's Root store, but every
  validation path (Refit, SignalR negotiate, WebSockets, any future client) must remember the
  callback, a mistake fails *open* if someone returns `true` to "fix" a bug, and none of it can be
  built or tested on the macOS dev loop. Distribution is the same USB step either way. Rejected.
- **Keep HTTP on loopback for the entry-gate Desktop.** Saves nothing — the certificate already
  names `127.0.0.1` and is trusted locally — and leaves two listeners and two code paths. Rejected
  (the startup guard still allows it as an explicit troubleshooting override).
- **Local CA + leaf certificates.** Lets a re-issue avoid touching clients, but the CA private key
  would sit on the same gate PC, and a CA in every client's Root store can mint certificates for
  any name. Overkill for one server per gate. Rejected.
- **Trust on first use (client fetches the cert over the LAN).** The first fetch is exactly the
  MITM window this issue is about. Rejected.

## Consequences

- Upgrading from ≤ v1.3.0 always re-issues (the old certificate lacks the LAN names): every
  exit-gate PC must be re-run with `-ServerCertificate`, and a `b2-client.json` that spells out
  `http://…:5000` must be changed to the bare IP. Called out in the CHANGELOG and operator docs.
- The certificate binds the LAN IPs present at issue time. A DHCP lease change breaks remote
  clients until `renew-certificate` + client re-import; the docs recommend a static IP for the
  entry-gate PC. Using the hostname in `serverUrl` survives IP changes where name resolution works.
- The firewall rule moves from TCP 5000 to TCP 5001 (Private profile only).
- No new package or runtime dependency.
