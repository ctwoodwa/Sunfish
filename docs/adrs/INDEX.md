# ADR Topical Index

_Auto-generated from frontmatter by `tools/adr-projections/project.py`. Do not edit by hand._

## By tier

### accelerator (7)

- ADR 0006 — [Bridge Is a Generic SaaS Shell, Not a Vertical App](./0006-bridge-is-saas-shell.md)
- ADR 0026 — [Bridge Posture (SaaS Shell vs. Managed Relay)](./0026-bridge-posture.md)
- ADR 0031 — [Bridge as Hybrid Multi-Tenant SaaS (Zone C default, Option B contractual)](./0031-bridge-hybrid-multi-tenant-saas.md)
- ADR 0032 — [Multi-Team Anchor (Slack-Style Workspace Switching)](./0032-multi-team-anchor-workspace-switching.md)
- ADR 0033 — [Browser Shell v1 Render Model + Trust Posture](./0033-browser-shell-render-model-and-trust-posture.md)
- ADR 0044 — [Anchor ships Windows-only for Business MVP Phase 1](./0044-anchor-windows-only-phase-1.md)
- ADR 0048 — [Anchor multi-backend MAUI: native MAUI for Win/Mac/iOS/Android, MAUI Avalonia for Linux/WebAssembly](./0048-anchor-multi-backend-maui.md)

### adapter (3)

- ADR 0014 — [UI Adapter Parity Policy (Blazor ↔ React)](./0014-adapter-parity-policy.md)
- ADR 0030 — [React Adapter Scaffolding](./0030-react-adapter-scaffolding.md)
- ADR 0034 — [Accessibility Harness Per Adapter](./0034-a11y-harness-per-adapter.md)

### block (5)

- ADR 0053 — [Work Order Domain Model](./0053-work-order-domain-model.md)
- ADR 0054 — [Electronic Signature Capture & Document Binding](./0054-electronic-signature-capture-and-document-binding.md)
- ADR 0057 — [Leasing Pipeline + Fair Housing Compliance Posture](./0057-leasing-pipeline-fair-housing.md)
- ADR 0058 — [Vendor Onboarding Posture](./0058-vendor-onboarding-posture.md)
- ADR 0059 — [Public Listing Surface (Bridge-served)](./0059-public-listing-surface.md)

### foundation (22)

- ADR 0005 — [Type-Customization Model (Typed vs. Dynamic Balance)](./0005-type-customization-model.md)
- ADR 0007 — [Bundle Manifest Schema](./0007-bundle-manifest-schema.md)
- ADR 0008 — [Foundation.MultiTenancy Contracts + Finbuckle Boundary](./0008-foundation-multitenancy.md)
- ADR 0009 — [Foundation.FeatureManagement (Flags vs. Entitlements vs. Editions)](./0009-foundation-featuremanagement.md)
- ADR 0011 — [Bundle Versioning and Upgrade Policy](./0011-bundle-versioning-upgrade-policy.md)
- ADR 0012 — [Foundation.LocalFirst Contracts + Federation Relationship](./0012-foundation-localfirst.md)
- ADR 0013 — [Foundation.Integrations + Provider-Neutrality Policy](./0013-foundation-integrations.md)
- ADR 0015 — [Module-Entity Registration Pattern (Shared Bridge DbContext)](./0015-module-entity-registration.md)
- ADR 0021 — [Document and Report Generation Pipeline](./0021-reporting-pipeline-policy.md)
- ADR 0022 — [Canonical Example Catalog, Documentation Taxonomy, and the Demo-Page Panel](./0022-example-catalog-and-docs-taxonomy.md)
- ADR 0035 — [Global Domain Types as a Separate Wave](./0035-global-domain-types-as-separate-wave.md)
- ADR 0036 — [SyncState Multimodal Encoding Contract](./0036-syncstate-multimodal-encoding-contract.md)
- ADR 0046 — [Historical-Keys Projection for Signature Survival under Operator-Key Rotation](./0046-a1-historical-keys-projection.md)
- ADR 0046 — [Key-loss recovery scheme for Business MVP Phase 1](./0046-key-loss-recovery-scheme-phase-1.md)
- ADR 0051 — [Foundation.Integrations.Payments](./0051-foundation-integrations-payments.md)
- ADR 0052 — [Bidirectional Messaging Substrate](./0052-bidirectional-messaging-substrate.md)
- ADR 0055 — [Dynamic Forms Substrate](./0055-dynamic-forms-substrate.md)
- ADR 0056 — [Foundation.Taxonomy Substrate (Versioned Product Model with Lineage)](./0056-foundation-taxonomy-substrate.md)
- ADR 0061 — [Three-Tier Peer Transport Model (mDNS / Mesh VPN / Managed Relay)](./0061-three-tier-peer-transport.md)
- ADR 0062 — [Mission Space Negotiation Protocol (runtime layer)](./0062-mission-space-negotiation-protocol.md)
- ADR 0063 — [Mission Space Requirements (install-UX layer)](./0063-mission-space-requirements.md)
- ADR 0065 — [Wayfinder System + Standing Order Contract (bundled)](./0065-wayfinder-system-and-standing-order-contract.md)

### governance (8)

- ADR 0001 — [Schema Registry Governance Model](./0001-schema-registry-governance.md)
- ADR 0016 — [App and Accelerator Naming Convention](./0016-app-and-accelerator-naming.md)
- ADR 0018 — [Governance Model and License Posture](./0018-governance-and-license-posture.md)
- ADR 0029 — [Federation vs. Gossip Reconciliation](./0029-federation-reconciliation.md)
- ADR 0037 — [CI Platform Decision (Stay on GitHub Actions, Adopt `act` for Local)](./0037-ci-platform-decision.md)
- ADR 0038 — [Branch Protection via GitHub Rulesets (not legacy branch-protection)](./0038-branch-protection-via-rulesets.md)
- ADR 0039 — [Required-Check Minimalism on Public OSS Repos](./0039-required-check-minimalism-public-oss.md)
- ADR 0042 — [Subagent-Driven Development for High-Velocity Sessions](./0042-subagent-driven-development-for-high-velocity.md)

### kernel (6)

- ADR 0002 — [Kernel Module Format](./0002-kernel-module-format.md)
- ADR 0003 — [Event-Bus Distribution Semantics](./0003-event-bus-distribution-semantics.md)
- ADR 0004 — [Post-Quantum Signature Migration Plan](./0004-post-quantum-signature-migration.md)
- ADR 0027 — [Kernel Runtime Split](./0027-kernel-runtime-split.md)
- ADR 0028 — [CRDT Engine Selection](./0028-crdt-engine-selection.md)
- ADR 0049 — [Audit-Trail Substrate: Distinct Package over Kernel IEventLog](./0049-audit-trail-substrate.md)

### policy (3)

- ADR 0043 — [Unified Threat Model: The Chain of Permissiveness in Sunfish''s Public-OSS Posture](./0043-unified-threat-model-public-oss-chain-of-permissiveness.md)
- ADR 0060 — [Right-of-Entry Compliance Framework](./0060-right-of-entry-compliance-framework.md)
- ADR 0064 — [Runtime Regulatory / Jurisdictional Policy Evaluation](./0064-runtime-regulatory-policy-evaluation.md)

### process (1)

- ADR 0040 — [Translation Workflow: AI-First with 3-Stage Validation Gate](./0040-translation-workflow-ai-first-3-stage-validation.md)

### tooling (1)

- ADR 0010 — [Templates Module Boundary (Foundation.Catalog vs. blocks-templating)](./0010-templates-boundary.md)

### ui-core (5)

- ADR 0017 — [Spec-First UI Contracts with Native Framework Adapters and an Optional Web-Components Consumption Track](./0017-web-components-lit-technical-basis.md)
- ADR 0023 — [Dialog Provider-Interface Expansion (Per-Slot Class Methods)](./0023-dialog-provider-slot-methods.md)
- ADR 0024 — [ButtonVariant Enum Expansion for Cross-Framework Style Parity](./0024-button-variant-enum-expansion.md)
- ADR 0025 — [CSS Class Prefix Policy (`sf-*`, `mar-*`, `k-*`)](./0025-css-class-prefix-policy.md)
- ADR 0041 — [Dual-Namespace Components by Design (Rich vs. MVP)](./0041-dual-namespace-components-rich-vs-mvp.md)

## By concern

### accessibility (4)

- ADR 0017 — [Spec-First UI Contracts with Native Framework Adapters and an Optional Web-Components Consumption Track](./0017-web-components-lit-technical-basis.md)
- ADR 0023 — [Dialog Provider-Interface Expansion (Per-Slot Class Methods)](./0023-dialog-provider-slot-methods.md)
- ADR 0034 — [Accessibility Harness Per Adapter](./0034-a11y-harness-per-adapter.md)
- ADR 0036 — [SyncState Multimodal Encoding Contract](./0036-syncstate-multimodal-encoding-contract.md)

### audit (9)

- ADR 0046 — [Historical-Keys Projection for Signature Survival under Operator-Key Rotation](./0046-a1-historical-keys-projection.md)
- ADR 0049 — [Audit-Trail Substrate: Distinct Package over Kernel IEventLog](./0049-audit-trail-substrate.md)
- ADR 0052 — [Bidirectional Messaging Substrate](./0052-bidirectional-messaging-substrate.md)
- ADR 0053 — [Work Order Domain Model](./0053-work-order-domain-model.md)
- ADR 0054 — [Electronic Signature Capture & Document Binding](./0054-electronic-signature-capture-and-document-binding.md)
- ADR 0057 — [Leasing Pipeline + Fair Housing Compliance Posture](./0057-leasing-pipeline-fair-housing.md)
- ADR 0058 — [Vendor Onboarding Posture](./0058-vendor-onboarding-posture.md)
- ADR 0060 — [Right-of-Entry Compliance Framework](./0060-right-of-entry-compliance-framework.md)
- ADR 0062 — [Mission Space Negotiation Protocol (runtime layer)](./0062-mission-space-negotiation-protocol.md)

### capability-model (2)

- ADR 0062 — [Mission Space Negotiation Protocol (runtime layer)](./0062-mission-space-negotiation-protocol.md)
- ADR 0063 — [Mission Space Requirements (install-UX layer)](./0063-mission-space-requirements.md)

### commercial (5)

- ADR 0007 — [Bundle Manifest Schema](./0007-bundle-manifest-schema.md)
- ADR 0009 — [Foundation.FeatureManagement (Flags vs. Entitlements vs. Editions)](./0009-foundation-featuremanagement.md)
- ADR 0011 — [Bundle Versioning and Upgrade Policy](./0011-bundle-versioning-upgrade-policy.md)
- ADR 0021 — [Document and Report Generation Pipeline](./0021-reporting-pipeline-policy.md)
- ADR 0051 — [Foundation.Integrations.Payments](./0051-foundation-integrations-payments.md)

### configuration (7)

- ADR 0005 — [Type-Customization Model (Typed vs. Dynamic Balance)](./0005-type-customization-model.md)
- ADR 0007 — [Bundle Manifest Schema](./0007-bundle-manifest-schema.md)
- ADR 0009 — [Foundation.FeatureManagement (Flags vs. Entitlements vs. Editions)](./0009-foundation-featuremanagement.md)
- ADR 0013 — [Foundation.Integrations + Provider-Neutrality Policy](./0013-foundation-integrations.md)
- ADR 0055 — [Dynamic Forms Substrate](./0055-dynamic-forms-substrate.md)
- ADR 0056 — [Foundation.Taxonomy Substrate (Versioned Product Model with Lineage)](./0056-foundation-taxonomy-substrate.md)
- ADR 0060 — [Right-of-Entry Compliance Framework](./0060-right-of-entry-compliance-framework.md)

### data-residency (1)

- ADR 0064 — [Runtime Regulatory / Jurisdictional Policy Evaluation](./0064-runtime-regulatory-policy-evaluation.md)

### dev-experience (21)

- ADR 0002 — [Kernel Module Format](./0002-kernel-module-format.md)
- ADR 0005 — [Type-Customization Model (Typed vs. Dynamic Balance)](./0005-type-customization-model.md)
- ADR 0010 — [Templates Module Boundary (Foundation.Catalog vs. blocks-templating)](./0010-templates-boundary.md)
- ADR 0014 — [UI Adapter Parity Policy (Blazor ↔ React)](./0014-adapter-parity-policy.md)
- ADR 0015 — [Module-Entity Registration Pattern (Shared Bridge DbContext)](./0015-module-entity-registration.md)
- ADR 0016 — [App and Accelerator Naming Convention](./0016-app-and-accelerator-naming.md)
- ADR 0021 — [Document and Report Generation Pipeline](./0021-reporting-pipeline-policy.md)
- ADR 0022 — [Canonical Example Catalog, Documentation Taxonomy, and the Demo-Page Panel](./0022-example-catalog-and-docs-taxonomy.md)
- ADR 0025 — [CSS Class Prefix Policy (`sf-*`, `mar-*`, `k-*`)](./0025-css-class-prefix-policy.md)
- ADR 0027 — [Kernel Runtime Split](./0027-kernel-runtime-split.md)
- ADR 0030 — [React Adapter Scaffolding](./0030-react-adapter-scaffolding.md)
- ADR 0035 — [Global Domain Types as a Separate Wave](./0035-global-domain-types-as-separate-wave.md)
- ADR 0037 — [CI Platform Decision (Stay on GitHub Actions, Adopt `act` for Local)](./0037-ci-platform-decision.md)
- ADR 0039 — [Required-Check Minimalism on Public OSS Repos](./0039-required-check-minimalism-public-oss.md)
- ADR 0040 — [Translation Workflow: AI-First with 3-Stage Validation Gate](./0040-translation-workflow-ai-first-3-stage-validation.md)
- ADR 0041 — [Dual-Namespace Components by Design (Rich vs. MVP)](./0041-dual-namespace-components-rich-vs-mvp.md)
- ADR 0042 — [Subagent-Driven Development for High-Velocity Sessions](./0042-subagent-driven-development-for-high-velocity.md)
- ADR 0044 — [Anchor ships Windows-only for Business MVP Phase 1](./0044-anchor-windows-only-phase-1.md)
- ADR 0048 — [Anchor multi-backend MAUI: native MAUI for Win/Mac/iOS/Android, MAUI Avalonia for Linux/WebAssembly](./0048-anchor-multi-backend-maui.md)
- ADR 0055 — [Dynamic Forms Substrate](./0055-dynamic-forms-substrate.md)
- ADR 0063 — [Mission Space Requirements (install-UX layer)](./0063-mission-space-requirements.md)

### distribution (8)

- ADR 0003 — [Event-Bus Distribution Semantics](./0003-event-bus-distribution-semantics.md)
- ADR 0013 — [Foundation.Integrations + Provider-Neutrality Policy](./0013-foundation-integrations.md)
- ADR 0027 — [Kernel Runtime Split](./0027-kernel-runtime-split.md)
- ADR 0028 — [CRDT Engine Selection](./0028-crdt-engine-selection.md)
- ADR 0029 — [Federation vs. Gossip Reconciliation](./0029-federation-reconciliation.md)
- ADR 0052 — [Bidirectional Messaging Substrate](./0052-bidirectional-messaging-substrate.md)
- ADR 0061 — [Three-Tier Peer Transport Model (mDNS / Mesh VPN / Managed Relay)](./0061-three-tier-peer-transport.md)
- ADR 0062 — [Mission Space Negotiation Protocol (runtime layer)](./0062-mission-space-negotiation-protocol.md)

### governance (11)

- ADR 0001 — [Schema Registry Governance Model](./0001-schema-registry-governance.md)
- ADR 0002 — [Kernel Module Format](./0002-kernel-module-format.md)
- ADR 0016 — [App and Accelerator Naming Convention](./0016-app-and-accelerator-naming.md)
- ADR 0018 — [Governance Model and License Posture](./0018-governance-and-license-posture.md)
- ADR 0037 — [CI Platform Decision (Stay on GitHub Actions, Adopt `act` for Local)](./0037-ci-platform-decision.md)
- ADR 0038 — [Branch Protection via GitHub Rulesets (not legacy branch-protection)](./0038-branch-protection-via-rulesets.md)
- ADR 0039 — [Required-Check Minimalism on Public OSS Repos](./0039-required-check-minimalism-public-oss.md)
- ADR 0040 — [Translation Workflow: AI-First with 3-Stage Validation Gate](./0040-translation-workflow-ai-first-3-stage-validation.md)
- ADR 0042 — [Subagent-Driven Development for High-Velocity Sessions](./0042-subagent-driven-development-for-high-velocity.md)
- ADR 0043 — [Unified Threat Model: The Chain of Permissiveness in Sunfish''s Public-OSS Posture](./0043-unified-threat-model-public-oss-chain-of-permissiveness.md)
- ADR 0056 — [Foundation.Taxonomy Substrate (Versioned Product Model with Lineage)](./0056-foundation-taxonomy-substrate.md)

### identity (5)

- ADR 0008 — [Foundation.MultiTenancy Contracts + Finbuckle Boundary](./0008-foundation-multitenancy.md)
- ADR 0032 — [Multi-Team Anchor (Slack-Style Workspace Switching)](./0032-multi-team-anchor-workspace-switching.md)
- ADR 0033 — [Browser Shell v1 Render Model + Trust Posture](./0033-browser-shell-render-model-and-trust-posture.md)
- ADR 0046 — [Key-loss recovery scheme for Business MVP Phase 1](./0046-key-loss-recovery-scheme-phase-1.md)
- ADR 0058 — [Vendor Onboarding Posture](./0058-vendor-onboarding-posture.md)

### mission-space (2)

- ADR 0062 — [Mission Space Negotiation Protocol (runtime layer)](./0062-mission-space-negotiation-protocol.md)
- ADR 0063 — [Mission Space Requirements (install-UX layer)](./0063-mission-space-requirements.md)

### multi-tenancy (9)

- ADR 0006 — [Bridge Is a Generic SaaS Shell, Not a Vertical App](./0006-bridge-is-saas-shell.md)
- ADR 0007 — [Bundle Manifest Schema](./0007-bundle-manifest-schema.md)
- ADR 0008 — [Foundation.MultiTenancy Contracts + Finbuckle Boundary](./0008-foundation-multitenancy.md)
- ADR 0009 — [Foundation.FeatureManagement (Flags vs. Entitlements vs. Editions)](./0009-foundation-featuremanagement.md)
- ADR 0031 — [Bridge as Hybrid Multi-Tenant SaaS (Zone C default, Option B contractual)](./0031-bridge-hybrid-multi-tenant-saas.md)
- ADR 0032 — [Multi-Team Anchor (Slack-Style Workspace Switching)](./0032-multi-team-anchor-workspace-switching.md)
- ADR 0052 — [Bidirectional Messaging Substrate](./0052-bidirectional-messaging-substrate.md)
- ADR 0057 — [Leasing Pipeline + Fair Housing Compliance Posture](./0057-leasing-pipeline-fair-housing.md)
- ADR 0059 — [Public Listing Surface (Bridge-served)](./0059-public-listing-surface.md)

### operations (5)

- ADR 0006 — [Bridge Is a Generic SaaS Shell, Not a Vertical App](./0006-bridge-is-saas-shell.md)
- ADR 0011 — [Bundle Versioning and Upgrade Policy](./0011-bundle-versioning-upgrade-policy.md)
- ADR 0031 — [Bridge as Hybrid Multi-Tenant SaaS (Zone C default, Option B contractual)](./0031-bridge-hybrid-multi-tenant-saas.md)
- ADR 0044 — [Anchor ships Windows-only for Business MVP Phase 1](./0044-anchor-windows-only-phase-1.md)
- ADR 0048 — [Anchor multi-backend MAUI: native MAUI for Win/Mac/iOS/Android, MAUI Avalonia for Linux/WebAssembly](./0048-anchor-multi-backend-maui.md)

### persistence (10)

- ADR 0001 — [Schema Registry Governance Model](./0001-schema-registry-governance.md)
- ADR 0003 — [Event-Bus Distribution Semantics](./0003-event-bus-distribution-semantics.md)
- ADR 0005 — [Type-Customization Model (Typed vs. Dynamic Balance)](./0005-type-customization-model.md)
- ADR 0010 — [Templates Module Boundary (Foundation.Catalog vs. blocks-templating)](./0010-templates-boundary.md)
- ADR 0015 — [Module-Entity Registration Pattern (Shared Bridge DbContext)](./0015-module-entity-registration.md)
- ADR 0028 — [CRDT Engine Selection](./0028-crdt-engine-selection.md)
- ADR 0035 — [Global Domain Types as a Separate Wave](./0035-global-domain-types-as-separate-wave.md)
- ADR 0049 — [Audit-Trail Substrate: Distinct Package over Kernel IEventLog](./0049-audit-trail-substrate.md)
- ADR 0053 — [Work Order Domain Model](./0053-work-order-domain-model.md)
- ADR 0055 — [Dynamic Forms Substrate](./0055-dynamic-forms-substrate.md)

### regulatory (7)

- ADR 0051 — [Foundation.Integrations.Payments](./0051-foundation-integrations-payments.md)
- ADR 0053 — [Work Order Domain Model](./0053-work-order-domain-model.md)
- ADR 0054 — [Electronic Signature Capture & Document Binding](./0054-electronic-signature-capture-and-document-binding.md)
- ADR 0057 — [Leasing Pipeline + Fair Housing Compliance Posture](./0057-leasing-pipeline-fair-housing.md)
- ADR 0059 — [Public Listing Surface (Bridge-served)](./0059-public-listing-surface.md)
- ADR 0060 — [Right-of-Entry Compliance Framework](./0060-right-of-entry-compliance-framework.md)
- ADR 0064 — [Runtime Regulatory / Jurisdictional Policy Evaluation](./0064-runtime-regulatory-policy-evaluation.md)

### security (17)

- ADR 0004 — [Post-Quantum Signature Migration Plan](./0004-post-quantum-signature-migration.md)
- ADR 0013 — [Foundation.Integrations + Provider-Neutrality Policy](./0013-foundation-integrations.md)
- ADR 0018 — [Governance Model and License Posture](./0018-governance-and-license-posture.md)
- ADR 0029 — [Federation vs. Gossip Reconciliation](./0029-federation-reconciliation.md)
- ADR 0031 — [Bridge as Hybrid Multi-Tenant SaaS (Zone C default, Option B contractual)](./0031-bridge-hybrid-multi-tenant-saas.md)
- ADR 0033 — [Browser Shell v1 Render Model + Trust Posture](./0033-browser-shell-render-model-and-trust-posture.md)
- ADR 0038 — [Branch Protection via GitHub Rulesets (not legacy branch-protection)](./0038-branch-protection-via-rulesets.md)
- ADR 0043 — [Unified Threat Model: The Chain of Permissiveness in Sunfish''s Public-OSS Posture](./0043-unified-threat-model-public-oss-chain-of-permissiveness.md)
- ADR 0046 — [Historical-Keys Projection for Signature Survival under Operator-Key Rotation](./0046-a1-historical-keys-projection.md)
- ADR 0046 — [Key-loss recovery scheme for Business MVP Phase 1](./0046-key-loss-recovery-scheme-phase-1.md)
- ADR 0049 — [Audit-Trail Substrate: Distinct Package over Kernel IEventLog](./0049-audit-trail-substrate.md)
- ADR 0051 — [Foundation.Integrations.Payments](./0051-foundation-integrations-payments.md)
- ADR 0054 — [Electronic Signature Capture & Document Binding](./0054-electronic-signature-capture-and-document-binding.md)
- ADR 0058 — [Vendor Onboarding Posture](./0058-vendor-onboarding-posture.md)
- ADR 0059 — [Public Listing Surface (Bridge-served)](./0059-public-listing-surface.md)
- ADR 0061 — [Three-Tier Peer Transport Model (mDNS / Mesh VPN / Managed Relay)](./0061-three-tier-peer-transport.md)
- ADR 0064 — [Runtime Regulatory / Jurisdictional Policy Evaluation](./0064-runtime-regulatory-policy-evaluation.md)

### threat-model (1)

- ADR 0043 — [Unified Threat Model: The Chain of Permissiveness in Sunfish''s Public-OSS Posture](./0043-unified-threat-model-public-oss-chain-of-permissiveness.md)

### ui (10)

- ADR 0014 — [UI Adapter Parity Policy (Blazor ↔ React)](./0014-adapter-parity-policy.md)
- ADR 0017 — [Spec-First UI Contracts with Native Framework Adapters and an Optional Web-Components Consumption Track](./0017-web-components-lit-technical-basis.md)
- ADR 0023 — [Dialog Provider-Interface Expansion (Per-Slot Class Methods)](./0023-dialog-provider-slot-methods.md)
- ADR 0024 — [ButtonVariant Enum Expansion for Cross-Framework Style Parity](./0024-button-variant-enum-expansion.md)
- ADR 0025 — [CSS Class Prefix Policy (`sf-*`, `mar-*`, `k-*`)](./0025-css-class-prefix-policy.md)
- ADR 0030 — [React Adapter Scaffolding](./0030-react-adapter-scaffolding.md)
- ADR 0033 — [Browser Shell v1 Render Model + Trust Posture](./0033-browser-shell-render-model-and-trust-posture.md)
- ADR 0034 — [Accessibility Harness Per Adapter](./0034-a11y-harness-per-adapter.md)
- ADR 0036 — [SyncState Multimodal Encoding Contract](./0036-syncstate-multimodal-encoding-contract.md)
- ADR 0041 — [Dual-Namespace Components by Design (Rich vs. MVP)](./0041-dual-namespace-components-rich-vs-mvp.md)

### version-management (5)

- ADR 0001 — [Schema Registry Governance Model](./0001-schema-registry-governance.md)
- ADR 0004 — [Post-Quantum Signature Migration Plan](./0004-post-quantum-signature-migration.md)
- ADR 0011 — [Bundle Versioning and Upgrade Policy](./0011-bundle-versioning-upgrade-policy.md)
- ADR 0028 — [CRDT Engine Selection](./0028-crdt-engine-selection.md)
- ADR 0056 — [Foundation.Taxonomy Substrate (Versioned Product Model with Lineage)](./0056-foundation-taxonomy-substrate.md)
