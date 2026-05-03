# MDM Pre-Seeded Node Config — Schema Specification

**Status:** Draft — Wave 4.5 deliverable of the Paper-Alignment Plan
**Schema version:** `v1` (JSON Schema Draft 2020-12)
**Date:** 2026-04-22
**Source paper:** [`_shared/product/local-node-architecture-paper.md`](../../_shared/product/local-node-architecture-paper.md) §16.1, §16.2
**Machine-readable schema:** [`mdm-config-schema.json`](mdm-config-schema.json)

> This document specifies the structure of the `node-config.json` file that an MDM operator drops onto a managed endpoint **before** the Sunfish local-node host first runs. Paths + platform conventions are in [`air-gap-deployment.md`](air-gap-deployment.md) §5.

---

## 1. Purpose

The pre-seeded config is the MDM's channel to configure a managed endpoint without touching the user. It covers:

- Team enrollment (`teamId`).
- Relay selection (`relayEndpoint`).
- Data paths for BYOD separation (`dataDirectory`; see [`byod-path-separation.md`](byod-path-separation.md)).
- Air-gap toggles (`updateServerUrl`).
- Attestation-issuer public key for role verification (`enterpriseAttestationIssuerPublicKey`).

Host configuration precedence (highest wins):

1. Explicit CLI flag / environment variable.
2. `appsettings.{Environment}.json` shipped with the app.
3. `appsettings.json` shipped with the app.
4. **Pre-seeded `node-config.json`** (this schema).
5. Platform defaults baked into `LocalNodeOptions.GetDefaultDataDirectory()`.

The host MUST NOT silently ignore a pre-seeded file that fails schema validation; it MUST log the validation error and refuse to start.

---

## 2. Schema Fields (v1)

| Field                                     | Type       | Req? | Description                                                                                               |
|-------------------------------------------|------------|:----:|-----------------------------------------------------------------------------------------------------------|
| `schemaVersion`                           | string     | yes  | Must equal `"v1"`. Used for forward-compat.                                                               |
| `teamId`                                  | string     | yes  | Stable team identifier; UUID recommended. Binds `LocalNodeOptions.TeamId`.                                |
| `relayEndpoint`                           | string URI | no   | Bridge URL for the team's relay. Absent = peer-to-peer only, no relay fallback.                           |
| `allowedBuckets`                          | string[]   | no   | Whitelist of subscription buckets the host will sync. Absent = default policy (from role attestation).    |
| `dataDirectory`                           | string     | no   | Override for `LocalNodeOptions.DataDirectory`. SHOULD point at the MDM-wipe-target path (see §16.3).      |
| `logLevel`                                | enum       | no   | One of `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`. Default: `Information`.           |
| `updateServerUrl`                         | string URI | no   | Internal update server base URL. Absent = no auto-update.                                                 |
| `enterpriseAttestationIssuerPublicKey`    | string     | yes  | Base64-encoded 32-byte Ed25519 public key of the org's attestation issuer.                                |

**Reserved for v2 (MUST NOT appear in v1 config):** `telemetryEndpoint`, `logEndpoint`, `proxy`, `rolloutRing`.

### 2.1 Example

```json
{
  "schemaVersion": "v1",
  "teamId": "7cf5b3de-8e4a-4b18-b2aa-1c0f2e3d5f90",
  "relayEndpoint": "https://bridge.internal.corp/relay",
  "allowedBuckets": ["corp.general", "eng.platform"],
  "dataDirectory": "C:\\ProgramData\\Sunfish\\TeamData",
  "logLevel": "Information",
  "updateServerUrl": "https://sunfish-mirror.internal.corp",
  "enterpriseAttestationIssuerPublicKey": "MCowBQYDK2VwAyEAl5pX2h3PnfKzF1N..."
}
```

### 2.2 Field Details

**`teamId`.** Immutable after first sync. Changing it post-enrollment invalidates cached role attestations and forces a re-onboard. MDM operators MUST coordinate team-id rotation with a full wipe (see [`byod-path-separation.md`](byod-path-separation.md)).

**`relayEndpoint`.** MUST be an `https://` URI. The host enforces TLS and refuses `http://` even for internal relays — use an internal CA instead of disabling TLS.

**`allowedBuckets`.** Policy *enforcement* lives in role attestation (Wave 1.6); this field is an *additional* client-side filter to suppress unnecessary subscription traffic. It is not a security boundary.

**`dataDirectory`.** If set, MUST resolve to a path under the platform's MDM-wipe-target directory (see [`byod-path-separation.md`](byod-path-separation.md) §2). The host logs a warning if the configured path is outside that tree, since it defeats the §16.3 separation.

**`enterpriseAttestationIssuerPublicKey`.** Ed25519, 32 raw bytes, base64-encoded (no PEM wrappers). The host uses this to verify role attestations it receives during handshake. **Bootstrap:** the MDM operator obtains the public key from the org's key-ceremony out-of-band (not part of this schema).

---

## 3. Versioning

- Schema evolves via `schemaVersion` bump.
- A v2 host MUST accept v1 configs with a deprecation warning until two minor versions after v2 ships.
- A v1 host MUST reject unknown fields in v2 configs — forward compat is opt-in per field.
- Removed fields get a `"deprecated": true` marker in the schema file for one major version before being removed.

---

## 4. Validation

The JSON Schema is at [`mdm-config-schema.json`](mdm-config-schema.json).

Operators can validate a config locally with any JSON Schema 2020-12 validator, e.g.:

```bash
# Using ajv-cli (requires Node.js)
npx ajv-cli validate \
    -s docs/specifications/mdm-config-schema.json \
    -d /etc/sunfish/node-config.json
```

```powershell
# Using Test-Json (PowerShell 7.4+)
Get-Content C:\ProgramData\Sunfish\node-config.json `
    | Test-Json -SchemaFile docs\specifications\mdm-config-schema.json
```

CI MAY enforce schema validation on any sample configs committed under `docs/specifications/samples/` (future wave).

---

## 5. Change Log

| Date       | Version | Change                                  |
|------------|---------|-----------------------------------------|
| 2026-04-22 | v1      | Initial schema.                         |
