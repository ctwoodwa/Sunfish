# Architecture Decision Records

This directory contains Architecture Decision Records (ADRs) for the Sunfish platform.

## Format

Each ADR uses a minimal five-section template: **Status, Context, Decision, Consequences, References**.

Files are named `NNNN-short-slug.md` starting at `0001`.

## Index

| # | Title | Status | Gap |
|---|-------|--------|-----|
| [0001](0001-schema-registry-governance.md) | Schema Registry Governance Model | Accepted | G31 |
| [0002](0002-kernel-module-format.md) | Kernel Module Format | Accepted | G32 |
| [0003](0003-event-bus-distribution-semantics.md) | Event-Bus Distribution Semantics | Accepted | G33 |
| [0004](0004-post-quantum-signature-migration.md) | Post-Quantum Signature Migration Plan | Accepted | G34 |
| [0005](0005-type-customization-model.md) | Type-Customization Model (Typed vs. Dynamic Balance) | Accepted | — |
| [0006](0006-bridge-is-saas-shell.md) | Bridge Is a Generic SaaS Shell, Not a Vertical App | Accepted | — |
| [0007](0007-bundle-manifest-schema.md) | Bundle Manifest Schema | Accepted | — |
| [0008](0008-foundation-multitenancy.md) | Foundation.MultiTenancy Contracts + Finbuckle Boundary | Accepted | — |
| [0009](0009-foundation-featuremanagement.md) | Foundation.FeatureManagement (Flags vs. Entitlements vs. Editions) | Accepted | — |
| [0010](0010-templates-boundary.md) | Templates Module Boundary (Foundation.Catalog vs. blocks-templating) | Accepted | — |
| [0011](0011-bundle-versioning-upgrade-policy.md) | Bundle Versioning and Upgrade Policy | Accepted | — |
| [0012](0012-foundation-localfirst.md) | Foundation.LocalFirst Contracts + Federation Relationship | Accepted | — |
| [0013](0013-foundation-integrations.md) | Foundation.Integrations + Provider-Neutrality Policy | Accepted | — |
| [0014](0014-adapter-parity-policy.md) | UI Adapter Parity Policy (Blazor ↔ React) | Accepted | — |
| [0016](0016-app-and-accelerator-naming.md) | App and Accelerator Naming Convention | Accepted | — |
| [0017](0017-web-components-lit-technical-basis.md) | Web Components (via Lit) as UI Technical Basis; JS and WASM Consumption Tracks | Accepted | — |
| [0018](0018-governance-and-license-posture.md) | Governance Model and License Posture (BDFL + MIT + ODF/UPF/ICM stack) | Accepted | — |
| [0021](0021-reporting-pipeline-policy.md) | Document and Report Generation Pipeline (Contracts + Pure-OSS Defaults + Commercial Adapters) | Accepted | — |
| [0043](0043-unified-threat-model-public-oss-chain-of-permissiveness.md) | Unified Threat Model — Chain of Permissiveness in Public-OSS Posture (layered over 0038/0039/0042) | Accepted | — |

## Appendix C Resolution

ADRs 0001–0004 close the four open questions from Appendix C of the Sunfish platform specification
that were surfaced as gaps G31–G34 in the gap analysis
(`icm/01_discovery/output/sunfish-gap-analysis-2026-04-18.md`).
