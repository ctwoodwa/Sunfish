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
| [0022](0022-example-catalog-and-docs-taxonomy.md) | Canonical Example Catalog, Documentation Taxonomy, and the Demo-Page Panel | Accepted | — |
| [0023](0023-dialog-provider-slot-methods.md) | Dialog Provider Slot Methods | Accepted | — |
| [0024](0024-button-variant-enum-expansion.md) | Button Variant Enum Expansion | Accepted | — |
| [0025](0025-css-class-prefix-policy.md) | CSS Class Prefix Policy | Accepted | — |
| [0026](0026-bridge-posture.md) | Bridge Posture (SaaS Shell + Managed Relay) | Superseded by 0031 | — |
| [0027](0027-kernel-runtime-split.md) | Kernel Runtime Split | Accepted | — |
| [0028](0028-crdt-engine-selection.md) | CRDT Engine Selection | Accepted | — |
| [0029](0029-federation-reconciliation.md) | Federation and Reconciliation | Accepted | — |
| [0030](0030-react-adapter-scaffolding.md) | React Adapter Scaffolding | Accepted | — |
| [0031](0031-bridge-hybrid-multi-tenant-saas.md) | Bridge as Hybrid Multi-Tenant SaaS (Zone C default, Option B contractual) | Accepted | — |
| [0032](0032-multi-team-anchor-workspace-switching.md) | Multi-Team Anchor (Slack-Style Workspace Switching) | Accepted | — |
| [0033](0033-browser-shell-render-model-and-trust-posture.md) | Browser Shell v1 Render Model and Trust Posture | Accepted | — |
| [0034](0034-a11y-harness-per-adapter.md) | Accessibility Harness per Adapter | Accepted | — |
| [0035](0035-global-domain-types-as-separate-wave.md) | Global Domain Types as Separate Wave | Accepted | — |
| [0036](0036-syncstate-multimodal-encoding-contract.md) | SyncState Multimodal Encoding Contract | Accepted | — |
| [0037](0037-ci-platform-decision.md) | CI Platform Decision (Stay on GitHub Actions, Adopt `act` for Local) | Accepted | — |
| [0038](0038-branch-protection-via-rulesets.md) | Branch Protection via GitHub Rulesets (not legacy branch-protection) | Accepted | — |
| [0039](0039-required-check-minimalism-public-oss.md) | Required-Check Minimalism on Public OSS Repos | Accepted | — |
| [0040](0040-translation-workflow-ai-first-3-stage-validation.md) | Translation Workflow: AI-First with 3-Stage Validation Gate | Accepted | — |
| [0041](0041-dual-namespace-components-rich-vs-mvp.md) | Dual-Namespace Components by Design (Rich vs. MVP) | Accepted | — |
| [0042](0042-subagent-driven-development-for-high-velocity.md) | Subagent-Driven Development for High-Velocity Sessions | Accepted | — |
| [0043](0043-unified-threat-model-public-oss-chain-of-permissiveness.md) | Unified Threat Model — Chain of Permissiveness in Public-OSS Posture (layered over 0038/0039/0042) | Accepted | — |
| [0044](0044-anchor-windows-only-phase-1.md) | Anchor Ships Windows-only for Business MVP Phase 1 | Accepted | — |
| [0046](0046-key-loss-recovery-scheme-phase-1.md) | Anchor Key-Loss Recovery Scheme for Phase 1 (Primitive #48 sub-pattern selection) | Accepted | — |
| [0048](0048-anchor-multi-backend-maui.md) | Anchor multi-backend MAUI: native MAUI for Win/Mac/iOS/Android, MAUI Avalonia for Linux/WebAssembly (extends 0044) | Accepted | — |
| [0049](0049-audit-trail-substrate.md) | Audit-Trail Substrate — distinct `Sunfish.Kernel.Audit` package, parallel to `Kernel.Ledger`, layered over kernel `IEventLog` | Accepted | — |

## Appendix C Resolution

ADRs 0001–0004 close the four open questions from Appendix C of the Sunfish platform specification
that were surfaced as gaps G31–G34 in the gap analysis
(`icm/01_discovery/output/sunfish-gap-analysis-2026-04-18.md`).
