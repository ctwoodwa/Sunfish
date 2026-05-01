# ADR 0007 — Bundle Manifest Schema

**Status:** Accepted
**Date:** 2026-04-19
**Resolves:** Missing formal shape for business-case bundle manifests; required before Bridge can provision tenants or before any second bundle is composable.

---

## Context

ADR 0005 established that bundles are *configuration*, not code — a bundle is a named entitlement composition that tells the shell which reusable modules to activate, which feature defaults to apply, and which provider integrations are required for a given business case. ADR 0006 established that Bridge is a generic SaaS shell that hosts bundles rather than being a bundle itself.

To make bundles operational, Sunfish needs a stable, versioned shape for bundle manifests that:

1. Can be authored as JSON (shipped as seed data), as a C# record (loaded into services), and as CLI template output (P6).
2. Composes reusable modules by string key, without hardcoding module types or references.
3. Declares feature defaults and edition → module mappings so tenants can subscribe to a tier and inherit a consistent module set.
4. Declares which deployment modes (lite, self-hosted, hosted SaaS) the bundle supports.
5. Declares provider requirements (billing, payments, feature flags, channel managers, …) without locking to any vendor.
6. Stays backwards-compatible across bundle version bumps — consumers must be able to upgrade a tenant from v0.1 to v0.2 of a bundle.

The bundle manifest is the control-plane contract for every downstream feature: tenant provisioning, edition enforcement, feature evaluation, template activation, and integration setup.

---

## Decision

Adopt the following record shapes in `Sunfish.Foundation.Catalog.Bundles`, deserializable from JSON via `System.Text.Json` with default camelCase property names. All collections default to empty so manifests can grow additively.

### `BusinessCaseBundleManifest`

| Field | Type | Purpose |
|---|---|---|
| `key` | `string` (required) | Stable bundle identifier, reverse-DNS style (e.g. `sunfish.bundles.property-management`). |
| `name` | `string` (required) | Human-readable name. |
| `version` | `string` (required) | Semver. Bundle upgrades move tenants forward safely when minor/patch; major requires migration. |
| `description` | `string?` | Longer prose. |
| `category` | `BundleCategory` | Coarse classification (Operations, Diligence, Finance, Platform). |
| `status` | `BundleStatus` | Lifecycle (Draft, Preview, GA, Deprecated). |
| `maturity` | `string` | Engineering readiness note ("Scaffold", "Beta", etc.). Free-form by design. |
| `requiredModules` | `string[]` | Module keys that must be installed. |
| `optionalModules` | `string[]` | Module keys that may be installed per edition. |
| `featureDefaults` | `map<string,string>` | Default feature values applied at tenant provisioning. |
| `editionMappings` | `map<string, string[]>` | Edition key → module keys activated (e.g. "lite" → `[blocks.leases, blocks.maintenance]`). |
| `deploymentModesSupported` | `DeploymentMode[]` | Which of Lite / SelfHosted / HostedSaaS this bundle supports. |
| `providerRequirements` | `ProviderRequirement[]` | Provider categories this bundle needs (optional or required). |
| `integrationProfiles` | `string[]` | Named provider-configuration profiles (e.g. `payment-gateway-default`). |
| `seedWorkspaces` | `string[]` | Pre-built workspaces/dashboards to seed for new tenants. |
| `personas` | `string[]` | Named personas the bundle ships roles and navigation for. |
| `dataOwnership` | `string?` | Free-form policy statement (ownership, export, residency). |
| `complianceNotes` | `string?` | Free-form compliance framing. |

### `ProviderRequirement`

| Field | Type | Purpose |
|---|---|---|
| `category` | `ProviderCategory` | Payments, BankingFeed, FeatureFlags, ChannelManager, etc. |
| `required` | `bool` | If false, feature degrades gracefully when absent. |
| `purpose` | `string?` | One-line explanation. |

### `ModuleManifest` (minimal, authored alongside each `blocks-*` module)

| Field | Type |
|---|---|
| `key` | `string` (required) |
| `name` | `string` (required) |
| `version` | `string` (required) |
| `description` | `string?` |
| `capabilities` | `string[]` |

Module manifests are a forward primitive: the first consumer is bundle-reference validation (does the bundle reference any module key that isn't installed?), which can be implemented once real modules start shipping their own manifests in P2.

### Enums

- `BundleCategory`: `Operations`, `Diligence`, `Finance`, `Platform`.
- `BundleStatus`: `Draft`, `Preview`, `GA`, `Deprecated`.
- `DeploymentMode`: `Lite`, `SelfHosted`, `HostedSaaS`.
- `ProviderCategory`: `Billing`, `Payments`, `BankingFeed`, `FeatureFlags`, `ChannelManager`, `Messaging`, `Storage`, `IdentityProvider`, `Other`.

All enums serialize as strings in JSON (via `JsonStringEnumConverter`).

### Runtime: `IBundleCatalog`

```csharp
public interface IBundleCatalog
{
    void Register(BusinessCaseBundleManifest manifest);
    IReadOnlyList<BusinessCaseBundleManifest> GetBundles();
    bool TryGet(string key, out BusinessCaseBundleManifest? manifest);
}
```

Bridge's provisioning service consumes `IBundleCatalog` to: look up a selected bundle by key, verify required modules are installed, evaluate edition mappings, apply feature defaults, and activate provider integrations.

### Shipping manifests

Reference bundle manifests ship as **embedded JSON resources inside `Sunfish.Foundation.Catalog`**, under `Manifests/Bundles/*.bundle.json` with clean `LogicalName`s. A small `BundleManifestLoader` reads embedded or arbitrary JSON into `BusinessCaseBundleManifest`. This was left as an open decision in ADR 0005; this ADR resolves it to Option (1) — embedded — for the first several manifests. If the catalog grows or external teams ship their own bundles, a sibling `catalog-seed` package becomes the follow-up split (no code migration required).

### Versioning policy

- Manifest `version` is semver.
- **Patch** bumps may change prose, `featureDefaults`, `personas`, `seedWorkspaces`, `complianceNotes`, `maturity`. No tenant action required.
- **Minor** bumps may add `optionalModules`, add entries to `editionMappings`, add `providerRequirements` where `required: false`. Tenants auto-upgrade; newly-optional modules are inactive until the tenant opts in.
- **Major** bumps may alter `requiredModules`, remove edition mappings, flip `providerRequirements.required` to true, or change `deploymentModesSupported`. Tenants require an explicit migration, surfaced in Bridge admin.

This is the same contract that bundle-store UX (P3) will communicate to tenant admins.

---

## Consequences

### Positive

- Bundle composition is declarative and diffable across versions.
- Property Management (and soon Asset, Project, Facility, Acquisition) become coherent, activatable artifacts.
- External bundles become feasible — the manifest is portable JSON; nothing forces a bundle to live in the Sunfish repo.
- Bridge's provisioning logic shrinks to "read catalog, apply manifest."
- Feature management (ADR 0009, P1) consumes bundle manifests directly — no ad-hoc feature lists.

### Negative

- Manifest evolution requires versioning discipline. Without it, tenant upgrades break.
- `editionMappings` duplicates some information from `optionalModules` intentionally for clarity at the expense of terseness.
- Embedding manifests in the library grows the assembly. First five bundles are a few kilobytes total; not material for a decade.

### Follow-ups

1. **Bundle manifest JSON Schema** — a meta-schema for authoring editor validation, to ship once three or more bundles exist.
2. **Module manifest runtime** — `IModuleCatalog` + registration from module assemblies, triggered when P2 modules start carrying their own manifests.
3. **Bridge `BundleProvisioningService`** — consumes `IBundleCatalog` + `IFeatureEvaluator` to realize a tenant's bundle selection.
4. **Catalog-seed split** — only if/when manifests grow or external publishers appear.

---

## References

- ADR 0005 — Type-Customization Model (establishes bundles as configuration, manifests as data).
- ADR 0006 — Bridge Is a Generic SaaS Shell (establishes the shell that consumes the catalog).
- ABP Framework module/bundle patterns — inspiration for module manifest granularity.
- Salesforce managed packages, Microsoft Dataverse Solutions — prior-art for versioned bundle artifacts with edition-like enforcement.
- `Sunfish.Foundation.Catalog` package — home of the new types and embedded seed.

---

## Amendments (post-acceptance, 2026-05-01)

### A1 (REQUIRED) — `BusinessCaseBundleManifest.Requirements: MinimumSpec?` field

**Driver:** ADR 0063 §"Sibling amendment dependencies named" — ADR 0063 (Mission Space Requirements; install-UX layer; landed post-A1 via PR #411) introduced `MinimumSpec` schema that bundles need a place to declare. The substrate ships in `Sunfish.Foundation.MissionSpace`; the bundle-manifest tier needs a field on `BusinessCaseBundleManifest` to carry per-bundle declarations. Without this field, ADR 0063 Phase 2 wiring cannot proceed.

**Pipeline variant:** `sunfish-api-change` (introduces new field on a public schema; non-breaking due to nullability).

**Companion intake:** [`icm/00_intake/output/2026-04-30_bundle-manifest-requirements-field-intake.md`](../../icm/00_intake/output/2026-04-30_bundle-manifest-requirements-field-intake.md) (PR #412; merged).

#### A1.1 — Schema field addition

`BusinessCaseBundleManifest` gains one new field:

| Field | Type | Purpose |
|---|---|---|
| `requirements` | `MinimumSpec?` | Optional per-bundle minimum-spec declaration (per ADR 0063). When `null`, the bundle declares no requirements (effectively "runs anywhere ADR 0044/0048 supports"). When non-null, install-time UX renders the spec against the user's `MissionEnvelope` per ADR 0063's Steam-style System Requirements page. |

The field is **optional (nullable)**; existing bundles default to `null` (no install-time gating). Backward-compat preserved.

`MinimumSpec` is the type defined by ADR 0063 in `Sunfish.Foundation.MissionSpace` (10 per-dimension spec records + `SpecPolicy` enum + `PerPlatformSpec` overrides; per ADR 0063 §"Initial contract surface" + post-A1 corrections per ADR 0063-A1.1–A1.14).

#### A1.2 — Canonical-JSON encoding

Field encodes via `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` per ADR 0007's existing camelCase/System.Text.Json conventions. JSON field name: `"requirements"`. When `null`, the field is omitted from JSON output (System.Text.Json `JsonIgnoreCondition.WhenWritingNull` semantics).

Sample post-A1 manifest fragment:

```json
{
  "key": "sunfish.bundles.property-management",
  "name": "Property Management",
  "version": "1.2.0",
  "requiredModules": ["blocks.leases", "blocks.maintenance"],
  "requirements": {
    "hardware": { "minMemoryBytes": 17179869184, "minCpuCores": 8 },
    "user": { "requiredAuthMethods": ["biometric"] },
    "policy": "required"
  }
}
```

(Bundle declaring 16 GB RAM + 8-core CPU + biometric-auth as Required — install blocks if user's device doesn't meet spec; force-install requires operator override per ADR 0062-A1.9 + ADR 0063-A1.11.)

#### A1.3 — Validation

ADR 0007's existing schema-validation surface gains structural validation of the `requirements` field:

- Field is optional — absence is valid (treated as `null`).
- When present, MUST be a valid `MinimumSpec` instance. The `MinimumSpec` resolver (per ADR 0063) is responsible for value-validation; the manifest validator only checks presence + type-shape.
- `requirements.policy` (a `SpecPolicy` enum) MUST be one of `Required` / `Recommended` / `Informational` per ADR 0063.

No new exceptions. Existing manifest-validation failures continue to surface via the existing error path (per ADR 0007's `IBundleCatalog` contract).

#### A1.4 — Backward + forward compatibility

Existing manifests default to `null` for the new field. No behavior change for bundles that don't opt in.

Phase 1 substrate of ADR 0063 (post-A1) ships the resolver + renderer; Phase 2 wires bundle manifests' `requirements` into install-time evaluation. ADR 0007-A1 ships the field; the wiring is a separate work product per ADR 0063's Phase 2 plan.

Per ADR 0028-A6 council F12 verification: `CanonicalJson.Serialize` unknown-key tolerance holds — older deserializers ignore the new field silently. Forward-compat preserved.

#### A1.5 — Acceptance criteria

For an A1-conformant `BusinessCaseBundleManifest` implementation:

- [ ] `Requirements: MinimumSpec?` field added to the record/class
- [ ] `null` is the default value
- [ ] Round-trips via `CanonicalJson.Serialize` (camelCase per ADR 0007 convention; field name `"requirements"`)
- [ ] Validation: presence-optional + type-shape check; per-value validation delegated to ADR 0063's `IMinimumSpecResolver`
- [ ] Backward-compat test: pre-A1 manifest (no `requirements` field) deserializes correctly with `requirements == null`
- [ ] Forward-compat test: post-A1 manifest (with `requirements`) serializes; older deserializer ignores the field; round-trip preserves the field via CanonicalJson unknown-key tolerance
- [ ] apps/docs entry for ADR 0007 walkthrough page gains a §"Requirements field" section linking to ADR 0063

#### A1.6 — Cited-symbol verification (per cohort discipline)

**Existing on `origin/main`** (verified 2026-05-01):

- ADR 0063 (Mission Space Requirements; post-A1) — landed via PR #411; provides `MinimumSpec` schema
- ADR 0062 (Mission Space Negotiation Protocol; post-A1) — landed via PR #406; provides `MissionEnvelope`
- `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` — encoding contract (per ADR 0028-A4 retraction; verified existing)
- ADR 0044 + ADR 0048 — Phase 1 + Phase 2 platform-scope precedents
- `Sunfish.Foundation.Catalog.Bundles` namespace — verified existing per ADR 0007 §"Decision"

**Introduced by A1** (not on `origin/main`; ship in implementation hand-off):

- `BusinessCaseBundleManifest.Requirements` field
- Updated apps/docs §"Requirements field" subsection

**Cohort lesson reminder (per ADR 0028-A10 + ADR 0063-A1.15):** §A0 self-audit pattern is necessary but NOT sufficient. Implementation should structurally verify each cited Sunfish.* symbol exists (read actual cited file's schema; don't grep alone) before declaring AP-21 clean.

#### A1.7 — Implementation hand-off

Stage 06 hand-off lands as a small workstream (~3–5h per parent intake at PR #412). Recommend writing the hand-off file at `icm/_state/handoffs/foundation-catalog-requirements-field-stage06-handoff.md` when COB capacity opens (currently shipping W#30 P7; W#35 Foundation.Migration queued; W#23 iOS Field-Capture queued — this amendment's hand-off slots after those).

#### A1.8 — Cohort discipline

Per `feedback_decision_discipline.md` cohort batting average (16-of-16 substrate ADR amendments needing council fixes; structural-citation failure rate 11-of-16 ~69% XO-authored — A9's parent-propagation retracted via A10; §A0 catch rate 0-of-5):

- This is a small mechanical schema-extension amendment matching ADR 0036-A1's type-exposure precedent. **Pre-merge council MAY be waived** per Decision Discipline Rule 3 (mechanical addition; matches ADR 0036-A1 + ADR 0028-A3 + A4 + A10 council-waiver precedents).
- Cited-symbol verification at draft time:
  - `MinimumSpec` per ADR 0063 — verified Accepted + structural read confirms shape
  - `MissionEnvelope` per ADR 0062 — verified Accepted
  - `CanonicalJson.Serialize` — verified existing per ADR 0028-A4 retraction
  - ADR 0044 / 0048 — verified Accepted
- Post-merge **standing rung-6 spot-check** within 24h per the cohort discipline.

The pre-merge council waiver on this amendment is intentional: **field-addition** (one optional nullable field; no new contract; backward-compat preserved). Substrate-tier introductions (W#33 §7.2 cohort) pass through pre-merge council; mechanical schema-extensions with established precedent auto-accept per Decision Discipline Rule 3.
