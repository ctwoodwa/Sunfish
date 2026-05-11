# Sunfish Wayfinder — Configuration UX Discovery

**Stage:** 01 Discovery
**Pipeline:** `sunfish-gap-analysis` (exit: **Approved Gap**)
**Date:** 2026-05-01
**Author:** XO research session
**Status:** Approved Gap — CO approved 2026-05-01
**Companion plan:** `~/.claude/plans/sunfish-wayfinder-configuration-research.md` (UPF v1.2)
**Intake:** `icm/00_intake/output/2026-05-01_wayfinder-intake.md`
**Active workstream:** W#34 in `icm/_state/active-workstreams.md`

---

## 1. Executive Summary

Sunfish has eight scattered configuration layers but no unified UX surface. Each feature implementer derives "where do I put this setting, how does the user find it, how is the change recorded, who can change it, how is it audited" from scratch. Cross-platform OS settings (macOS / iOS / Android / Windows 11) have 13+ years of mature precedent — search-as-you-type, dual-surface UI+JSON, schema-driven forms, change audit, sync — that Sunfish should adopt rather than re-derive.

This discovery maps each layer to its current coverage, surveys cross-platform OS + pro-tool + SaaS UX patterns, identifies gaps, and queues 4 follow-on ADR intakes. The matrix is a *map*, not a *specification* — the actual Wayfinder system architecture, Helm composition, Atlas UI contract, and Standing Order type signature each become their own downstream ADRs.

**Naming locked**: **Wayfinder** (umbrella) + **Helm** (live-state pane) + **Atlas** (deep-config UI) + **Standing Order** (config-change record / event). Atlas-as-projection-of-Standing-Orders composes with **ADR 0049** audit trail (every Standing Order is an audit event by construction) and **ADR 0028** CRDT semantics (Standing Orders are append-only operations on a per-team or per-tenant log).

### Verdict table

| # | Layer | Coverage | Confidence | Recommended next step |
|---|---|---|---|---|
| 5.1 | User preferences (cosmetic, behavioral) | **Gap** | Medium | New ADR — Wayfinder system + Standing Order contract (bundled, event-sourcing-shaped) |
| 5.2 | Tenant configuration (locale, jurisdiction, multi-actor permissions) | **Partial** | Medium-High | ADR 0009 amendment — extend to tenant-config policy beyond flags/entitlements |
| 5.3 | Feature management (entitlements, editions, flags) | **Specified** | High | Reference only — ADR 0009 covers comprehensively |
| 5.4 | Capability declarations (what hardware/runtime affords) | **Specified** | High | Reference only — W#33 Mission Space + ADRs 0062 + 0063 cover the full envelope |
| 5.5 | Domain configuration (custom forms, taxonomies, dynamic schemas) | **Specified** | High | Reference only — ADRs 0055 + 0056 cover dynamic forms + versioned taxonomies |
| 5.6 | Integration configuration (payments / messaging / providers) | **Partial** | Medium-High | New ADR — Atlas integration-config UI surface composing on existing provider-neutrality contracts |
| 5.7 | Security configuration (MFA, attestation, audit policies) | **Gap** *(A4 spot-check confirmed)* | Medium | New ADR — tenant-configurable security posture (distinct from ADR 0043 OSS-project threat model) |
| 5.8 | Account / identity (profile, keys, recovery contacts) | **Partial** | Medium-High | New ADR — Helm composition (renders identity glance + recovery-contact UX) |

**Summary statistic**: 3 Specified + 3 Partial + 2 Gap. Of the 2 Gaps, both are net-new ADRs. Of the 3 Partials, 2 route to new ADRs (Atlas integration UI + Helm composition) and 1 to an amendment (ADR 0009 extension to tenant-config policy).

**Stage 1.5 hardening note**: §5.1 (Atlas surface) received a WCAG / a11y adversarial pass. Findings folded in: Atlas requires WCAG 2.2 AA conformance baseline, ARIA-conformance per cross-platform OS standards, keyboard-first navigation, and screen-reader compatibility (VoiceOver / TalkBack / Narrator).

---

## 2. Research Question

CO directive (paraphrased from 2026-05-01 brief): *"Research industry best practices for configuration management — how systems organize, validate, audit, version, and surface configuration to users. Investigate desktop OS (macOS / Windows / Linux), mobile (iOS / Android), and reduced-surface form factors (Watch / TV / IoT). Synthesize a unified configuration UX for Sunfish — common search, common navigation, common audit story across all eight Sunfish configuration layers. The deliverable is a map of patterns + gaps; downstream ADRs will specify the actual contracts."*

The matrix produced here is the canonical reference answering "for any Sunfish configuration concern, where does it live, who can change it, how is it surfaced, how is it audited?" It does not specify the Wayfinder system architecture, Helm composition, Atlas UI contract, or Standing Order type signature — those become downstream ADRs.

---

## 3. Method

### 3.1 Verdict-tag scheme

| Tag | Meaning | What's required |
|---|---|---|
| **Specified** | Predecessor (ADR or paper §) covers the layer substantively, including configuration semantics and (where applicable) UX surface | Citation + one-line gate definition + "reference only" recommendation |
| **Partial** | Predecessor exists for the data model or infrastructure layer; UX surface or policy specifics are missing | Citation of what IS covered + named gap + recommended new ADR or amendment |
| **Gap** | No current artifact covers the layer as a configuration concern | Industry prior-art (1–3 references) + sketch of contract shape + new ADR recommendation |

### 3.2 Per-layer §5 schema

Every layer's subsection follows: **Coverage tag** / **Confidence** / **Recommended next step** header, then **Gate definition** / **Examples** / **Current coverage** / **What's missing** / **Recommendation**. Word budget per layer: ~350w (Specified), ~600w (Partial), ~800w (Gap).

### 3.3 What's in scope

The eight Sunfish configuration layers (per intake §S3) plus the cross-cutting Wayfinder UX surfaces (Helm + Atlas) that span them. Industry prior-art surveys for cross-platform OS settings (Appendix A) and pro-tool / SaaS exemplars (Appendix B).

### 3.4 What's out of scope

- **Concrete Wayfinder system architecture** — type signatures, persistence, CRDT-merge semantics for Standing Orders go into a downstream ADR
- **Atlas UI contract specification** — the actual schema-to-form generator, search algorithm, page hierarchy go into a downstream ADR
- **Helm composition specification** — what widgets render, subscription mechanics, glance-pane refresh policy go into a downstream ADR
- **Per-feature configuration assignments** — assigning specific features to specific Atlas pages is per-block module work, not Wayfinder-level
- **Pricing-tier policy** — the matrix names commercial tier as a layer; the actual edition/SKU matrix is product/marketing work outside ICM

### 3.5 Anti-pattern guards (drafting time)

Every claim in §5 cites a paper §, ADR #, or external standard. Industry-prior-art is constrained to ~3 references per gap. Specified-tier layers stay light; Gap-tier layers go deep. WCAG / a11y adversarial pass applied to §5 Layer 1 (Atlas surface) and folded into §5.1 + §A.

---

## 4. Sunfish Substrate Recap

### 4.1 The four-name vocabulary

| Layer | Name | What it is | Visible to |
|---|---|---|---|
| System / umbrella | **Sunfish Wayfinder** | The configuration system as a whole | All audiences |
| Live-state pane | **Helm** | Glance pane: current Mission Envelope + sync state + active team + quick toggles | End users + admins |
| Deep configuration surface | **Atlas** | User-facing settings UI; navigable pages of structured configuration | End users + admins |
| Configuration-change record | **Standing Order** | Internal type/event representing a single configuration change | Engineers + audit |

### 4.2 Architecture: Atlas-as-projection-of-Standing-Orders

The Atlas is what users see. A Standing Order is what the system records. The user edits the Atlas → the system issues a Standing Order → the Standing Order goes through validation / audit / distribution → the Atlas's effective state updates.

This is event-sourcing-shaped:

- **Standing Orders** = append-only event stream (`StandingOrder.Issued`, `StandingOrder.Amended`, `StandingOrder.Rescinded`)
- **Atlas** = the materialized view (current effective config, projected from the ordered Standing Orders)

Composes naturally with:
- **ADR 0049 (audit trail)** — every Standing Order is an audit event by construction; configuration history is "free" (filtered Standing Orders log)
- **ADR 0028 (CRDT semantics)** — Standing Orders are append-only operations; conflict resolution between concurrently-issued Standing Orders is a CRDT merge

### 4.3 Composition with W#33 Mission Space

- **Mission Space** (W#33) = the *envelope* (dimensional matrix of what's available)
- **Wayfinder** (this research) = the *operational system* (configuration + live-state surface that operates within the envelope)
- **Helm** renders Mission Envelope output (live capability glance via ADR 0062 Mission Space Negotiation Protocol)
- **Atlas** is governed by Standing Orders within the Mission Space gates; install-UX integrates ADR 0063 Mission Space Requirements

Reads cleanly: *"In the Wayfinder, the Helm shows what your device can do right now (per its Mission Space) and lets you act on it; the Atlas is where you issue Standing Orders to configure how it operates."*

### 4.4 The eight configuration layers

Brief summary; each gets full §5 treatment below.

1. **User preferences** — cosmetic, behavioral; what most apps call "Settings" for end users
2. **Tenant configuration** — locale, jurisdiction, multi-actor permissions, branding, defaults
3. **Feature management** — flags, entitlements, editions, per-bundle features (ADR 0009)
4. **Capability declarations** — what hardware/runtime/version affords (W#33 Mission Space)
5. **Domain configuration** — custom forms, taxonomies, dynamic schemas (ADRs 0055 + 0056)
6. **Integration configuration** — payments, messaging, mesh-VPN, provider selection (ADRs 0013 + 0051 + 0052 + 0061)
7. **Security configuration** — MFA enrollment, attestation policies, audit-policy rules
8. **Account / identity** — profile, keys, recovery contacts (ADR 0046 + 0046-a1)

---

## 5. Per-layer evaluation

### 5.1 — User preferences (cosmetic, behavioral)

**Coverage tag:** Gap
**Confidence:** Medium
**Recommended next step:** New ADR — **Sunfish Wayfinder system + Standing Order contract** (bundled; event-sourcing system + event type are interdependent and should land together).

#### Gate definition

Per-user preferences governing cosmetic appearance, behavioral defaults, language, theme, and other non-tenant-shared, non-security configuration. The "Settings" pane that end users in macOS / iOS / Android / Windows expect to find.

#### Examples

- Dark / light / auto theme selection
- Default sort order in lists (oldest-first vs newest-first)
- Notification preferences (which events ping, which are silent)
- Time zone display override (when traveling)
- Density preference (compact vs comfortable list rendering)
- Language / locale (independent of tenant locale)
- Per-user keyboard shortcuts
- Sync-pause toggle ("offline mode" without affecting other team members)

#### Current coverage

**No current artifact covers user preferences as a Sunfish concern.** ADR 0009 (FeatureManagement) covers flags / entitlements / editions but explicitly distinguishes *Product features* from *Editions* — preferences are a fourth category not addressed. Paper §13.2 (lines 409–420) covers AP/CP visibility staleness thresholds — the closest precedent — but those are *system-decided* not *user-configurable*. ADR 0041 (dual-namespace components) frames the rich-vs-MVP UI degradation primitive but doesn't surface a user-facing preference choice.

#### What's missing (genuine gap)

- **Per-user preference data model** — where preferences live, how they sync, scope rules (per-device vs per-account)
- **Preferences UX surface** — how the user finds preferences, how they're categorized, how search works, how defaults are shown
- **Standing Order issuance for preference changes** — when a user changes a preference, what's the audit-trail record? (The architecture decision per §4.2)
- **Cross-device preference sync** — if a user has Anchor on laptop + iPad, do their preferences sync? Per-device or per-account?
- **Migration policy** — what happens to a preference whose feature is deactivated or whose form factor doesn't render it? (Hide / read-only / ignore)
- **Reset-to-default** — every OS Settings has it; Sunfish doesn't specify

#### Industry prior-art

- **Apple HIG — Settings** ([developer.apple.com/design/human-interface-guidelines/settings](https://developer.apple.com/design/human-interface-guidelines/settings/)) — preferences as a hierarchical drill-down on iOS, sidebar+content on macOS, with system-wide search across all preference panes
- **Material Design 3 — Settings** — search as primary discovery surface (Material You suggestion cards highlight recently-changed or contextually-relevant preferences)
- **Microsoft Fluent UI — Settings** (Windows 11 Settings) — sidebar + content with deep search; replaces legacy Control Panel entirely

#### Recommendation

New ADR (~0065 — name TBD; recommend "Sunfish Wayfinder + Standing Order contract"). Bundles the system architecture and event type because they're interdependent. Scope: per-user preference data model; per-device-vs-per-account sync rules; reset-to-default semantics; preference-change Standing Order shape; UX search-as-you-type contract; cross-device migration policy. Effort: large (~16–24h authoring + council review).

**WCAG / a11y note (Stage 1.5 hardening pass output, adversarially-reviewed 2026-05-01)**: this layer is the most-visible to end users. WCAG **2.2 AA** conformance is the Atlas baseline. **EN 301 549** (EU procurement; mandatory for Bridge hosted-node-as-SaaS in EU jurisdictions; 2024 revision references WCAG 2.2) is the cross-border conformance citation.

**Conformance baseline (cited by criterion):**

| Concern | WCAG 2.2 AA criteria | Atlas requirement |
|---|---|---|
| Keyboard navigation | 2.1.1 Keyboard (A) · 2.1.2 No Keyboard Trap (A) · 2.4.3 Focus Order (A) · 2.4.11 Focus Not Obscured Minimum (AA, *new*) | Tab/Shift-Tab in DOM-logical order; skip-link for sidebar; Esc closes modals; modal focus restored on close |
| Focus appearance | 2.4.7 Focus Visible (AA) · 2.4.13 Focus Appearance (AAA, *new*) | ≥2 CSS px AND ≥3:1 against adjacent colors AND area ≥ perimeter of 1px outline |
| Screen reader | 4.1.2 Name/Role/Value (A) · 4.1.3 Status Messages (AA) · 1.3.1 Info and Relationships (A) · 2.4.6 Headings and Labels (AA) | Form fields use `aria-describedby` for help text + `aria-invalid` on validation failure; Standing Order issuance announced via polite live region; search-result counts announced |
| Page structure | 2.4.2 Page Titled (A) · 3.1.1 Language of Page (A) · 3.1.2 Language of Parts (AA) | Each Atlas page has a programmatic title + `lang` attribute; per-user locale (Layer 1) and tenant locale (Layer 2) drive `lang` on dynamic content |
| Error prevention | 3.3.1 Error Identification (A) · 3.3.3 Error Suggestion (AA) · 3.3.4 Error Prevention (AA) · 3.3.7 Redundant Entry (A, *new*) · 3.3.8 Accessible Authentication (AA, *new*) | Inline + summary error messaging; reversible/checked/confirmed for security-policy + credential edits; redundant entry not required (autofill OK); cognitive-function tests forbidden in MFA UX (load-bearing for §5.7 + ~ADR 0068) |
| Target size | 2.5.5 Target Size AAA (44×44) · 2.5.8 Target Size Minimum (AA, *new*, 24×24) · 2.5.7 Dragging Movements (AA, *new*) | iOS HIG 44pt / Material 48dp / Web ≥24×24 CSS px; any drag interaction (e.g., notification reorder) provides single-pointer alternative |
| Motion + contrast | 2.3.3 Animation from Interactions (AAA) · 1.4.4 Resize Text (AA) · 1.4.10 Reflow (AA) · 1.4.11 Non-text Contrast (AA) · 1.4.1 Use of Color (A) | Respect OS prefs: `prefers-reduced-motion`, `prefers-color-scheme`, `prefers-contrast`, `prefers-reduced-transparency`, **`forced-colors`** (mandatory for Windows 11 High Contrast + Narrator); resize to 200% without horizontal scroll; text reflows at 320px viewport; color paired with shape/icon/label |
| Session timing | 2.2.1 Timing Adjustable (A) · 2.2.6 Timeouts (AAA) | Any session expiry / re-auth flow surfaces warning + extension; load-bearing for §5.7 audit-policy and security-session UX |

**Native platform a11y APIs (not just ARIA):** Anchor MAUI ships native UIs on each platform; Atlas surfaces must expose accessibility through the platform-native API, not only ARIA.

| Platform | Native a11y API | Mandatory assistive technologies |
|---|---|---|
| Windows | UI Automation (UIA) provider tree | Narrator, Magnifier, Voice Access, **forced-colors / High Contrast** |
| macOS | NSAccessibility (AppKit/SwiftUI) | VoiceOver (with rotor navigation), Full Keyboard Access |
| iOS / iPadOS | UIAccessibility (UIKit/SwiftUI) | VoiceOver, Switch Control, Voice Control, Dynamic Type (reflow to 310% per HIG) |
| visionOS | UIAccessibility (SwiftUI 3D) | VoiceOver, Pointer Control, Dwell Control, Switch Control; gaze-only + voice-only interaction modes |
| Android | AccessibilityNodeInfo + Compose accessibility | TalkBack, Switch Access, Voice Access; `contentDescription` on non-text controls; 48dp touch target (exceeds WCAG 2.5.8) |
| Web (Bridge admin) | WAI-ARIA 1.2 + DOM | NVDA, JAWS, VoiceOver, Narrator; `forced-colors` media query |

**Atlas-specific accessibility concerns:**

- **Search-as-you-type a11y** — search results announced via live region (count + first match); arrow keys navigate results; deep-link result moves focus to target preference (2.4.3, 3.2.1).
- **Dual-surface (form ↔ JSON) a11y** — JSON-edit view (per VSCode-style toggle, B.1) must be screen-reader navigable OR expose an "accessible alternative" to the form view. Monaco/CodeMirror have known SR gaps; downstream ~ADR 0065 must specify the accessible-alternative contract.
- **Standing Order diff-preview a11y** — diff rendering not color-only (1.4.1); changes announced via live region (4.1.3); per Stripe-pattern (B.3) but accessible.
- **Reset-to-default + destructive actions** — confirmation dialogs have accessible names, focus trap, restored focus on close.
- **Drag operations** — any reorder/drag UX (e.g., notification-priority reorder) provides single-pointer alternative per 2.5.7.

These requirements propagate into ~ADR 0065 (Wayfinder system + Standing Order contract), ~ADR 0066 (Helm + identity Atlas), ~ADR 0067 (Atlas integration-config), ~ADR 0068 (security policy — MFA UX is *directly* in 3.3.8 scope), and the ADR 0009 amendment (Bridge admin feature-flag UX inherits Atlas a11y baseline). See §7.4 for council-review subagent dispatch.

---

### 5.2 — Tenant configuration (locale, jurisdiction, multi-actor permissions)

**Coverage tag:** Partial
**Confidence:** Medium-High
**Recommended next step:** ADR 0009 amendment — extend FeatureManagement to tenant-config policy beyond flags/entitlements/editions.

#### Gate definition

Per-tenant administrative configuration governing locale / jurisdiction / multi-actor permissions matrix / branding / default policies. Distinct from user preferences (which are per-user) and from feature management (which is per-bundle/edition).

#### Examples

- Tenant default locale (en-US, en-GB, es-MX, etc.)
- Tenant jurisdiction declaration (CA / NY / UT / EU-DE / ...)
- Multi-actor permissions matrix (who can approve work orders > $X; who can view tenant SSNs; etc.)
- Tenant branding (logo, primary color, organization name)
- Default tax/currency for the tenant's locale
- Tenant audit-retention policy (how long Standing Orders + audit events are retained)

#### Current coverage

- **ADR 0009** (Foundation.FeatureManagement, lines 13–18, 32, 71–72) covers the *infrastructure* for per-tenant entitlements + edition mappings: *"Bundle manifest (ADR 0007) becomes the authoring source for entitlements — featureDefaults and editionMappings map directly onto FeatureSpec.DefaultValue and IEntitlementResolver rules."*
- **ADR 0032** (Multi-team Anchor with subkey derivation, lines 110–119) covers per-team identity isolation: *"the install derives a per-team subkey via HKDF(root_private, 'sunfish-team-subkey-v1:' + team_id)."*
- **ADR 0046** + **0046-a1** cover role-key wrapping per tenant; tenant-scoped key handling
- **ADR 0007** (bundle manifest schema) covers per-bundle config defaults

What's *covered*: per-tenant flags, entitlements, edition mappings, key handling, identity isolation. The *infrastructure* is solid.

#### What's missing

- **Tenant-config UX surface** — how a tenant admin finds and edits these settings (locale, jurisdiction, permissions matrix). No Atlas equivalent exists.
- **Multi-actor permissions matrix specification** — Phase 2 commercial scope flags this as needed (BDFL spouse-recovery, vendor multi-actor delegation) but no current ADR specifies the matrix shape, role-binding, or UX
- **Tenant-policy language** — beyond flag toggles, tenants need *policy* (e.g., "all signatures require notarization in CA jurisdiction"); ADR 0009 doesn't address this
- **Tenant-config Standing Order audit** — per ADR 0049 the audit trail exists, but Standing Orders for tenant-config changes don't have a defined shape

#### Recommendation

ADR 0009 amendment (recommended A1 amendment). Scope: extend FeatureManagement's entitlement/edition concept to *tenant-config policy* — a fifth category alongside flags/features/entitlements/editions. Specify Standing Order shape for tenant-config changes (composing on ADR 0049). Cross-references the new Wayfinder ADR (~0065) for the Atlas UI surface. Effort: medium (~8–12h authoring + council review).

---

### 5.3 — Feature management (entitlements, editions, flags)

**Coverage tag:** Specified
**Confidence:** High
**Recommended next step:** Reference only — ADR 0009 covers the surface comprehensively.

#### Gate definition

Configuration of which features are enabled / disabled per tenant, per edition (lite / standard / enterprise), per technical-flag (rollout / kill-switch). Distinct from user preferences (per-user) and tenant configuration (per-tenant operational policy).

#### Examples

- Enable/disable a feature flag for a beta rollout
- Set entitlement: "this tenant has signatures with notarization (enterprise edition)"
- Configure edition: "this bundle ships in lite / standard / enterprise"
- Kill-switch a feature with known production issue

#### Current coverage

ADR 0009 (Foundation.FeatureManagement) covers this layer comprehensively. Lines 13–18 carry the four-concept separation: *"Technical flags — runtime booleans / variants, often for rollouts or kill-switches... Product features — named capabilities the product exposes... Entitlements — what a tenant is allowed to use... Editions / tiers — named product configurations (lite, standard, enterprise)."* `IFeatureCatalog` declares known features; `IFeatureProvider` is the OpenFeature-style seam; `IEntitlementResolver` computes entitlements from tenant/edition/bundles/modules; `IFeatureEvaluator` is the top-level read with explicit resolution order. Lines 71–72 ground the bundle-manifest authoring source. UX surface for Bridge admin is deferred to a P1 follow-up but the data model is fully specified.

#### Recommendation

No new artifact. The Atlas surface for feature management composes on ADR 0009's existing infrastructure. Reference this layer as "fully specified — Atlas just renders the existing data model."

---

### 5.4 — Capability declarations (what hardware / runtime / version affords)

**Coverage tag:** Specified
**Confidence:** High
**Recommended next step:** Reference only — W#33 Mission Space + ADRs 0062 + 0063 cover the full envelope.

#### Gate definition

Declarations of what features can run given hardware (CPU / GPU / RAM / disk), form factor (laptop / tablet / watch / IoT), runtime version, network class, jurisdiction, and commercial tier. Read by the Wayfinder Helm at runtime; surfaced to the user as "what your device can do."

#### Examples

- Mission Envelope reports: "this device supports HSM-backed key storage (TPM 2.0 detected); mesh-VPN unavailable (no kernel WireGuard)"
- ADR 0063 install-UX page renders: "Required: 8GB RAM, 4-core CPU. Your device meets requirements."
- Helm pane: "Currently online; sync healthy; 2 features hidden because regulatory-jurisdiction = EU"

#### Current coverage

- **W#33 Mission Space Matrix** (`icm/01_discovery/output/2026-04-30_mission-space-matrix.md`, 7,482 words) — the canonical dimensional matrix
- **ADR 0062 — Mission Space Negotiation Protocol** (runtime layer; merged 2026-04-30 PR #406) — defines `MissionEnvelopeProvider` coordinator and per-feature `ICapabilityGate<TCapability>` gates with uniform telemetry and re-evaluation semantics
- **ADR 0063 — Mission Space Requirements** (install-UX layer; merged PR #413) — defines declarative `MinimumSpec` schema (10 per-dimension sub-schemas) bundled into `BusinessCaseBundleManifest`, rendered Steam-style at install time

The Helm consumes ADR 0062's runtime envelope; the Atlas's install-UX page consumes ADR 0063's MinimumSpec.

#### Recommendation

No new artifact. The Helm and Atlas install-UX both compose on existing W#33 outputs. Reference this layer as "fully specified — Helm + Atlas install-UX render the existing Mission Space substrate."

---

### 5.5 — Domain configuration (custom forms, taxonomies, dynamic schemas)

**Coverage tag:** Specified
**Confidence:** High
**Recommended next step:** Reference only — ADRs 0055 + 0056 cover dynamic forms + versioned taxonomies.

#### Gate definition

Admin-defined data shapes — custom inspection categories, jurisdiction-specific receipt fields, tenant-extended classification taxonomies, dynamic-form schema definitions. Distinct from user preferences (per-user) and tenant configuration (operational policy) — domain configuration shapes the *data model* itself.

#### Examples

- A property-management tenant adds a custom "Solar Panel" equipment class to the Equipment taxonomy
- A jurisdiction extension adds CA-specific fields to the Receipt schema for tax compliance
- A tenant defines a custom Inspection Deficiency category not in Sunfish defaults
- A vendor specialty taxonomy is extended with niche services per tenant

#### Current coverage

- **ADR 0055** (Dynamic Forms Substrate) — `ISchemaRegistry` accepting `SchemaDefinition` with `JsonSchema` (JSON Schema 2020-12) + `SunfishOverlay` (UI hints, sections, rules); JSONB storage; subform/section-level permissions
- **ADR 0056** (Foundation.Taxonomy substrate) — versioned taxonomies with `ITaxonomyRegistry`, lineage tracking, authoritative-vs-marketplace governance regimes; five starter taxonomies shipped in v1 (Signature Scopes, Equipment Classes, Vendor Specialties, Inspection Deficiency Categories, Contact Use Contexts); cross-tenant analytics via version-pinned `TaxonomyClassification` references

The Atlas surface for domain configuration composes on these — admin extends a taxonomy → Standing Order issued → ADR 0056's `ITaxonomyRegistry` materializes the new node.

#### Recommendation

No new artifact. The Atlas surface for domain configuration renders ADRs 0055 + 0056's existing infrastructure with form-builder UX. Reference this layer as "fully specified — Atlas builds on substrate."

---

### 5.6 — Integration configuration (payments / messaging / mesh-VPN / providers)

**Coverage tag:** Partial
**Confidence:** Medium-High
**Recommended next step:** New ADR — Atlas integration-config UI surface composing on existing provider-neutrality contracts.

#### Gate definition

Per-tenant configuration of integration providers — which payment gateway (Stripe / Square / etc. via providers-* adapters), which email/SMS provider (Postmark / SendGrid / Twilio), which mesh-VPN provider (Headscale / Tailscale / NetBird), which captcha provider (reCAPTCHA / hCaptcha). Composes on existing provider-neutrality contracts.

#### Examples

- Tenant selects payment provider: "use providers-stripe for this tenant; providers-square for that one"
- Tenant configures email provider: "use providers-postmark for transactional; providers-sendgrid for marketing"
- Tenant configures mesh-VPN: "providers-mesh-headscale (self-hosted) for org A; providers-mesh-netbird (managed) for org B"
- Tenant configures rate limits, API keys, callback URLs per provider

#### Current coverage

- **ADR 0013** (Provider neutrality, lines 58–61) — *"Domain modules never reference vendor SDKs directly. Domain concepts are Sunfish-modeled, not vendor-mirrored."*
- **ADR 0051** (Foundation.Integrations.Payments) — payment-provider adapter contract (`IPaymentGateway` etc.)
- **ADR 0052** (Bidirectional Messaging Substrate) — messaging-provider adapter contract; PR #273 shipped `Sunfish.Foundation.Integrations.Messaging`
- **ADR 0061** (Three-Tier Peer Transport) — mesh-VPN provider abstraction (`MeshVpnPeerTransport`); license-screening posture (SSPL/BSL excluded)

What's *covered*: the provider-neutrality contract, per-vendor adapter shape, license-screening rules. The *infrastructure* is solid.

#### What's missing

- **Integration-config Atlas UI surface** — how an admin selects a provider, configures rate limits / API keys / callback URLs, validates the configuration before activation
- **Provider-credential storage** — API keys are sensitive; how does Atlas capture them, how are they encrypted (compose on ADR 0046-A2's `EncryptedField`), how does the tenant rotate them
- **Multi-provider per-category selection** — can a tenant use providers-postmark for transactional and providers-sendgrid for marketing simultaneously? The infrastructure supports it; the UX doesn't specify it
- **Provider capability negotiation** — when a tenant selects providers-square (payments), which Sunfish features become available / unavailable? Cross-references W#33 Mission Space (capability dimension) but no Atlas surface specifies it

#### Recommendation

New ADR — **Atlas integration-config UI surface**. Scope: provider-selection UX; credential storage (composing on `EncryptedField` from ADR 0046-A2); multi-provider per-category selection; capability-negotiation surface (Mission Space integration); validation before activation. Cross-references existing provider-neutrality contracts (ADRs 0013 + 0051 + 0052 + 0061). Effort: medium-large (~12–18h authoring + council review).

---

### 5.7 — Security configuration (MFA, attestation, audit policies)

**Coverage tag:** Gap *(A4 spot-check confirmed)*
**Confidence:** Medium
**Recommended next step:** New ADR — tenant-configurable security posture (distinct from ADR 0043 OSS-project threat model).

> **Reader caution**: this layer is a load-bearing security concern. Downstream ADR drafts MUST engage a security-engineering perspective (and where MFA / attestation policy intersects regulatory compliance, general counsel) before specifying enforcement behavior.

#### Gate definition

Tenant-configurable security posture — required MFA factors, device-attestation requirements, audit-log retention policy, role-key rotation cadence, recovery-contact enforcement. Distinct from the OSS-project threat model (ADR 0043) which addresses the merge-path supply chain.

#### Examples

- Tenant policy: "all admin actions require hardware-keyed MFA"
- Tenant policy: "audit logs retained for 7 years (HIPAA compliance)"
- Tenant policy: "role-keys rotate every 90 days"
- Tenant policy: "all federation peers must present device attestation"
- Tenant policy: "recovery contacts must be enrolled within 30 days of account creation"

#### Current coverage

The A4 spot-check (Phase 2 Wayfinder meta-plan) found a **citation category mismatch**: ADR 0043 (Unified Threat Model) covers the OSS-project threat model — *"The merge-path surface composes as follows: [Subagent] → [CI runs minimal checks] → [Merge happens] OR [bypass merge]"* — but does NOT cover tenant-configurable security posture.

Adjacent but not-load-bearing:
- ADR 0046 + 0046-a1 cover key-rotation infrastructure (the *mechanism*) but not policy
- ADR 0049 (audit trail) covers the audit infrastructure but not retention policy
- ADR 0061 (transport tiers) covers attestation at federation time but not tenant-policy
- ADR 0058 (vendor onboarding) covers security posture for *that* domain but not cross-cutting

The infrastructure for security primitives exists; what's missing is the **policy layer** + UX surface.

#### What's missing (genuine gap)

- **Security-policy data model** — what shape does a tenant security policy take? (Map of policy-name → enforcement-rule? Decision-tree? OPA / Rego?)
- **MFA enrollment policy specification** — required factors per role; grace period for enrollment; recovery flow
- **Attestation-requirement specification** — when device attestation is mandatory vs optional; what counts as "attested"
- **Audit-policy specification** — retention windows; export rights; right-to-be-forgotten interplay with audit immutability (ADR 0049)
- **Role-key rotation policy** — cadence; trigger conditions (compromise indicators); grace period
- **Recovery-contact policy** — enrollment deadline; minimum count; verification cadence
- **Atlas UI surface** — how an admin sets these policies; how they're surfaced to end users (e.g., "your team requires MFA enrollment within 30 days")
- **Standing Order shape for security-policy changes** — these are higher-stakes than user preferences; require additional approval (multi-actor delegation per Phase 2 commercial scope)

#### Industry prior-art

- **Apple Identity / Managed Apple ID + DEP** — enterprise MFA enrollment policy, device attestation via T2/Apple Silicon Secure Enclave; admin configures via Apple Business Manager
- **Microsoft Entra ID Conditional Access** — policy-based access control with MFA, device-trust, location signals; Azure portal UX is the industry reference for tenant-security-policy editing
- **Okta Policy Framework** — tenant-configurable policies (sign-on, password, MFA enrollment, session); per-policy + per-application binding
- **Open Policy Agent (OPA) / Rego** — general-purpose policy language; could ground the policy data model

#### Recommendation

New ADR — **Tenant-configurable security policy + Atlas surface**. Scope: security-policy data model (recommend: typed-DSL composing OPA/Rego patterns, not free-form rules); MFA enrollment policy; attestation-requirement specification; audit-policy specification (with ADR 0049 immutability boundary); role-key rotation cadence; recovery-contact policy; Atlas UI surface (with mandatory dual-control / multi-actor approval for security-policy changes); Standing Order shape (with elevated audit emission). Cross-references ADR 0046 (key handling), ADR 0049 (audit), ADR 0058 (vendor onboarding security precedent). Effort: large (~18–24h authoring + extended council review including security-engineering perspective).

---

### 5.8 — Account / identity (profile, keys, recovery contacts)

**Coverage tag:** Partial
**Confidence:** Medium-High
**Recommended next step:** New ADR — Helm composition (renders identity glance + recovery-contact UX).

#### Gate definition

Per-account configuration of profile (name, avatar, contact info), cryptographic keys (root key, role keys, historical keys), recovery contacts (spouse / co-owner / trustee per Phase 2 spouse-recovery scope), and per-team subkeys (ADR 0032).

#### Examples

- User edits profile name + avatar
- User initiates key rotation (compromise response)
- User adds spouse as recovery contact (ADR 0046 spouse-recovery scope)
- User reviews historical keys (ADR 0046-a1 historical-keys projection)
- User switches active team (ADR 0032 multi-team Anchor)

#### Current coverage

- **ADR 0046** (Encrypted Field + IFieldDecryptor + spouse-recovery) covers key handling, role-key wrapping, recovery-key distribution. Lines (per W#33 spot-check): *"Keys are wrapped with each qualifying member's public key (asymmetric encryption). Wrapped key bundles are distributed as administrative events in the log... Each node decrypts its role key bundle using its private key and stores keys in the OS keystore."*
- **ADR 0046-a1** (historical-keys projection) covers signature survival under operator-key rotation
- **ADR 0032** (Multi-team Anchor) covers per-team subkey derivation
- **W#32** (Foundation.Recovery field-encryption substrate, built 2026-04-30) ships `EncryptedField` + `IFieldDecryptor`
- **ADR 0049** (audit) covers identity-change audit emission

What's *covered*: cryptographic infrastructure, key-rotation mechanics, audit emission. The *infrastructure* is solid.

#### What's missing

- **Identity glance UX (Helm)** — how the user sees their current identity (active team, current role, key fingerprint, recovery-status) at a glance — composing on ADR 0036 sync states
- **Recovery-contact UX** — how a user enrolls / removes / verifies a recovery contact; how the spouse-recovery flow is surfaced
- **Key-rotation UX** — when does a user trigger rotation; what's the UX during the rotation window; how is the user warned about pending compromised-key alerts
- **Historical-keys browse** — UX for inspecting which old keys are still valid for verifying historical signatures (per 0046-a1)
- **Profile-edit Atlas surface** — name / avatar / contact info changes; Standing Order shape

#### Recommendation

New ADR — **Helm composition + identity Atlas surface**. Scope: Helm widgets (identity-glance, sync-state, active-team switcher per ADR 0032); identity Atlas surface (profile edit, key rotation flow, recovery-contact management, historical-keys browse); Standing Order shapes for identity changes (with multi-actor approval where appropriate per ADR 0046 spouse-recovery semantics). Cross-references ADRs 0032 + 0036 + 0046 + 0049. Effort: medium-large (~12–18h authoring + council review).

---

## 6. Synthesis — recommended follow-on intakes

The matrix surfaces 4 follow-on intakes per meta-plan §13.2 recommendation:

### 6.1 — New ADR ~0065: Sunfish Wayfinder system + Standing Order contract (bundled)

**Predecessor:** none clean. Adjacent: ADR 0009 (FeatureManagement — provides the "fifth concept" alongside flags/features/entitlements/editions); ADR 0049 (audit — Standing Orders compose as audit events); ADR 0028 (CRDT — Standing Orders are append-only operations).

**Why net-new + bundled**: the system architecture and the event type are interdependent. Defining the Standing Order shape requires the Wayfinder system context (who issues, who validates, who consumes). Defining the Wayfinder system requires the Standing Order shape (what events flow). Splitting them creates a chicken-and-egg sequencing problem.

**Effort**: large (~16–24h authoring + council review). Includes WCAG / a11y conformance specification per Stage 1.5 hardening pass output.

**Priority**: highest of the new ADRs because it grounds Layer 1 (User preferences — the most-visible end-user surface) and the Standing Order contract that all 8 layers consume.

### 6.2 — New ADR ~0066: Helm composition + identity Atlas surface

**Predecessor:** Layer 8 (account/identity) — ADR 0046 / 0046-a1 / 0032 / 0036; W#33 Mission Space outputs (ADR 0062 negotiation protocol provides Helm's runtime substrate).

**Why net-new**: Helm composition is its own surface (live-state pane vs deep-config Atlas); identity Atlas needs explicit UX specification (recovery-contact enrollment, key-rotation flow, historical-keys browse).

**Effort**: medium-large (~12–18h).

### 6.3 — New ADR ~0067: Atlas integration-config UI surface

**Predecessor:** Layer 6 (integration configuration) — ADRs 0013 + 0051 + 0052 + 0061; ADR 0046-A2 (`EncryptedField` for credential storage).

**Why net-new**: provider-selection UX, credential capture, multi-provider per-category selection, capability-negotiation surface — all unaddressed cross-cuttingly.

**Effort**: medium-large (~12–18h).

### 6.4 — New ADR ~0068: Tenant-configurable security policy + Atlas surface

**Predecessor:** Layer 7 (security configuration) — A4-confirmed gap. Adjacent: ADRs 0046 / 0049 / 0058. Industry prior-art: Apple Managed Apple ID, Microsoft Entra ID Conditional Access, Okta Policy Framework, OPA/Rego.

**Why net-new**: policy layer + UX surface unaddressed; A4 spot-check confirmed citation category mismatch with ADR 0043.

**Effort**: large (~18–24h) including security-engineering council review.

**Priority**: highest commercial priority — security-policy gaps are launch-blockers for regulated-industry tenants (HIPAA, PCI-DSS, SOC 2).

### 6.5 — ADR 0009 amendment: extend FeatureManagement to tenant-config policy (5th concept)

**Predecessor:** ADR 0009 (clean amendment slot).

**Scope**: extend FeatureManagement's four-concept separation (flags/features/entitlements/editions) with a fifth — *tenant-config policy*. Specify Standing Order shape for tenant-config changes.

**Effort**: medium (~8–12h).

### 6.6 — Out-of-scope (track-as-deferred)

- Concrete Wayfinder system architecture (type signatures, persistence, CRDT-merge semantics) — deferred to ~ADR 0065
- Atlas UI contract specification (schema-to-form generator, search algorithm, page hierarchy) — deferred to ~ADR 0065
- Per-feature configuration assignments (which features go on which Atlas pages) — per-block module work, not Wayfinder-level
- Pricing-tier policy (edition matrix, trial/preview lifecycle) — product/marketing work outside ICM
- Form-factor amendment to ADR 0048 for cross-form-factor Atlas rendering — flagged but recommend defer to a per-form-factor surge driven by W#23 iOS field-capture concrete needs

---

## 7. Implementation Guidance

### 7.1 Routing recommendation

| Follow-on | Routing | Rationale |
|---|---|---|
| Wayfinder system + Standing Order contract | New ADR ~0065 (bundled) | Interdependent; cannot be split |
| Helm composition + identity Atlas | New ADR ~0066 | Distinct UX surface (live-state vs deep-config) |
| Atlas integration-config UI | New ADR ~0067 | Provider-selection cross-cutting; provider-neutrality predecessors don't address UX |
| Tenant-configurable security policy | New ADR ~0068 | Genuine A4-confirmed gap; cannot amend ADR 0043 (category mismatch) |
| Tenant-config policy (5th concept) | ADR 0009 amendment | Clean amendment slot |

### 7.2 Sequencing recommendation

Phase 4 will produce intake stubs for the 5 follow-ons. Recommended authoring sequence:

1. **~ADR 0065 first** (Wayfinder + Standing Order). Foundational; everything else consumes the Standing Order contract and the Wayfinder system frame.
2. **ADR 0009 amendment second**. Small scope; resolves Layer 2 partial coverage; unblocks tenant-config Standing Order shape.
3. **~ADR 0066 third** (Helm + identity Atlas). Depends on ~0065's system frame; consumes ADR 0062 for runtime substrate.
4. **~ADR 0067 fourth** (Atlas integration-config). Depends on ~0065's Atlas UI contract.
5. **~ADR 0068 fifth** (security policy). Independent architecturally but needs security-engineering council; can run in parallel from any point.

### 7.3 Cross-workstream impact

- **W#22 Leasing Pipeline** — Layer 7 (security) ~ADR 0068 unblocks Phase 6 compliance half (currently deferred); Layer 6 (integration) ~ADR 0067 informs payment-provider config UX
- **W#23 iOS Field-Capture** — Layer 4 (capability declarations) consumed via existing W#33 + ADRs 0062 / 0063; Atlas form-factor adaptation deferred per §6.6
- **W#28 Public Listings** — Layer 6 (integration) ~ADR 0067 informs CAPTCHA-provider config; Layer 2 (tenant-config) amendment informs jurisdiction-aware listing rules
- **W#29 Owner Web Cockpit** — adjacent UI surface; ~ADR 0066 (Helm + identity Atlas) needs explicit relationship clarification with the Cockpit aggregate dashboard. Recommend Phase 4 synthesis intake stub names "Cockpit-vs-Wayfinder boundary" as a sub-question to address during authoring.
- **W#31 Foundation.Taxonomy** — Layer 5 fully composes; Atlas surface for taxonomy-extension UX is a downstream W#31 follow-on
- **W#33 Mission Space** — Wayfinder operates within the Mission Space envelope; ADRs 0062 + 0063 provide runtime + install-UX substrates

### 7.4 Council review posture

Per `feedback_decision_discipline.md`: **pre-merge council canonical** for all 5 follow-on ADR drafts. Cohort metric is now 11-of-11 substrate amendments needing council fixes (per recent W#33 follow-on shipping log).

For ~ADR 0068 (security policy) specifically: in addition to the standard adversarial council, dispatch a **security-engineering** perspective subagent. Where MFA / attestation policy intersects regulatory compliance (HIPAA / PCI-DSS / SOC 2), engage general counsel. **Also dispatch a WCAG / a11y subagent** — WCAG 2.2 AA criterion 3.3.8 (Accessible Authentication, *new*) is *directly* in the MFA/attestation UX scope; cognitive-function tests (typing OTP from memory, image-puzzle CAPTCHA) are non-conforming without accessible alternative. EN 301 549 procurement risk for Bridge EU tenants if missed.

For ~ADR 0065 (Wayfinder + Standing Order), ~ADR 0066 (Helm + identity Atlas), ~ADR 0067 (Atlas integration-config — credential-capture forms hit 3.3.7 + 3.3.8 + sensitive-data masking a11y; diff-preview a11y per Stripe-pattern), and the **ADR 0009 amendment** (Bridge admin feature-flag UX inherits Atlas a11y baseline): dispatch a **WCAG / a11y** perspective subagent (precedent set by Stage 1.5 hardening pass on this discovery's §5.1).

### 7.5 Pipeline closure

Per the gap-analysis pipeline contract (`icm/pipelines/sunfish-gap-analysis/routing.md`), this discovery is sufficient closure under the **"Approved Gap"** exit pattern. No Stage-02 architecture pass is required *for the matrix itself*. Each follow-on intake in §6 will run its own ICM pipeline (`sunfish-feature-change` for new ADRs; `sunfish-api-change` for ADR 0009 amendment).

Pipeline closes when CO records a final "Approved Gap" decision in this doc's frontmatter Status field, after Phase 4 (synthesis intake stubs) and Phase 5 (handoff + active-workstreams ledger flip from `building` → `built`).

---

## Appendix A — Cross-platform OS settings UX survey

### A.1 — macOS Settings (Ventura redesign, 2022+)

**Pattern**: sidebar + content (replacing the legacy System Preferences icon-grid model). Top-level search across all panes. Each pane is a structured form with native controls. System-wide preferences sync via iCloud (per Apple Account); per-app preferences live in `~/Library/Preferences/<bundle-id>.plist` (defaults system) and are typically edited through each app's own Settings/Preferences pane.

**Strengths**: search is excellent; live-update preview (e.g., changing wallpaper updates immediately).

**Accessibility**: macOS native UIs use **NSAccessibility (AppKit/SwiftUI)** — not ARIA. Atlas on Anchor MAUI macOS must surface a11y via native `AccessibilityElement` properties; Full Keyboard Access mode + VoiceOver rotor navigation must work; Reduce Motion / Increase Contrast OS prefs must be honored.

**Sunfish takeaway**: the sidebar+content shape is the canonical desktop pattern; Atlas on Anchor (MAUI macOS per ADR 0048) should match. Search-as-you-type is non-negotiable for the 8-layer surface.

### A.2 — iOS / iPadOS Settings

**Pattern**: hierarchical drill-down with breadcrumb back. System Settings.app for system-wide; per-app Settings panes accessible *inside* each app (or in some cases via the system Settings.app under the app's name — the "Settings bundle" pattern). Search is global. iPadOS adds a sidebar+content split for landscape orientation, blending iOS-drill-down with macOS-sidebar.

**Strengths**: drill-down is finger-friendly; per-app duality handles both system-level (notifications, location) and app-internal (themes, sort order) concerns.

**Accessibility**: native API is **UIAccessibility (UIKit/SwiftUI)**. VoiceOver + Switch Control + Voice Control + Dynamic Type (reflow to 310% per HIG) all load-bearing. iPadOS Pointer support requires 28pt minimum hit targets per HIG. Atlas drill-down must expose `accessibilityTraits` + proper heading hierarchy for VoiceOver rotor.

**Sunfish takeaway**: for W#23 iOS Field-Capture (paired iOS app), Atlas UX must follow iOS drill-down; for Anchor MAUI on iPad, follow iPadOS sidebar+content. Cross-platform Atlas must adapt — same Standing Order data, different UI shape per form factor.

### A.3 — Android Settings (Material Design 3)

**Pattern**: sidebar + content on tablets / large screens (Material 3 adaptive layouts); drill-down on phones. Search across all settings. **Suggestion cards** at the top — Material You's signature pattern — surface contextually-relevant or recently-changed settings. Per-app Settings via PreferenceFragment / SharedPreferences.

**Strengths**: suggestion cards solve discoverability; Material You theming respects per-user preferences (color, font).

**Accessibility**: native API is **AccessibilityNodeInfo + Compose accessibility**. TalkBack + Switch Access + Voice Access load-bearing. `contentDescription` mandatory on non-text controls; Material 3 chips/cards must declare `Role.Button`; minimum 48dp touch target (Material) exceeds WCAG 2.5.8.

**Sunfish takeaway**: suggestion cards are worth adopting for the Atlas top-level page — surface "recently changed Standing Orders" + "settings you might want to review (e.g., MFA enrollment grace period expiring)." Material 3 is the design-system reference for Android adapter.

### A.4 — Windows 11 Settings (Microsoft Fluent UI / Fluent 2)

**Pattern**: sidebar + content. Replaces legacy Control Panel almost entirely. Deep search. Per-feature pages (System / Network / Personalization / Accounts / Time & Language / Gaming / Accessibility / Privacy & Security / Windows Update). Per-user preferences sync via Microsoft Account; tenant-config (in enterprise contexts) syncs via Azure AD / Entra ID Group Policy.

**Strengths**: clear top-level categorization; search is comprehensive.

**Accessibility**: native API is **UI Automation (UIA)** provider tree — not ARIA. Narrator + Magnifier + Voice Access load-bearing. **Windows High Contrast / `forced-colors`** is mandatory for any Atlas surface on Windows; failure to honor it is a launch-blocker for EU/UK enterprise procurement (EN 301 549).

**Sunfish takeaway**: Fluent UI 2 is the design-system reference for Windows adapter; Anchor MAUI on Windows should match. The 8-category top-level organization on Windows 11 (System / Network / Personalization / etc.) is a useful precedent for Atlas's top-level taxonomy of the 8 layers.

### A.5 — VisionOS Settings

**Pattern**: hand- and eye-driven; settings appear as floating panes in 3D space. Drill-down preserved from iOS heritage. Reduced surface compared to macOS/iOS but more than WatchOS.

**Strengths**: spatial computing brings settings near the user's gaze; voice input augments. Less mature than iOS/macOS but evolving fast.

**Accessibility**: native API is **UIAccessibility (SwiftUI 3D)**. VoiceOver + Pointer Control + Dwell Control + Switch Control are visionOS a11y APIs; spatial UIs must support gaze-only and voice-only interaction modes. **Reduce Motion is critical** — vestibular triggers in 3D have higher impact than 2D motion.

**Sunfish takeaway**: future Anchor visionOS would use this pattern; not in current Phase 2 scope but worth flagging that Atlas's adaptive-rendering must accommodate spatial form factor.

### A.6 — WatchOS Settings (reduced surface)

**Pattern**: extreme constraint. System-level Settings app exists with limited categories; per-app settings typically managed from the paired iPhone (the iPhone's Watch app contains a "My Watch → [App Name] → Settings" pane for each installed app).

**Accessibility**: native API is **UIAccessibility (watchOS)**. VoiceOver + AssistiveTouch (wrist-gesture) + Taptic feedback are watchOS-specific. Even if Atlas defers to paired-device, the **Helm glance widget on watch must be VoiceOver-accessible** — a complication-style summary needs proper accessibility traits.

**Sunfish takeaway**: validates the meta-plan's earlier position that *watch / TV / IoT form factors don't render Atlas at all* — settings are managed from the paired desktop / mobile. This is a Mission Space form-factor gate; Atlas cross-form-factor adapter respects it. *But* the Helm glance does render at watch scope and must meet a11y baseline.

---

## Appendix B — Pro-tool & SaaS settings UX survey

### B.1 — VSCode (dual-surface settings)

**Pattern**: same data, two views. The Settings UI is a structured form generator; the `settings.json` file is a JSON document. Both edit the same backing store. Search-as-you-type on both. Schema-driven validation (extension-defined schemas).

**Strengths**: dual-surface lets novices use the form and power-users edit the JSON directly; schemas enable autocomplete + validation in the JSON editor. Per-user, per-workspace, per-folder scope hierarchy.

**Sunfish takeaway**: dual-surface is the gold standard for technical users; Atlas should expose JSON view alongside the form view (toggle in advanced mode). Scope hierarchy (default → tenant → user → session) maps naturally onto Sunfish's Standing Order layering.

### B.2 — JetBrains IDEs (search-driven preferences)

**Pattern**: search bar is the primary discovery surface; the categorized tree is secondary. Settings are deeply hierarchical (Editor → General → Code Folding → ...) but search hits expand the tree to the relevant node. Per-IDE + per-project + per-user scope.

**Strengths**: search outperforms categorization for power users; "search by feature name" beats "remember which submenu it lives under."

**Sunfish takeaway**: search-as-you-type with deep-link results is mandatory for Atlas across 8 layers. Categorization is the IA fallback.

### B.3 — Stripe Dashboard (diff-preview before save)

**Pattern**: web admin console. Configuration changes show a diff before commit ("you're about to change X from Y to Z; confirm?"). Audit log accessible per-account. Multi-actor approval flows for high-stakes changes.

**Strengths**: diff-preview prevents fat-finger errors; audit log is right there in the UI (not a separate "compliance" pane).

**Sunfish takeaway**: diff-preview for high-stakes Standing Orders (security policy, integration credentials) is essential. Multi-actor approval composes on Phase 2 commercial scope's permissions matrix.

### B.4 — Notion / Linear (workspace + user split)

**Pattern**: clear separation of *workspace settings* (admin-controlled) and *user settings* (per-user). Top-level navigation distinguishes them.

**Sunfish takeaway**: validates Layer 2 (tenant configuration) vs Layer 1 (user preferences) split. Atlas's top-level navigation should make this distinction visible.

---

## Cross-references

- Plan: `~/.claude/plans/sunfish-wayfinder-configuration-research.md`
- Methodology playbook (W#33): `~/.claude/plans/mission-space-research-methodology.md`
- Intake: `icm/00_intake/output/2026-05-01_wayfinder-intake.md`
- Active workstream: `icm/_state/active-workstreams.md` row W#34
- Project memory (naming): `~/.claude/projects/-Users-christopherwood-Projects-Sunfish/memory/project_workstream_34_wayfinder_naming.md`
- Precedent (W#33): `icm/01_discovery/output/2026-04-30_mission-space-matrix.md`
- Pipeline: `icm/pipelines/sunfish-gap-analysis/{README,routing,deliverables}.md`
- W#33 follow-on ADRs (load-bearing predecessors): ADR 0062 (Mission Space Negotiation Protocol — runtime), ADR 0063 (Mission Space Requirements — install-UX)
- Predecessor ADRs: 0007 + 0009 + 0013 + 0028 + 0029 + 0036 + 0041 + 0046 + 0046-a1 + 0048 + 0049 + 0051 + 0052 + 0055 + 0056 + 0057 + 0061 + 0062 + 0063
