# Data Privacy Posture

**Status:** Posture for pre-release
**Last reviewed:** 2026-04-20
**Governs:** Every tenant-data boundary, every bundle-vertical regulatory regime, every export/import pathway, and every place Sunfish code touches personal information — whether in a self-hosted deployment, a Bridge-hosted tenant, or a federated peer exchange.
**Companion docs:** [coding-standards.md](coding-standards.md), [testing-strategy.md](testing-strategy.md), [supply-chain-security.md](supply-chain-security.md), [ai-code-policy.md](ai-code-policy.md), [../product/compatibility-policy.md](../product/compatibility-policy.md), [../product/vision.md §Pillar 1](../product/vision.md), [`../../GOVERNANCE.md`](../../GOVERNANCE.md), [ADR 0005](../../docs/adrs/0005-type-customization-model.md), [ADR 0007](../../docs/adrs/0007-bundle-manifest-schema.md), [ADR 0012](../../docs/adrs/0012-foundation-localfirst.md), [ADR 0013](../../docs/adrs/0013-foundation-integrations.md).

> **Not legal advice.** This is platform posture, not counsel. Deployers retain full regulatory responsibility for their specific deployments, jurisdictions, data subjects, and contractual obligations.

## Posture summary

**Sunfish is a platform, not a SaaS host by default.** Privacy obligations attach primarily to **deployers** — the self-hosted operators, small businesses, and integrators who run Sunfish against real data subjects. Bridge's hosted-SaaS mode adds a second locus of obligation (Sunfish's commercial services arm, when that tier activates) but does not centralize obligation away from the underlying deployer model.

Sunfish's role is to ship **primitives, contracts, and documented patterns** that make the deployer's job tractable: a first-class export operation (ADR 0012), an extension-field catalog that makes PII locatable and targetable (ADR 0005), an audit log that supports subject-rights requests, and provider-neutral integration seams (ADR 0013) that let deployers substitute their own compliance-scoped backends. Sunfish does not hold a Business Associate Agreement with a hospital, ship a fully-formed GDPR Data Processing Agreement, or run a hosted subject-rights intake desk — those are triggered deliverables (see §"Activation triggers"), not day-one pre-release commitments.

The posture is honest: most operational privacy controls are **deferred-with-triggers** in line with [`GOVERNANCE.md` §Transition triggers](../../GOVERNANCE.md#transition-triggers) and the supply-chain-security pattern. Deferral here is not apology — it is the same engineering discipline that keeps the rest of the platform honest about what ships versus what activates on demand.

## Privacy by Design

Sunfish adopts **Ann Cavoukian's 7 Foundational Principles of Privacy by Design** (IPC Ontario, 2009; referenced in GDPR Article 25, CCPA implementing regs, and the ISO/IEC 27701 information-security standard) as platform commitments. Each principle maps to a concrete Sunfish mechanism:

1. **Proactive not reactive; preventative not remedial.** Privacy considerations enter at ADR time, not post-incident. This doc and the bundle-manifest `dataClasses[]` field (ADR 0007) are the proactive surfaces.
2. **Privacy as the default setting.** New bundles inherit minimum-disclosure defaults. Local-first (Pillar 1) means data stays on the user's device unless the user takes an action to move it. Sync is opt-in per bundle, not opt-out.
3. **Privacy embedded into design.** Contracts in `Sunfish.Foundation.LocalFirst` and `Sunfish.Foundation.Catalog` treat data classification and export as first-class, not bolt-on.
4. **Full functionality — positive-sum, not zero-sum.** Sunfish does not trade privacy for features. Federation (ADR 0013) preserves cryptographic user control while enabling cross-org flows — the functionality gain does not require surrendering the privacy posture.
5. **End-to-end security — full lifecycle protection.** Encryption-in-transit is mandatory (§"Encryption posture"); encryption-at-rest is deployer-configured with documented baselines; cryptographic erasure retires data at end-of-life.
6. **Visibility and transparency.** Audit log surfaces every access to classified fields. This document, ADRs, and bundle manifests are the transparency artifacts. No undocumented data flows.
7. **Respect for user privacy — keep it user-centric.** Export is a first-class operation (Pillar 1 ideal #7; ADR 0012 `IDataExportService`). Users can leave with their data at any time. That is the Sunfish commitment in one primitive.

## Data classification taxonomy

Sunfish uses a four-tier classification that maps cleanly to ISO/IEC 27001 Annex A.8.2, NIST SP 800-60, and FIPS 199 categorization models. Every entity, extension field, and bundle artifact is classified into one tier; bundle manifests declare their highest tier in a `dataClasses[]` field (ADR 0007 follow-up).

| Tier | Definition | Sunfish examples |
|---|---|---|
| **Public** | Information whose disclosure has no adverse impact on anyone. | Bundle manifest JSON, ADR content, template definitions (schema + default values, not filled), icon sets, docs-site content, OSS package metadata. |
| **Internal** | Information whose disclosure would have limited or inconvenient impact, not legally sensitive. | Aggregated tenant analytics (counts, no identifiers), non-identifying telemetry (OpenTelemetry traces with PII stripped), Bridge ops dashboards, build logs, internal tenant configuration that doesn't embed user records. |
| **Confidential** | Information whose disclosure would materially harm the tenant or its users — business-sensitive, non-regulated. | Filled template instances without PII (e.g., a property-management maintenance checklist linked to a unit but not a named tenant), tenant subscription and billing records, capability-delegation grants between peers. |
| **Restricted** | Information whose disclosure triggers regulatory, contractual, or reputational consequences. PII, PHI, FERPA-scoped records, financial identifiers. | Tenant names and addresses, tenant-app user account records, PHI in the Medical bundle, FERPA education records in the School bundle, applicant SSN in FCRA-scoped tenant screening, payment-instrument tokens, federated peer credentials. |

Tier assignment drives platform behavior: Restricted fields are eligible for automatic masking in logs; Confidential-and-above require an audit-log entry on access; Public-tier content can be cached freely and served without auth. The classification itself is a platform concern; *which fields fit which tier* is bundle-specific and encoded in the bundle manifest.

## Per-vertical regulatory scope

Each Sunfish bundle inherits the regulatory regime of the vertical it serves. The matrix below records primary regimes. Each row is also the documentation Sunfish maintains when the bundle activates its corresponding trigger.

| Bundle | Primary regulatory regimes | Notes |
|---|---|---|
| **Property Management** | State rental and landlord-tenant statutes (50 US states + DC + territories); federal **Fair Housing Act** (protected-class discrimination); **FCRA** for tenant background screening; state security-deposit holding rules; emerging state rental-listings laws. | Cross-jurisdiction: a landlord in one state, a tenant relocating from another — both states' rules may apply. |
| **Small Medical Office** | **HIPAA** (US) Privacy + Security Rules; **HITECH** breach notification; state-law overlays (e.g., California CMIA, New York SHIELD Act); **PIPEDA** + provincial health-info acts (Canada); **GDPR** Article 9 special-category data (EU). | Requires a Business Associate Agreement when Bridge hosts; requires risk-assessment documentation per the HIPAA Security Rule. |
| **School / Education** | **FERPA** (US K-12 and higher ed); **COPPA** (online services knowingly serving under-13); **PPRA** (protection of student rights amendment); state student-privacy laws (CA SOPIPA, NY Ed Law 2-d, CO HB-1423, and growing); UK DfE guidance (if UK deployment). | "Directory information" and "education record" distinction encoded in bundle manifest; parent/eligible-student consent flows required. |
| **Asset Management** | Depends on asset class: **FDA 21 CFR Part 11** for medical devices; **DOT** regulations for fleet; **IRS** depreciation records; **SOX** for publicly-traded companies' fixed-asset registers. | The bundle itself is regulation-neutral; tenants declare applicable regime via a bundle-manifest field. |
| **Project Management / Facility Ops / Acquisition** | Generally Confidential-tier by default; elevated to Restricted when the project involves regulated data (healthcare build-outs, education construction, financial underwriting). | Inherit regulation from the client-data they handle. |
| **Any bundle crossing jurisdictions** | **Schrems II** considerations for EU↔US data flows; **UK GDPR** post-Brexit; **LGPD** (Brazil); **PIPEDA** (Canada); **PDPA** (Singapore); the growing US state-law patchwork (CA CCPA/CPRA, VA CDPA, CO CPA, CT CTDPA, UT UCPA, TX TDPSA, OR OCPA, MT CDPA, plus 2025-2026 additions). | Federation (ADR 0013) is the transport that makes cross-jurisdiction flows viable — residency controls live at the peer-registry layer. |

Sector-agnostic US federal overlays that apply wherever relevant: **GLBA** (financial institutions handling customer info), **SOX** (public companies' financial records), **CAN-SPAM** and **TCPA** (messaging consent; applies to every bundle's notification surface).

## Data-subject rights

The major privacy regimes converge on a common rights vocabulary (GDPR Articles 15-22; CCPA/CPRA §1798.100-.125; PIPEDA Principle 9; LGPD Articles 17-22; Quebec Law 25; Virginia CDPA §59.1-577). Sunfish supports each right through existing platform primitives, with one gap tracked to Follow-ups.

The fulfillment SLAs differ by regime — GDPR is "without undue delay and in any event within one month" (Art. 12(3)), CCPA is 45 days with a 45-day extension, PIPEDA is 30 days — but the underlying primitives are the same across regimes. Sunfish ships the mechanics; deployers run the clock.

| Right | GDPR Article | Sunfish mechanism |
|---|---|---|
| **Access** (subject access request) | Art. 15 | `IDataExportService` with subject-scoped filter (ADR 0012); audit-log query surfaces every prior access event. |
| **Rectification** | Art. 16 | Extension-field catalog (ADR 0005) makes every tenant-defined PII field discoverable and targetable for update; standard entity updates cover typed fields. |
| **Erasure / right to be forgotten** | Art. 17 | Per-entity delete flows through `IDataExportService`'s inverse and the extension-field catalog; **gap**: bulk erasure across a tenant's entire footprint (e.g., "erase all data for subject X across every bundle") is manual today and scheduled as a Follow-up. |
| **Portability** | Art. 20 | Export is a first-class operation (Pillar 1, ideal #7); ADR 0012 `IDataExportService` produces a machine-readable, deployment-portable bundle — the same primitive supports migration from hosted SaaS to self-hosted. |
| **Restriction of processing** | Art. 18 | Soft-delete semantics in every entity; tenant-configurable flags on extension fields to mark "do not process". |
| **Objection** | Art. 21 | Consent flags on the relevant entity; bundle manifests declare which processing activities are lawful basis vs. consent-based. |
| **No automated decision-making** | Art. 22 | Sunfish ships no autonomous decision surface that makes legal-effect decisions without human review. AI-generated artifacts (per [ai-code-policy.md](ai-code-policy.md)) are developer tooling, not automated decisions about data subjects. |

The CCPA/CPRA-specific rights (right to know, right to delete, right to opt-out of sale/share, right to limit use of sensitive PI, right to correct) map to the same primitives plus the classification taxonomy above. LGPD rights (Art. 18) and PIPEDA access/correction principles likewise.

## Retention and deletion

Retention policy is a **bundle-manifest concern**, not a platform constant. Different verticals require radically different retention windows — HIPAA minimum 6 years for certain records, FERPA education records typically retained through graduation plus a state-specified window, PM state rental-records windows varying by state, tenant-screening FCRA-scoped records retained per reporting requirements. Per ADR 0007, bundle manifests declare per-entity retention in a `retentionPolicy` field (added when the first compliance-scoped bundle activates — see Follow-ups).

**Platform-level minimums:**

- **Soft-delete by default.** All tenant-visible deletes are soft-deletes; hard-delete is a separate tenant-configured action.
- **Tenant-configurable hard-delete window.** Default 30 days; tenants can shorten (down to 0 — immediate hard delete) or extend (up to a bundle-specified statutory maximum).
- **Cryptographic erasure for encrypted blobs.** When encryption-at-rest is enabled with per-tenant keys (see [supply-chain-security.md](supply-chain-security.md) §"Artifact signing" and the key-management follow-up), hard-delete destroys the key — the ciphertext becomes computationally unrecoverable without bulk-scanning storage. This satisfies NIST SP 800-88 Rev. 1 "Cryptographic Erase" for encrypted data at rest.
- **Backup retention alignment.** Deployers must configure backup retention no longer than the longest hard-delete window plus a reasonable recovery buffer. Sunfish documents the pattern; Sunfish does not run the backup infrastructure in self-hosted deployments.

## Encryption posture

**In transit.** TLS 1.3 is the minimum for every Sunfish-operated endpoint — Bridge APIs, federation peer-to-peer sync, Bridge Client to Bridge Server, ports exposed by the App Host. TLS 1.2 is acceptable for inbound external-provider webhooks where the provider does not yet support 1.3, documented per integration. Mutual TLS (mTLS) is required for federation peer-to-peer sync; peer certificates are issued through the capability-delegation flow (`federation-capability-sync`) rather than a centralized CA.

**At rest.** Encryption at rest is the **deployer's responsibility**. Sunfish documents the baseline: transparent database encryption (TDE) on the Postgres tenant-data store, filesystem encryption for blob storage (LUKS on Linux, BitLocker on Windows, FileVault on macOS, platform-native on managed cloud), and per-tenant envelope keys when multi-tenancy is active. Sunfish does not ship application-layer field-level encryption in the pre-release baseline — introducing it is a trigger-gated enhancement (see §"Activation triggers").

**Key management.** Pre-release, Sunfish defers key management to the deployer. Integration points for HSM and cloud KMS (AWS KMS, Azure Key Vault, Google Cloud KMS, HashiCorp Vault) are documented when the first production tenant signals the need. Federation capability keys are held in the tenant's LocalFirst store by default; `Sunfish.Foundation.Integrations` (ADR 0013) provides the seam for KMS-backed key custody without changing contracts.

## Data residency

Multi-region deployment is possible today per ADR 0012 — the `IOfflineStore` and `ISyncEngine` contracts are location-agnostic, and self-hosted deployers choose their own region. Cross-region federation is possible, but the policy of *which peer is allowed to sync with which* is a deployer decision mediated through capability grants.

**Bridge hosted-SaaS residency options** (defined when the hosted-SaaS tier activates per [GOVERNANCE.md](../../GOVERNANCE.md) triggers): US-only, EU-only, Canada-only, and bring-your-own-region. Each option carries its own pricing, its own SLAs, and its own contractual commitments; none are committed pre-release because no hosted tenant exists yet to justify the operational lift.

## Cross-border transfers

The **Schrems II** decision (CJEU, July 2020) and the subsequent **EU-US Data Privacy Framework** (adequacy decision, July 2023; currently subject to Schrems III challenge) shape every EU-origin data flow. Sunfish's posture:

- **Federation peer relationships are the transfer boundary.** A cross-border federation grant is a cross-border data transfer; capability delegation makes the transfer cryptographically auditable, which is a material evidentiary asset in a Schrems II defensibility analysis.
- **Standard Contractual Clauses** (EU Commission Implementing Decision 2021/914) are the fallback transfer mechanism when adequacy does not apply. Sunfish's DPA template (shipped on first-EU-tenant trigger — see §"Activation triggers") incorporates the 2021 SCCs.
- **Transfer Impact Assessment** (per EDPB Recommendation 01/2020) is a deployer deliverable for non-adequate destinations. Sunfish provides a TIA template when the trigger fires.
- **UK IDTA** (International Data Transfer Agreement; post-Brexit UK equivalent to SCCs) is supported through the same template path.

2025-2026 regulatory context Sunfish tracks: **EU Data Act** (entered into application September 2025; affects IoT data, cloud-switching rights, and B2B data-access obligations), **Digital Services Act** enforcement (intermediary transparency, systemic-risk assessments for very large platforms — unlikely to apply to Sunfish directly but relevant to deployers whose tenants reach VLOP scale), **EU AI Act** prohibited-practices provisions (in effect February 2025) and general-purpose AI obligations (August 2025), and the continued proliferation of US state privacy laws (eight new state acts in effect or passed during 2024-2026, with more in committee).

Deployers operating across multiple regimes should assume the **most-protective rule wins by default** — pick the strictest retention minimum, the shortest breach-notification window, and the broadest subject-rights scope across applicable jurisdictions unless a specific business decision (documented and reviewed) justifies otherwise.

## Activation triggers

Sunfish escalates privacy posture on named triggers rather than speculatively, consistent with [`GOVERNANCE.md`](../../GOVERNANCE.md) and [supply-chain-security.md](supply-chain-security.md).

| Trigger | Activates |
|---|---|
| **First PII in a deployed bundle** | Publish a Data Protection Impact Assessment (DPIA) template (GDPR Art. 35 aligned); ensure classification hooks are active in Foundation.Catalog; bundle-manifest `dataClasses[]` becomes a validation requirement. |
| **First HIPAA-scoped deployment** | Ship a Business Associate Agreement (BAA) template (Bridge-hosted path only; self-hosted deployers handle their own BAAs); publish a HIPAA Security Rule risk-assessment companion guide; enforce audit-log immutability for the Medical bundle's Restricted-tier fields. |
| **First FERPA-scoped deployment** | Ship model agreements for school districts and state education agencies; the School bundle's manifest gains a `directoryInformation` vs. `educationRecord` distinction per FERPA §99.3; parent/eligible-student consent flows become mandatory. |
| **First EU-based tenant** | Publish a GDPR Data Processing Agreement template incorporating 2021 SCCs; document whether a Data Protection Officer is required (Art. 37 tests) and, if so, name one; ship the Transfer Impact Assessment template; activate Schrems II transfer mechanisms on any cross-border federation grant. |
| **First hosted-SaaS production tenant** | Publish a full privacy notice, a cookie/tracker policy (if any are used), a subject-rights intake process with documented SLAs, and a breach-notification workflow aligned to GDPR Art. 33-34 (72-hour regulator notice) plus HITECH, CCPA, and state breach-notification statutes. |
| **First financial-records bundle production** | Activate GLBA Safeguards Rule controls; document PCI DSS scope if any payment-card data is handled directly (most bundles avoid this via tokenization through ADR 0013 provider integrations). |
| **First confirmed privacy incident** | Bundle `dataClasses[]` validation becomes a release gate; field-level encryption evaluation for Restricted-tier data; incident-response playbook update. |

## Commercial privacy services

Per vision §"Business model", Sunfish offers **privacy-compliance services as commercial offerings** — helping deployers who would rather pay than DIY. These are orthogonal to the OSS stack (no feature-gating) and include:

- **Privacy-compliance consulting** — DPIA preparation, vendor-assessment support, regulatory-gap analysis for a deployer's specific bundle composition.
- **Regulatory attestation prep** — HIPAA risk assessment packaging, SOC 2 Type I/II prep (when infrastructure justifies), ISO/IEC 27701 implementation guidance.
- **Federation onboarding with privacy review** — helping a school district wire up a state-agency peer with correct residency and consent flows baked in.
- **Subject-rights intake services** — a managed intake desk for deployers who want the obligation handled by a partner.

The OSS stack is sufficient for a competent self-host to meet its obligations; commercial services are for deployers who prefer an informed partner. We make money when we add value.

## What Sunfish will not do

- **No surveillance features.** No "employee monitoring," no covert location tracking, no keystroke capture, no behavioral analytics that identify individuals without consent. Feature requests of this shape are rejected at RFC.
- **No dark-pattern consent flows.** Consent UI in the OSS components meets the **EDPB Guidelines 03/2022** standards (no pre-ticked boxes, symmetric accept/reject, no nagging, no manipulative framing).
- **No undisclosed data sharing.** Every outbound integration is declared in the bundle manifest and auditable at runtime.
- **No cross-tenant analytics without explicit tenant opt-in.** The default is zero cross-tenant data aggregation. Bridge-hosted telemetry is tenant-scoped and identifier-stripped unless the tenant has affirmatively opted into an aggregate program.
- **No secret extension of data retention beyond declared windows.** If the manifest says 30 days, it is 30 days.
- **No "privacy-washing" feature labels.** A feature is privacy-relevant when it alters a data flow; we do not market unrelated features as privacy features.

## Follow-ups — triggered deliverables

Tracked for activation on the triggers above. None are speculative pre-work.

| Follow-up | Trigger |
|---|---|
| **DPIA template** (GDPR Art. 35 aligned, adaptable to CCPA risk assessment and LGPD Art. 38) | First PII in a deployed bundle. |
| **`dataClasses[]` validation in bundle manifests** | First compliance-scoped bundle activation. |
| **Classification-hook tooling in `Foundation.Catalog`** | First compliance-scoped bundle activation. |
| **Bulk subject-erasure API** (cross-entity, cross-bundle) | First subject-deletion request against a deployed bundle, or the first GDPR/CCPA deployment — whichever fires first. |
| **Application-layer field-level encryption for Restricted-tier data** | First HIPAA-scoped deployment or first confirmed privacy incident. |
| **Cryptographic-erasure API surface** (formalized around key-management integration) | First production key-management integration (HSM or cloud KMS). |
| **Per-bundle retention-policy schema** (ADR 0007 follow-up field) | First bundle with a statutory retention minimum distinct from the platform default. |
| **Breach-notification workflow + runbook** | First hosted-SaaS production tenant. |
| **Cookie-and-tracker policy + consent surface** | First Bridge-hosted public-facing deployment. |

## Legal disclaimer

This document is **platform posture, not legal advice**. Sunfish is a software platform; deployers — including any commercial Sunfish services arm operating a Bridge-hosted tenant — retain full regulatory responsibility for their specific deployment, the data subjects they serve, the jurisdictions they operate in, and the contractual commitments they make. Consult qualified counsel for jurisdiction-specific advice. Nothing in this document creates a warranty, a fiduciary duty, or a contractual commitment outside the MIT license's disclaimer.

## Cross-references

- [vision.md §Pillar 1](../product/vision.md) — local-first, user-owned data, export as a first-class operation; the product commitment this document implements.
- [compatibility-policy.md](../product/compatibility-policy.md) — version numbering for privacy-affecting contract changes.
- [supply-chain-security.md](supply-chain-security.md) — encryption, signing, and key-management posture that this doc's Encryption and Retention sections ride on.
- [ai-code-policy.md](ai-code-policy.md) — how AI assistance interacts with data access and subject rights (no automated decision-making about data subjects).
- [testing-strategy.md](testing-strategy.md) — parity, integration, and contract test expectations that include privacy-sensitive code paths.
- [coding-standards.md](coding-standards.md) — XML doc and nullable-reference practices that affect how PII fields are declared and exposed.
- [`../../GOVERNANCE.md`](../../GOVERNANCE.md) — transition triggers shape; this document inherits the same activation discipline.
- [ADR 0005 — Type-customization model](../../docs/adrs/0005-type-customization-model.md) — extension-field catalog that makes PII locatable for rectification/erasure.
- [ADR 0007 — Bundle manifest schema](../../docs/adrs/0007-bundle-manifest-schema.md) — `dataClasses[]` and `retentionPolicy` fields live here.
- [ADR 0012 — Foundation.LocalFirst](../../docs/adrs/0012-foundation-localfirst.md) — `IDataExportService` / `IDataImportService` as the portability primitive.
- [ADR 0013 — Foundation.Integrations](../../docs/adrs/0013-foundation-integrations.md) — provider-neutral seams for KMS, messaging, storage, and payment-tokenization integrations.
- External references: [Cavoukian — Privacy by Design 7 Foundational Principles](https://www.ipc.on.ca/wp-content/uploads/resources/7foundationalprinciples.pdf); [GDPR full text](https://gdpr-info.eu/); [CCPA/CPRA — California AG](https://oag.ca.gov/privacy/ccpa); [HIPAA — HHS Security Rule](https://www.hhs.gov/hipaa/for-professionals/security/index.html); [FERPA — US Dept. of Education](https://studentprivacy.ed.gov/); [EDPB Recommendation 01/2020 (Schrems II)](https://edpb.europa.eu/our-work-tools/our-documents/recommendations/recommendations-012020-measures-supplement-transfer_en); [NIST SP 800-88 Rev. 1 — Guidelines for Media Sanitization](https://csrc.nist.gov/pubs/sp/800/88/r1/final); [ISO/IEC 27701:2019 — Privacy Information Management](https://www.iso.org/standard/71670.html).
