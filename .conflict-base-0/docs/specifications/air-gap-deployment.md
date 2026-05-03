# Air-Gap Deployment Specification

**Status:** Draft — Wave 4.5 deliverable of the Paper-Alignment Plan
**Date:** 2026-04-22
**Source paper:** [`_shared/product/local-node-architecture-paper.md`](../../_shared/product/local-node-architecture-paper.md) §16.2
**Audience:** IT operators deploying Sunfish behind a corporate air-gap; CI/release engineers producing internal-mirror artifacts.

> This document specifies how Sunfish runs with **no internet access** — neither the local-node host, the sync daemon, nor the Anchor shell reach the public network. It does not motivate the architecture — see paper §16.2 for the *why*. It specifies the contracts an on-prem operator must stand up to replace each cloud assumption.

---

## 1. Goals, Non-Goals

**In scope:**

- Internal update server — REST contract for release artifact + SBOM delivery.
- Internal relay — using the Bridge accelerator in `Mode=Relay` as an on-prem replacement for a managed relay SaaS.
- MDM-pre-seeded configuration — file paths + override semantics.
- Logging — what stays on-device and what can be shipped to an internal collector.
- Outbound network denial — the set of endpoints an operator may safely block without breaking the product.

**Out of scope:**

- Disconnected CRDT semantics beyond what paper §9 already covers — air-gap is a *network* concern, not a *consistency* concern. Local-first means the product works offline by construction.
- MDM-vendor-specific artifact format (Intune XML, Jamf `.mobileconfig`, Kandji JSON) — those are per-customer deliverables; this spec defines the *data* they carry.
- Crypto-key distribution under air-gap — attestation-issuer key bootstrap is covered by Wave 1.6.

---

## 2. Deployment Posture

Three independently-configurable network postures, in increasing strictness:

| Posture              | Public internet | Internal update server | Internal relay | Telemetry endpoint |
|----------------------|:---------------:|:----------------------:|:--------------:|:------------------:|
| **Connected**        | Allowed         | Optional               | Optional       | Optional           |
| **Proxied**          | Through proxy   | Optional               | Optional       | Optional           |
| **Air-gap (strict)** | **Denied**      | **Required**           | **Required**   | Optional (on-prem) |

The operator expresses posture through [`mdm-config-schema.md`](mdm-config-schema.md) — specifically `updateServerUrl`, `relayEndpoint`, and (absent) `telemetryEndpoint`.

Air-gap mode is **not a separate build**. The same binary runs in all three postures; strict posture is produced by:

1. Setting `updateServerUrl` to the internal mirror.
2. Setting `relayEndpoint` to a self-hosted Bridge.
3. Omitting any outbound telemetry endpoint.
4. Operator-level firewall rules denying egress.

---

## 3. Internal Update Server

### 3.1 Contract

A plain HTTPS server the local-node host + Anchor shell poll for new releases. Minimum endpoints:

```
GET  /releases/latest.json
GET  /releases/{version}/artifacts/{name}
GET  /releases/{version}/Sunfish.cdx.json
GET  /releases/{version}/Sunfish.cdx.sha256
```

#### `GET /releases/latest.json`

Returns the metadata for the latest release the operator has approved for rollout.

```jsonc
{
  "version": "0.4.2",
  "published_at": "2026-04-22T18:30:00Z",
  "artifacts": [
    {
      "name": "Sunfish.msi",
      "platform": "windows",
      "arch": "x64",
      "url": "https://mirror.internal/releases/0.4.2/artifacts/Sunfish.msi",
      "sha256": "b3a1…"
    },
    {
      "name": "sunfish_0.4.2_amd64.deb",
      "platform": "linux",
      "arch": "x64",
      "url": "https://mirror.internal/releases/0.4.2/artifacts/sunfish_0.4.2_amd64.deb",
      "sha256": "07e4…"
    },
    {
      "name": "Sunfish-0.4.2.pkg",
      "platform": "macos",
      "arch": "arm64",
      "url": "https://mirror.internal/releases/0.4.2/artifacts/Sunfish-0.4.2.pkg",
      "sha256": "92cd…"
    }
  ],
  "sbom_url": "https://mirror.internal/releases/0.4.2/Sunfish.cdx.json",
  "sbom_sha256": "d41d…"
}
```

Clients MUST verify `sha256` on every artifact before installing. Clients SHOULD fetch and archive the SBOM alongside the artifact for audit.

#### `GET /releases/{version}/…`

Serves the artifact bytes. Authentication is deployment-specific (mTLS, bearer, IP allow-list); the client respects the deployment's proxy config but does not prescribe the auth model.

### 3.2 Mirror Population

CI produces the release artifacts + SBOM. Operators populate the mirror by:

1. Pulling from the public release feed (in a jump-host with outbound access), or
2. Receiving a signed tarball via sneakernet, or
3. Building from source in the air-gap using the same `tooling/sbom/generate-sbom.sh` contract.

The mirror itself is **not part of Sunfish** — it is operator-owned infrastructure. A reference implementation (static-file behind nginx) is the expected common case.

### 3.3 Auto-Update Behavior (not implemented yet)

Paper-aligned design intent (out of scope for Wave 4.5 implementation, documented here for future-wave contract stability):

- Poll interval: configurable, default 24h.
- Staged rollout: the host reports its version via Bridge when available; the operator can gate by percentage.
- Download-and-stage: the new artifact is fetched and verified against `sha256` before swap.
- On-swap: the service is cycled via the platform service-manager (SCM on Windows, systemd on Linux, launchd on macOS).

---

## 4. Internal Relay (Bridge in `Mode=Relay`)

Paper [§17.2](../../_shared/product/local-node-architecture-paper.md) positions the Bridge accelerator as the relay/offline fallback layer. Under strict air-gap, a team deploys one or more Bridge instances on-prem:

- Each Bridge is configured with `Mode=Relay` and an on-prem-issued cert chain.
- Local-node hosts point `relayEndpoint` at the Bridge's internal URL.
- Bridge instances peer with each other using the same sync-daemon protocol ([`sync-daemon-protocol.md`](sync-daemon-protocol.md)) — no central coordinator.

A team's relay topology may be as small as one VM or as large as a per-region fleet. The sync daemon's symmetric handshake makes both equivalent from the client's perspective.

**Deferred (Wave 4.2):** Bridge-reposition ADR 0026 may rename or refactor the Bridge accelerator. If so, this section is the authoritative binding point for air-gap; update in lock-step.

---

## 5. MDM-Pre-seeded Configuration

Operators drop a `node-config.json` at a platform-conventional location **before** the first run of the host. On start-up, the host reads this file first; values in it override defaults but are themselves overridden by explicit CLI flags or environment variables (standard .NET configuration precedence).

| Platform | Pre-seed path                                          |
|----------|--------------------------------------------------------|
| Windows  | `%PROGRAMDATA%\Sunfish\node-config.json`               |
| Linux    | `/etc/sunfish/node-config.json`                        |
| macOS    | `/Library/Application Support/Sunfish/node-config.json` |

Schema: [`mdm-config-schema.md`](mdm-config-schema.md) + [`mdm-config-schema.json`](mdm-config-schema.json).

The host MUST tolerate an absent config file (fresh single-user install). The host MUST reject a config file that fails schema validation and log the validation error without starting the service.

---

## 6. Logging

Default posture: **on-device only.** The host writes structured logs to:

| Platform | Default log path                                        |
|----------|---------------------------------------------------------|
| Windows  | `%LOCALAPPDATA%\Sunfish\Logs\` + Windows Event Log      |
| Linux    | `journald` via `systemd-notify` integration             |
| macOS    | `/var/log/sunfish/` + unified logging (`os_log`)        |

The schema field `logEndpoint` (reserved, not in v1 schema) will allow operators to ship logs to an internal collector (Fluent Bit, Loki, Splunk HEC). When set, the host additionally forwards via a pluggable sink. Until that wave lands, logs stay on-device.

---

## 7. Outbound Network Denial List

Under strict air-gap, operators can block the following without breaking the product:

| Destination                      | Used by      | Safe to block under air-gap? |
|----------------------------------|--------------|:----------------------------:|
| Public package feeds (nuget.org) | dev build    | Yes (only needed at build time) |
| Public update feed               | auto-update  | Yes (use internal mirror)       |
| Managed relay SaaS (when public) | sync relay   | Yes (use internal Bridge)       |
| Telemetry endpoint               | diagnostics  | Yes (telemetry is opt-in)       |
| OCSP / CRL responders            | TLS validation | **No** — breaks cert chains the host uses internally; configure an internal OCSP responder. |

---

## 8. Verification Checklist

Operator self-test after deployment:

1. Disconnect the host from the public internet.
2. Confirm the service is running (`systemctl status`, `launchctl print`, `sc query`).
3. Point a second host at the same Bridge relay and confirm sync between them.
4. Simulate a release rollout: publish a higher-version artifact on the internal mirror; verify the host stages it (when auto-update lands).
5. Verify the SBOM for the installed version was fetched + archived locally.

---

## 9. Change Log

| Date       | Change                                      |
|------------|---------------------------------------------|
| 2026-04-22 | Initial draft (Wave 4.5).                   |
