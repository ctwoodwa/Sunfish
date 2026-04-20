# Compatibility Policy

**Status:** Accepted (pre-1.0 rules in effect)
**Last reviewed:** 2026-04-19
**Governs:** Version numbering, deprecation, and breaking-change commitments for every package, public API, bundle, template, and accelerator release.
**Companion docs:** [architecture-principles.md](architecture-principles.md), [`docs/adrs/0011-bundle-versioning-upgrade-policy.md`](../../docs/adrs/0011-bundle-versioning-upgrade-policy.md).

Sunfish is pre-1.0. This document records what consumers can rely on today and what changes when 1.0 arrives. The rules are intentionally different for the two phases — pre-1.0 is optimized for design velocity; post-1.0 is optimized for consumer stability.

## Scope

Four surfaces have compatibility commitments:

| Surface | Versioned by | Governed by |
|---|---|---|
| **Packages** (`Sunfish.Foundation.*`, `Sunfish.Blocks.*`, `Sunfish.UIAdapters.*`, …) | Semver 2.0 per package; `Version` property in csproj (currently uniform `0.1.0`) | This document |
| **Public APIs** inside packages | Follow the package's semver | This document |
| **Bundle manifests** | Semver 2.0 per manifest; `version` field in JSON | [ADR 0011](../../docs/adrs/0011-bundle-versioning-upgrade-policy.md) |
| **Templates** (forms, checklists, reports, …) | Semver 2.0 per template; `version` field | ADR 0005 + ADR 0011 |

## Phases

### Pre-1.0 (current)

**Rule: APIs may change without major-version bump, but every breaking change must be flagged in release notes.**

Every package currently ships at `0.x.y`. Under semver, `0.y.z` releases carry no compatibility guarantees — minor bumps are allowed to break. Sunfish uses this as intended:

- **Breaking API changes** are allowed with a `0.x → 0.(x+1).0` bump and a release-notes entry.
- **Non-breaking additions** are `0.x.y → 0.x.(y+1)`.
- **Deprecation** is a courtesy, not a requirement. When practical, the old API stays for one minor version marked `[Obsolete]`, then the next minor removes it.
- **ADRs** gate structurally significant changes regardless of semver — a major refactor of Foundation.Catalog needs an ADR even if the version only bumps from `0.2.0` to `0.3.0`.

**What consumers should assume pre-1.0:** pin the exact version. Upgrade deliberately. Read release notes.

### Post-1.0 (future)

**Rule: strict semver with documented migration paths on majors.**

At 1.0, each package commits to:

- **Patch (`1.2.0 → 1.2.1`):** bug fixes only. No API surface change. No behavior change beyond the bug.
- **Minor (`1.2.0 → 1.3.0`):** additive API changes only. Existing consumers compile and run unchanged. New types, new overloads, new optional parameters (via default values), new enum values at the end are OK. Enum values inserted in the middle or renamed are not.
- **Major (`1.x.x → 2.0.0`):** breaking changes allowed. Requires:
  - A dedicated ADR naming the change and the reason.
  - A migration guide document (`docs/migrations/<package>-<from>-to-<to>.md`).
  - Announcement in release notes ≥30 days before the major ships.

1.0 for a package is declared when it's been structurally stable in-repo for ≥6 months and has ≥1 external consumer. Declaring 1.0 is an ADR-gated decision per package — not a single repo-wide event.

## Package scope

### Package identity is stable

A package's `PackageId` (`Sunfish.Foundation.Catalog`) and its namespace are contracts. Renaming them is a major version bump *and* requires a deprecated alias package for at least one major cycle post-1.0 — pre-1.0 a rename is allowed in one hop but must be announced.

### Tier boundaries are contracts

Moving a type across tiers (e.g., `ProviderCategory` from `Foundation.Catalog.Bundles` to `Foundation.Integrations`) is a breaking change for consumers of either package. Pre-1.0 we accept the churn if the new location is architecturally correct; post-1.0 such moves ship with a type-forward from the old location that lives for one major cycle.

### Package splits and merges

Splitting one package into two (`Sunfish.Foundation` → `Sunfish.Foundation.Extensibility` + `Sunfish.Foundation.MultiTenancy`) is a major change. Post-1.0 ships the new packages alongside type-forwards; pre-1.0 can ship a clean break with announcement.

### Public API surface

A member is "public API" if it's:

- `public` or `protected` in a non-sealed class
- on a type with `IsPackable=true` (part of the NuGet surface)

Internal types and `internal`-visible test surfaces are not public API. They can change freely at any version.

## Deprecation mechanics

### Marking deprecated

Use `[Obsolete("Use NewThing instead. See <doc-link>.")]` on deprecated members. For types slated for removal, also add a note at the top of the XML doc:

```csharp
/// <summary>
/// (Deprecated 2026-06) Use <see cref="NewThing"/> instead. Scheduled for removal in 1.0.
/// </summary>
[Obsolete("Use NewThing instead. See docs/migrations/foundation-catalog-0.3-to-0.4.md.")]
public sealed class OldThing { … }
```

### Deprecation windows

- **Pre-1.0:** deprecation is best-effort. Expect one minor of `[Obsolete]` marking before removal when practical.
- **Post-1.0:** obsolete markers live for at least one major version. Example: a type marked obsolete in 2.3.0 ships un-obsoleted in 2.x and is removable only starting 3.0.0.

### Removal requires announcement

A deprecated API cannot be removed in a patch release. Pre-1.0 it can be removed in a minor. Post-1.0 it requires a major bump with release-notes callout and migration guide.

## Bundles (ADR 0011)

[ADR 0011](../../docs/adrs/0011-bundle-versioning-upgrade-policy.md) governs bundle manifests. Key commitments:

- Manifest `key` never changes across versions. A bundle evolves; it does not fork identity. New identities get new keys.
- Major-version bundle upgrades are **explicit opt-in per tenant** and produce a migration report.
- Deprecated bundles stay in Deprecated status for ≥180 days before Archival.
- Rollback is permitted for patch/minor within 7 days; majors are forward-only.

## Templates (ADR 0005 + ADR 0011)

Templates follow the bundle-manifest semver rules. Additionally:

- Tenant overlays (ADR 0005) survive base-template upgrades via three-way merge.
- Removing a required field from a base template is a major bump.
- Adding an optional field is a minor bump and does not break existing overlays.

## Deployment-mode commitments

Changes to `deploymentModesSupported` on a bundle are semver-significant:

- **Adding** a mode is minor (new capability).
- **Removing** a mode is major (breaks tenants running that mode).

If a capability inside a bundle becomes mode-incompatible (e.g., a new feature requires hosted SaaS and breaks local-first), that's a major bump on the bundle unless the feature is gated off in the incompatible mode by default.

## Provider adapters

Provider adapter packages (`Sunfish.Providers.*`) follow their own semver. Breaking changes to a provider's internal translation logic are not breaking at the Sunfish contract level — the domain module continues to see `Foundation.Integrations` contracts. But a provider adapter bumping major means consumer hosts may need to update configuration.

## Bridge and accelerators

Bridge is an application, not a library. Its compatibility commitments are different:

- **Bridge does not publish a public API surface.** It's a deployable host. Internal refactors are not breaking.
- **Bridge's external contracts are:** the bundle manifests it provisions (governed by ADR 0011), the GraphQL schema exposed via DAB (versioned separately), and any REST endpoints (documented per endpoint).
- **Demo auth (`DemoTenantContext`, `MockOktaService`) is explicitly not a compatibility surface** — it's labeled demo-only and expected to be replaced before production.

Future accelerators follow the same pattern: accelerators do not make library-level compatibility promises; they promise behavior for their specific deployment surface.

## Declaring 1.0

When a package is ready:

1. Open an ADR naming the package and justifying 1.0 readiness. Justification includes: API surface stability (≥6 months without unplanned breaking change), consumer adoption (≥1 external), test coverage baseline met.
2. Update the package's `Version` to `1.0.0` in its csproj (break from uniform repo version).
3. Update release notes with a "1.0 commitments" section explicitly stating what the package guarantees going forward.
4. The roadmap tracker records the 1.0 declaration and its date.

Packages go 1.0 independently. The platform as a whole doesn't have a single "Sunfish 1.0" event — it has a sequence of per-package 1.0 declarations.

## What this document does not cover

- **Database schema changes.** Migration safety (backfills, blue-green schema, column-add-then-populate) is a per-accelerator concern. Bridge documents its own.
- **Wire-format compatibility for federation.** Federation packages (`federation-*`) have their own protocol-versioning concerns per the platform spec §3/§4.
- **Docs URL stability.** Published docs-site URLs (`ctwoodwa.github.io/sunfish/`) should stay stable across releases; that's a docs-publishing decision, not a package concern.
- **Versioning of `Directory.Packages.props` entries.** Those track external NuGet versions; bumping them follows each dependency's own semver.

## Pre-1.0 consumer contract (TL;DR)

If you're building on Sunfish today:

- **Pin exact versions** in your consumer's `.csproj`. Don't use floating or `*` versions.
- **Read release notes** before upgrading.
- **Expect minor bumps to break occasionally.** Plan an hour per upgrade for small packages; more for Foundation-tier.
- **ADRs are the canonical "why."** When something moves or renames, the ADR explains the motivation.
- **File an issue** if a minor bump breaks you in a way the release notes didn't call out. That's a bug, even pre-1.0.

## Cross-references

- [architecture-principles.md](architecture-principles.md) §10 — bundle versioning overview.
- [`docs/adrs/0011-bundle-versioning-upgrade-policy.md`](../../docs/adrs/0011-bundle-versioning-upgrade-policy.md) — normative rules for bundle semver.
- [`docs/adrs/0005-type-customization-model.md`](../../docs/adrs/0005-type-customization-model.md) — tenant-overlay three-way-merge commitment.
- [roadmap-tracker.md](roadmap-tracker.md) — follow-up items, including future migration guides.
