# Intake Brainstorm — Platform Features: Layered Startup, Visibility Modes, Support Delegation, Sensitivity Classification

**Status:** `brainstorm` — Stage 00 research-backlog. **sunfish-PM: do not build against this.** Research session captures ideas, identifies cross-cutting themes, and queues each for separate Stage 01 Discovery when promoted.
**Status owner:** research session
**Date:** 2026-04-28
**Requestor:** Christopher Wood (BDFL) — voice-dictated brainstorm session
**Pipeline variant (provisional):** `sunfish-feature-change` per individual feature when promoted; this intake itself is `sunfish-gap-analysis`-shaped (capturing missing capabilities)

---

## Why these four ideas are filed together

The four ideas the BDFL named cluster around a single underlying question: **"who sees what data, in what form, under what context — and how does the application know?"** Three of them (visibility modes, support delegation, sensitivity classification) are different consumers of the same underlying data model; the fourth (layered startup) is the substrate that loads the policy at runtime.

```
                    Sensitivity Classification (idea 4)
                              ↓ data model
            ┌─────────────────┼─────────────────┐
            ↓                 ↓                 ↓
      Visibility        Support             [Future]
      Modes             Delegation          (e.g., export
      (idea 2)          (idea 3)             redaction,
                                             audit-record
                                             retention,
                                             CCPA right-to-know)
                              ↓
                    Layered Startup (idea 1)
                    — composes the policy at app-launch
```

The brainstorm captures each idea individually; promotion to Stage 01 Discovery is per-idea, but the data model (idea 4) is likely the foundation that gates ideas 2 and 3.

---

## Idea 1 — Layered Application Startup

### What the BDFL named

> "Should we do something similar to how Claude starts up in having different layers of settings? In the Claude world it sets up the context and rules. Is there something — lessons learned — in that sequence of startup that should be or could be used?"

### What this means in Sunfish-Anchor terms

Claude Code's startup is layered: system prompt → user-level CLAUDE.md (`~/.claude/CLAUDE.md`) → project CLAUDE.md → loaded memory (project-keyed) → conversation history → user message. Each layer can override / augment the previous; no single layer holds the full policy.

Anchor's startup today (per `accelerators/anchor/Sunfish.Anchor/MauiProgram.cs` and ADR 0032 multi-team workspace switching) is closer to flat-DI: services register in one pass, then a `TeamContext` is selected and consumed throughout the app. The "context and rules" Claude has at startup are partly equivalent to:

- **Capability graph + macaroons** (what's authorized for this principal in this tenant — `Foundation.Capabilities`)
- **Feature flags + entitlements + editions** (what's enabled for this tenant — `Foundation.FeatureManagement`, ADR 0009)
- **Tenant resolver + tenant catalog** (which tenant is active — `Foundation.MultiTenancy`, ADR 0008)
- **Schema epoch + bundle catalog** (which versions of which modules are loaded — ADR 0001, 0007)

### The lesson worth porting

Claude's layering has three properties Sunfish-Anchor doesn't yet have systemically:

1. **Each layer is independently overridable.** Project CLAUDE.md overrides user-level; memory can override; session can override. A user can carry "do not write production code" as a feedback memory and that survives across sessions and projects.
2. **Layers compose deterministically.** Claude doesn't load layers in arbitrary order — there's a documented sequence and conflict-resolution rule.
3. **Layers are observable as data.** The user can read CLAUDE.md, list memory entries, see the chain. The startup is not a black box.

### Open questions for Stage 01 Discovery (when promoted)

- **What are Anchor's natural startup layers?** Candidates: (a) machine defaults; (b) user defaults across all tenants; (c) tenant defaults; (d) team defaults (per ADR 0032); (e) per-session overrides (e.g., "demo mode," "support session"). Other candidates from research (12-factor config, .NET options pattern, hierarchical DI scopes)?
- **Do block packages need a layered config story too?** `blocks-accounting`'s number-format defaults, `blocks-rent-collection`'s late-fee policy — are these block-level config or tenant-level config or both?
- **What governs override conflict resolution?** Most-specific-wins is the obvious default but doesn't always fit (e.g., security rules where strictest-wins regardless of layer specificity).
- **Are some layers hot-reloadable (e.g., feature flags) vs cold (e.g., schema epoch)?** ADR 0001/0007 already imply this distinction — make it first-class.

### Existing primitives to compose with

- ADR 0008 (Foundation.MultiTenancy) + ADR 0032 (multi-team Anchor) for tenant/team layer
- ADR 0009 (Foundation.FeatureManagement) for flag/entitlement/edition layer
- `foundation/Capabilities/` + macaroons for principal-scoped layer
- `Foundation.LocalFirst` (ADR 0012) for offline-resilience of layer state

### Industry references to research

- 12-factor config (https://12factor.net/config)
- .NET options pattern + `IConfiguration` provider chain (built-in layered config)
- Linux PAM (Pluggable Authentication Modules) — successive modules can `required` / `sufficient` / `optional` overrides
- VS Code's settings cascade (defaults → user → workspace → folder)
- Browser CSS cascade — analogous specificity model

---

## Idea 2 — Visibility Modes (screenshot/demo redaction)

### What the BDFL named

> "When a user is using the application, they may want to redact the figures and PII information so they can do screenshots."

### What this means

A runtime mode where the UI displays sensitive values as placeholders (`••••`, `XXX-XX-XXXX`, masked balances, blurred photos) so the user can take a screenshot for support, demo, training, or social-media purposes without leaking real data. Toggle-able from a menu, per-component or app-wide.

### Use cases (synthesized from existing memory + the BDFL's prior contexts)

- **Phase 2 commercial scope**: BDFL takes screenshots to share with bookkeeper or tax advisor; balances and tenant-personal-info should auto-redact.
- **Demo / sales walkthroughs**: showing the app to a prospective customer with realistic-looking but redacted data.
- **Support tickets**: "Here's a screenshot of the bug" without exposing tenant SSN, lease-holder names, payment account numbers.
- **Training / documentation screenshots**: capturing the kitchen-sink demo or apps/docs without baking real identifiable data into the artifact.
- **Medical-provider analog** (per the convention intake's worked example): provider screenshots their workflow for a peer review without exposing patient identifiers.

### Open questions for Stage 01 Discovery (when promoted)

- **Where does the policy live?** Component-level (each component declares "this field is redactable")? Block-level (each `blocks-*` declares its sensitivity map)? Foundation-level (a redaction registry consumed at render time)?
- **What's the redaction display rule?** Same character count vs. fixed mask length? Format-preserving (e.g., `XXX-XX-1234` keeps last 4)? Type-preserving (`$••.••` for currency)?
- **Per-component opt-in vs opt-out?** Default-redact-when-mode-on is safer; default-show-when-mode-on requires every sensitive field to be explicitly tagged.
- **Does the kernel know about redaction or is it purely UI-tier?** Storage stays unredacted; redaction is presentation-only. But what about reports/exports — does ADR 0021's pipeline need a redacted variant?
- **How does this interact with ADR 0049 audit?** Should "redacted screenshot taken" itself be auditable? (Probably yes for compliance scenarios.)

### Existing primitives to compose with

- ui-core component contracts (each component could grow a `redactable` parameter or an `IRedactable` markup interface)
- Foundation's data classification (idea 4 below) — redaction targets fields tagged at certain sensitivity levels
- ADR 0021 reporting pipeline (for exported documents)
- Foundation.LocalFirst (the redaction state itself is local-per-session, not tenant-state)

### Industry references to research

- iOS screen-recording detection + auto-redaction (banking apps darken sensitive views when recording detected)
- Slack's "redact screenshots" / privacy mode (cmd+shift+. style)
- macOS `NSSecureField` and screen-capture-protection flags
- Windows `WS_EX_NOREDIRECTIONBITMAP` for sensitive overlays
- Stripe's "demo mode" (test data with prefix `cus_test_*`) — analogous but data-side rather than display-side

---

## Idea 3 — Support Delegation (capability-trimmed troubleshooting access)

### What the BDFL named

> "How do we handle support? Is that another user that gets delegated? And for support users, how can we allow them to troubleshoot without actually accessing maybe sensitive information?"

### What this means

A pattern for letting a third party (vendor support, IT helpdesk, Sunfish-as-managed-service support, an MSP) access a tenant's session for troubleshooting **without seeing sensitive data**. The support user is a Principal (per `Foundation.Capabilities` + macaroons) with a time-bounded, capability-trimmed grant — they can see UI structure, error messages, system state, and metadata, but PII / financial figures / tenant identifiers are masked.

This is structurally similar to the bookkeeper / tax-advisor delegation already in Phase 2 scope, but with **higher redaction** because the support user has no business need for the underlying values, only the system behavior.

### Use cases

- **Sunfish-managed-service support** (post-LLC, when Sunfish operates a hosted Bridge): support engineer needs to debug a customer's issue.
- **Third-party MSP**: BDFL hires an MSP to troubleshoot Anchor on his work machine; MSP needs UI access without leaking tenant data.
- **In-house IT for larger Bridge deployments** (Phase 3+): IT helpdesk troubleshoots a user's issue without exposing the user's data.
- **Vendor support for blocks-***: a `blocks-payments` vendor support engineer helps debug a payment flow issue without seeing the customer's actual card transactions.

### Connection to existing patterns

- **Salesforce "Login As"** pattern with Privileged Access Management (PAM) + Just-In-Time (JIT) credentials
- **AWS Support's session-share model** — customer initiates, support gets time-bounded scoped access
- **Apple's "Screen Sharing" via Apple ID** with redaction overlays — partial analog

### Open questions for Stage 01 Discovery (when promoted)

- **Is "support" a separate Principal type or just a capability profile on a regular Principal?** Separate type buys clarity; profile buys reuse.
- **How is the access granted?** User-initiated ("I need help, please share") with ephemeral macaroon caveats? Pre-authorized SLA-based? Both?
- **What's the default redaction posture?** Strictest (everything classified Confidential or above is redacted) vs. opt-in by the supporting user when they need to see something?
- **Does the support user see audit records?** The audit trail is metadata about access events; support might legitimately need to see "what error happened when," but not the underlying data values that triggered it.
- **How do we prevent screenshot exfiltration by the support user?** UI-tier redaction is one layer; OS-level screen-capture-protection is another; client-binding cryptography (the support user's session is bound to *their* device, not redistributable) is a third.
- **How does ADR 0049 audit handle support sessions?** Probably needs a new `AuditEventType.SupportSessionStarted` / `.SupportAccessGranted` / `.SupportSessionEnded` set.

### Existing primitives to compose with

- `Foundation.Capabilities` + macaroons (caveats: principal, expiry, redaction-level, IP scope)
- ADR 0049 audit trail (every support action is auditable)
- ADR 0046 Recovery (the social-recovery trustee model is the closest precedent — third-party time-bounded access with strong attestation)
- The capability-trimmed Anchor UI pattern named in Phase 2 commercial scope ("bookkeeper uses own Anchor install with capability-trimmed UI")
- Visibility modes (idea 2) — support sessions are basically visibility-mode-on by default

### Industry references to research

- Privileged Access Management (PAM) products: CyberArk, BeyondTrust, HashiCorp Boundary
- Just-In-Time (JIT) access patterns for cloud (AWS IAM Access Analyzer, GCP IAM Conditional Access)
- HIPAA "minimum necessary" rule for support access to PHI
- SOC 2 CC6.1 (logical access controls including third-party support)

---

## Idea 4 — Sensitive Information Classification (the foundation data model)

### What the BDFL named

> "How to easily allow the user to hide what is sensitive information or classify what's sensitive information. There may be different levels of sensitive information. So PII is one kind. Financial information could be different. Photos. I'm sure there's information on the Internet about how to categorize."

### What this means

A **classification taxonomy** + a **runtime tag model** that lets data fields, records, and entire entity types be marked with their sensitivity class. Once data carries a class, downstream concerns (visibility modes, support delegation, audit retention, regulatory exports, encryption-at-rest tier, retention policy) all key off it consistently.

This is **the foundation that gates ideas 2 and 3** — without a classification model, redaction and support-trimming have to be hand-coded per field, which doesn't scale across blocks.

### Industry standards to anchor on (don't reinvent)

- **NIST SP 800-122** — Guide to Protecting the Confidentiality of Personally Identifiable Information. Defines PII categories (linked vs linkable, sensitivity tiers).
- **GDPR Article 9** — Special category data (health, biometric, sexual orientation, racial origin, political opinion, religious belief, trade-union membership, genetic).
- **HIPAA PHI** — Protected Health Information categories.
- **PCI DSS Cardholder Data** — primary account number, expiration, service code, cardholder name, sensitive authentication data.
- **ISO/IEC 27001 Information Classification** — standard 4-tier (Public / Internal / Confidential / Restricted) or 3-tier variants.
- **US Federal CUI program** — Controlled Unclassified Information, with categories like CUI//SP-PRVCY, CUI//SP-PCII, etc.
- **Microsoft Information Protection (MIP) sensitivity labels** — operational implementation reference.

A pragmatic Sunfish synthesis might be a **two-axis classification**:

- **Sensitivity tier** (axis 1): Public / Internal / Confidential / Restricted
- **Domain category** (axis 2): PII / Financial / Health / Payment-Card / Trade-Secret / Other

A field like "lease-holder SSN" would be `(Restricted, PII)`; "monthly rent amount" would be `(Confidential, Financial)`; "property address" would be `(Internal, PII)` (linkable but not directly identifying); "tenant logo" would be `(Public, Other)`.

### Use cases (drawn from BDFL's actual operation + medical analog)

- **Phase 2 commercial scope**: BDFL's property records have lease-holder SSN (Restricted/PII), bank account numbers (Restricted/Financial), property addresses (Internal/PII), monthly P&L (Confidential/Financial). Each downstream behavior (export to bookkeeper, screenshot for tax-advisor email, IRS export, support session, audit retention) needs to know which is which.
- **Medical-provider stress test**: patient name (Restricted/PII), diagnosis (Restricted/Health), today's-clinic-list (Confidential/Internal), provider's own credentials (Public/Internal). The same data model handles both domains.
- **Future regulated-SMB segment** (per ADR 0046 revisit triggers): healthcare, finance, government — each comes with classification mandates that map onto a common axis.

### Open questions for Stage 01 Discovery (when promoted)

- **Where does the tag live?** On the C# property (attribute: `[Sensitivity(Tier = Restricted, Category = PII)]`), on the schema (per ADR 0001 Schema Registry), as runtime metadata in the entity (`record.Sensitivity = ...`), all three? Multiple anchors trade off ergonomics vs. enforcement.
- **Is classification per-field or per-record?** Per-field is more granular but more verbose; per-record is simpler but loses precision (a `Lease` record has both `LeaseHolderSsn` (Restricted) and `MonthlyRent` (Confidential) — the record-level can't be both).
- **Who owns the policy mapping (sensitivity → behavior)?** Foundation ships the taxonomy; each consumer (visibility-mode, support-delegation, audit-retention, ADR 0049 audit, ADR 0021 reports) ships its own mapping table.
- **How do we handle inferred sensitivity?** A `Notes` text field is `Public` if it says "Property has a yellow door" but `Restricted` if it says "Tenant's SSN is 123-45-6789." Policy can't fully cover free-text. PII detection (regex / ML) is its own follow-up.
- **What's the migration story?** Existing entity types (in `blocks-tenant-admin`, `blocks-businesscases`, etc.) don't carry classification today. Default policy: missing classification → `Internal` + log for review? `Restricted` (most-strict default)? Block-by-block migration?
- **How does this interact with sentinel-tenants** (per the multi-tenancy convention intake)? `TenantId.Guest`, `TenantId.System` — does sentinel-tenant data have inherent classification (`Internal` regardless of field)?
- **Does this become an ADR?** Probably yes — classification is foundational and downstream features depend on its shape. Stage 02 Architecture decision is ADR-grade.

### Existing primitives to compose with

- ADR 0049 audit trail (audit records themselves carry sensitivity — probably `Confidential` or `Restricted` by default; influence retention)
- ADR 0021 reporting pipeline (export writers consume classification to redact / split / restrict)
- `Foundation.Catalog` / ADR 0007 bundle manifest (block-level classification declarations)
- `Foundation.LocalFirst` (encryption-at-rest tier could key off classification)
- ADR 0028 CRDT engine (some classifications might disable CRDT replication, e.g., Restricted/PII fields might be local-only)

### Industry references to research

- Microsoft Purview Information Protection (sensitivity labels + auto-classification)
- AWS Macie (PII discovery + classification)
- Open-source classification tools: Spirion (formerly Identity Finder), Apache Atlas
- The OWASP Data Classification Cheat Sheet
- Snowflake / Databricks data-tagging APIs (column-level classification)

---

## Cross-cutting themes worth surfacing

### Theme 1 — These compose with the multi-tenancy convention intake

The convention intake (`tenant-id-sentinel-pattern-intake-2026-04-28.md`) introduces `TenantSelection` for multi-tenant query scope and `TenantId.Guest` / `TenantId.System` sentinels. Sensitivity classification (idea 4) likely needs a parallel `SensitivityScope` value object for queries that say "give me audit records above Confidential" or "redact everything Restricted."

### Theme 2 — The capability graph is the unification point

Existing macaroon caveats can grow a `min_sensitivity` clause: a support user's macaroon says "you can read records but only at sensitivity ≤ Internal." The capability evaluator filters at query time. This is the cheapest place to enforce because it's already a chokepoint. (Alternative: enforce at render-time / serialization-time. More distributed. More gaps.)

### Theme 3 — All four ideas are relevant to Phase 2 commercial scope but not blocking it

Phase 2 (BDFL's property business) can ship without these features by hand-coding redaction rules where needed. But the medical-provider use case, the regulated-SMB segment (ADR 0046 revisit triggers), and any commercial customer post-BDFL needs these as platform features. **Decide whether to invest in the foundation now or defer.**

### Theme 4 — Idea 1 (startup layering) is independent and could ship sooner

Layered startup doesn't depend on classification; it's a refactor of how Anchor composes its services and config at app-launch. Could be its own ADR + small refactor wave, independent of the sensitivity work. Probably faster to discovery + design + ship. The other three (2, 3, 4) form a cluster that should be sequenced as 4 → 2 → 3.

---

## Recommended next steps (per idea)

| Idea | Stage 01 promotion priority | Approximate effort to discovery |
|---|---|---|
| **1 — Layered startup** | **High** if Phase 2 multi-actor delegation surfaces config-cascade pain points; **Medium** otherwise. Independent. | 1-2 sessions for Stage 01 discovery; 1 ADR likely. |
| **2 — Visibility modes** | Low until idea 4 lands (without classification, this is hand-coded per field). Bookkeeper screenshot use case is the natural pull. | 1 session for Stage 01 once 4 is foundational. |
| **3 — Support delegation** | Low until idea 4 lands AND we have a real customer asking for it. BDFL doesn't currently have third-party support relationships in Phase 2. | 1-2 sessions for Stage 01 once 4 is foundational. |
| **4 — Sensitivity classification** | **High** as the gating foundation. Even a v0 (PII / Financial / Other × Internal / Confidential / Restricted) is more useful than no model. | 2-3 sessions for Stage 01; ADR-grade outcome at Stage 02. |

**Sequencing recommendation:** 4 first (foundation), 1 in parallel (independent), then 2 + 3 once 4 lands. Don't promote any of the four to discovery until Phase 2 commercial work has stabilized — these are platform features for Phase 3+ unless a Phase 2 use case forces an earlier promotion (most likely candidate: bookkeeper screenshot redaction → forces idea 2 + a thin slice of idea 4).

---

## Pipeline variant routing

**Filed as:** This intake itself is `sunfish-gap-analysis` (capturing missing capabilities). Each idea, when promoted to Stage 01 Discovery, becomes its own intake with `sunfish-feature-change` variant (or `sunfish-api-change` if it requires breaking foundation contracts — likely for idea 4).

## Next stage

Hold at Stage 00 until any of the four ideas surfaces a forcing function that makes promotion-to-discovery the right move. The most likely forcing functions:

- BDFL takes a screenshot for bookkeeper and notices a value he doesn't want to share → **promotes idea 2** (and forces a thin slice of 4)
- BDFL adds a third-party MSP for his Mac troubleshooting → **promotes idea 3**
- Anchor's startup config grows complex enough that adding a new override site requires touching 5+ files → **promotes idea 1**
- A regulated-SMB prospect asks "can your audit records be retention-tagged by sensitivity class?" → **promotes idea 4**

When any of these fires, this brainstorm intake gets a `Promoted to Stage 01 Discovery (<idea>) on <date>` note + the new per-idea intake at `icm/01_discovery/output/`.
