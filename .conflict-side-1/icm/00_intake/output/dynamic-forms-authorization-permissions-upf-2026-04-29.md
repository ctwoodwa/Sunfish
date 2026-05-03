# UPF — Dynamic Forms Authorization & Role-Based Permissions

**Stage:** 00 Intake / 01 Discovery hybrid (Stage 0–2 UPF analysis driving an architectural decision)
**Status:** Research + recommendation complete; awaiting CEO sign-off on chosen approach
**Date:** 2026-04-29
**Author:** CTO (research session)
**Triggered by:** CEO directive 2026-04-29 — "we need a universal planning session on Levels of permission granularity as there may be different approaches to the challenge like simply using linked forms where each form is generated for a dedicated role. we need to research industry standards on dynamic form permissions/role based. alternative sources may be in dynamic survey systems."
**Resolves:** Open question from prior dynamic-forms discussion — how does Sunfish handle role-scoped form access (e.g., send form to plumber; plumber sees only fields they have access to)?
**Companion:** Forthcoming dynamic-forms substrate ADR (synthesizes this UPF + cross-field-rules UPF + OSS primitives research into the formal architecture decision)

---

## Stage 0 — Discovery & Sparring

Per UPF: "major discoveries happen *during* execution, not during planning." This Stage 0 surfaces them before we lock in.

### Check 0.1 — Existing Work

Sunfish substrate that touches this:

- **ADR 0032 — Multi-team Anchor + macaroon capability model.** Each principal carries a macaroon-bearing token; capabilities are scoped + revocable + verifiable. The "vendor magic-link" pattern in ADR 0053 work-orders + cluster vendor intake already uses this.
- **Foundation.Macaroons** — the kernel-tier macaroon primitive. Issue + verify + caveat-check.
- **Foundation.Catalog.Bundles** — `ProviderCategory` + `ProviderRequirement` declares what a bundle exposes; could be extended to declare per-role exposed fields.
- **`blocks-businesscases`** — bundle-provisioning + entitlement resolver. Already runs rule-evaluation infrastructure that could host permission policy.
- **`blocks-tenant-admin`** — `TenantRole` + `TenantUser` entities; basic per-tenant role management exists.
- **ADR 0043 — Threat model + chain-of-permissiveness.** Defines the trust posture for capability-bearing tokens crossing the public-input boundary.

Sunfish has the **capability + token + role** pieces for ANY of the approaches below. This is not a greenfield problem; it's a "which substrate composition do we adopt" problem.

### Check 0.4 — Industry Research (Factual Verification)

The CEO specifically called out dynamic survey systems as an alternative source. Researched 10+ sources across enterprise-app, survey-system, and policy-engine categories.

#### Enterprise-app field-level RBAC

| System | Pattern | Granularity | Trade-off |
|---|---|---|---|
| **Salesforce** | Field-level security (FLS) per profile + permission sets; can hide, read-only, or edit-allow per field per profile | Field-level + record-level (sharing rules) | Enterprise-grade; *very* powerful; admin overhead substantial — large-org Salesforce setups have dedicated profile-management teams |
| **Microsoft Dataverse / Dynamics 365** | Field-level security (FLS) via field security profiles; classified fields require explicit profile membership | Field-level | Identical model to Salesforce; same admin burden |
| **ServiceNow** | ACL (Access Control List) rules — table.field per role with read/write/create/delete | Field-level + condition-driven (script ACLs) | Most flexible; scripts can fire arbitrary logic per-field-per-record |
| **SAP** | Authorization objects with field-value combos | Field-value level (e.g., "this user can write Field=X but only when Value matches Pattern") | Most granular but most complex |
| **Drupal** | Field permissions module — per-content-type per-field per-role view/edit | Field-level | Mid-flex; scoped to CMS content types |

**Common theme:** Enterprise-app field-level RBAC is *very powerful* but has substantial admin overhead. Maintaining the permission matrix becomes a job for someone (Salesforce admins are a literal profession). Field-level RBAC is right when you have hundreds of users, dozens of roles, thousands of records, and regulatory pressure (HIPAA, SOX, GDPR). Probably overkill for SMB property management with 2-5 users + 10-30 leases.

#### Survey systems (CEO's suggested alternative source)

| System | Pattern | Granularity | Trade-off |
|---|---|---|---|
| **Qualtrics** | Role-based research with embedded-data + tokenized respondent links; per-respondent conditional display | Section/question-level + token-scoped | Used for 360-feedback flows; multiple roles fill different parts of one survey instance |
| **Typeform** | Logic jumps + hidden fields (set via URL params); workspace permissions about *form authoring*, not about *response viewing* | Question-level conditional visibility | Simple; lacks multi-respondent collaboration |
| **SurveyMonkey** | Page routing per response; section permissions limited; per-respondent token | Section-level mostly | Mid-flex |
| **JotForm** | Conditional logic per field; "Approval flow" with multi-stage submission to different reviewers; tab-based section grouping | Field-level conditional + section-level via tabs | Closest survey-system equivalent to "linked forms per role" — multi-stage approval flows are essentially what CEO described |
| **SurveyJS** (OSS) | `visibleIf` / `enableIf` / `requiredIf` per question; multi-form workflows; full role-based JSON survey definitions | Question-level via condition expressions | Open source; closest fit for what Sunfish would adapt |
| **Google Forms** | Section-based with branching | Section-level only | Simplest; no per-question RBAC |

**Common theme in survey systems:** **stage-based forms** with **tokenized delivery** dominate. Each respondent gets a unique link that reveals only their portion. **Field-level RBAC is uncommon in survey systems** — usually it's section-level or stage-level with conditional visibility for edge cases.

The vendor magic-link pattern (cluster intake + ADR 0053) is *exactly* this survey-system pattern: tokenized URL → vendor sees their assigned section.

#### Policy engines (separate from form systems)

| System | Pattern |
|---|---|
| **Open Policy Agent (OPA)** | Rego language; declarative policy as data; runtime evaluation engine; standard for cloud-native systems |
| **Casbin** | Configurable RBAC/ABAC library; policy stored as adapter-backed CSV/DB; embedded in app code |
| **XACML** (eXtensible Access Control Markup Language) | OASIS standard; declarative; verbose; legacy enterprise |
| **AWS IAM** | Policy documents (JSON); resource + action + condition; capability-bearing roles |
| **Zanzibar / SpiceDB / Permify** | Google's relationship-based authorization; "user X has relation Y to object Z"; powers Google Drive, Docs sharing model |

**Common theme:** Modern cloud-native systems separate policy from data + form. Policy lives in an external engine (OPA, Casbin, Zanzibar) that's queried at runtime. Heaviest, most flexible, but introduces a moving part.

#### What this means for Sunfish

The full design space:
- **Where permissions live:** in the schema (declarative tags); in a separate policy doc; in capability tokens (macaroons); in form templates themselves (linked forms)
- **What level they apply to:** entire form / section / field-group / field / specific cell
- **Who decides:** platform-shipped roles vs admin-defined roles; per-tenant vs cross-tenant
- **When they fire:** form-render time / submit-validation / background reconciliation

### Check 0.3 — The AHA effect: Better Alternatives

CEO's hint — "linked forms where each form is generated for a dedicated role" — is the load-bearing AHA candidate. **Investigate before committing.**

Six approaches now in the bake-off:

#### Approach A — Field-level permission tags on schema

Schema declares `read_by:[Role]` + `write_by:[Role]` per field/group. One form template renders differently per actor.

- **Pro:** Co-located with schema; declarative; powerful
- **Pro:** Single form template; no template duplication
- **Con:** Salesforce-grade admin overhead — this is the burden the survey systems explicitly avoid
- **Con:** Form engine complexity grows with permission expressiveness
- **Con:** Field-level admin UX needs to be built (set permissions per field per role)

#### Approach B — Linked forms / role-specific generation [CEO's suggestion]

For each role that needs a form, generate or author a *separate form template* targeting that role's fields. No in-form filtering; the form IS the role's view.

- **Pro:** Conceptually simple — each role gets a form designed for them
- **Pro:** Survey-system-aligned; matches Qualtrics/JotForm "approval flow" pattern
- **Pro:** Zero runtime permission-evaluation; rendering is template-driven
- **Pro:** Can be authored by non-developers (one form per role, not a permission matrix)
- **Con:** Schema-form drift risk — same underlying entity, multiple form templates, fields can be missed
- **Con:** Multi-form synchronization burden when entity changes
- **Con:** Doesn't handle "you can SEE this field but not EDIT it" cleanly without being either two forms or a per-field annotation anyway

#### Approach C — Macaroon-bearing tokens (capability-based)

Each actor presents a macaroon scoped to a `field-set`. Form engine intersects available fields with macaroon scope.

- **Pro:** Cryptographically enforceable; matches ADR 0032 substrate; revocation comes free
- **Pro:** Tokens carry the scope; no centralized policy lookup
- **Pro:** Works for cross-tenant scenarios (vendor magic-link from one tenant; vendor doesn't have an account)
- **Con:** Token scopes need to be defined somewhere — punts the "where's the permission data" question
- **Con:** Macaroon issuance UX is harder than tag-on-schema or template-generation
- **Con:** Scope changes require token re-issuance

#### Approach D — Policy-as-data separate engine

Permission rules in their own document; engine evaluates policy against actor + field at runtime. OPA / Casbin-style.

- **Pro:** Most flexible; handles complex conditional rules
- **Pro:** Industry-standard pattern; known tooling
- **Con:** Introduces another moving part (policy engine)
- **Con:** Latency on every form render
- **Con:** Heavier than SMB property management warrants

#### Approach E — Form workflow / multi-stage hand-off

Form has stages; each stage is assigned to a role. Stage 1 = applicant fills X, submits → stage 2 = reviewer fills Y, submits → stage 3 = vendor fills Z. Workflow + form together.

- **Pro:** Survey-system-aligned (JotForm Approval Flow, Typeform multi-step)
- **Pro:** Naturally maps to property-management workflows (lease application → owner review → lease drafting → tenant signs)
- **Pro:** Each stage's form is implicitly role-scoped — no separate permission system
- **Con:** Doesn't handle "concurrent partial access" (multiple parties looking at same form simultaneously)
- **Con:** Workflow engine becomes the permission engine — coupling

#### Approach F — Hybrid: section-based template generation + macaroon scope

Schema declares **sections** with role tags. Form engine generates per-role views by selecting sections matching the actor's role (Approach B-shape: linked forms via generation). Capability token carries the actor's role + section scope (Approach C-shape: macaroon enforcement). Within a visible section, field-level annotations are optional for edge cases (Approach A-shape: tag-overlay).

- **Pro:** Combines survey-system simplicity (section-based, generated forms) + capability-bearing tokens (cryptographic enforcement) + field-level escape hatch (when needed)
- **Pro:** Authoring UX scales: define sections-by-role at schema time, not field-by-field
- **Pro:** Composes with ADR 0032 macaroon substrate without bespoke policy engine
- **Pro:** Handles the vendor magic-link case naturally (token grants section access)
- **Con:** More moving parts than pure A or pure B
- **Con:** Implementation requires both template generation AND runtime token verification

### Check 0.5 — ROI analysis

| Approach | Phase 1 build cost | Admin authoring cost | Runtime cost | Long-term flexibility |
|---|---|---|---|---|
| A — Field-level tags | High (form engine + tag system) | High (per-field UX) | Medium (filter at render) | High |
| B — Linked forms | Low (multiple form templates) | Low (per-role form authoring) | Low (template-driven) | Low (drift risk) |
| C — Macaroons | Medium (token issuance UX) | Medium | Low (token scope) | Medium |
| D — Policy engine | Very high (OPA/Casbin embedding) | High (rule authoring) | Medium-high | Very high |
| E — Workflow stages | Medium (workflow + form coupling) | Medium | Low | Medium (workflow-bound) |
| **F — Hybrid** | **Medium-high** | **Low-medium** | **Low** | **High** |

ROI winner: **Approach F (Hybrid)** for property-management Phase 2; the section-level core inherits survey-system simplicity, macaroon-enforced capability inherits ADR 0032 substrate, field-level escape hatch handles edge cases without forcing them.

Approach B (pure linked forms) is the simplest and might be the right answer if Sunfish's roles are static + few + well-known. But CEO indicated MVP needs admin-defined types, which implies admin-defined roles / sections. F adapts; B doesn't.

### Check 0.6 — Updates / Constraints / People Risk

- **Constraint:** ADR 0032 macaroon substrate is real; build on it rather than bypass it
- **Constraint:** SMB property-management scale (2-5 users, 10-30 leases) doesn't justify enterprise-app permission burden
- **Constraint:** Vendor magic-link pattern is already a Sunfish primitive; survey-system tokenized-link pattern aligns
- **People risk:** No dedicated permission-admin role; CEO doesn't have time to manage a Salesforce-style profile matrix
- **People risk:** When admin-defined types unlock (Phase 2.x), users will define their own sections + role mappings; UX must remain simple

---

## Stage 1 — The Plan

### 1.1 Context & Why (≤3 sentences)

Sunfish needs to render forms scoped to actor role — vendor sees vendor's section, owner sees everything, tenant sees their portion. Multiple architectural approaches exist (field-level RBAC, linked forms, capability tokens, policy engines, workflow stages, hybrid); the right answer for SMB property-management is the one that minimizes admin burden + leverages existing ADR 0032 macaroon substrate + scales to admin-defined types in Phase 2.x. **CTO recommends Approach F (Hybrid: section-based template generation + macaroon scope + field-level annotations as escape hatch).**

### 1.2 Success Criteria (with FAILED conditions)

**Success:**
- Schema can declare sections with role tags; form engine renders only sections matching actor's macaroon scope
- Capability tokens carry section + field scope; tokens are cryptographically verifiable + revocable
- Vendor magic-link scenario works end-to-end without bespoke policy engine
- Admin-defined types (Phase 2.x) can declare sections + role mappings without Salesforce-grade admin UX
- Field-level annotations available for edge cases ("this field within an otherwise-visible section is hidden if X")
- Per-role rendering is deterministic — same schema + same token = same form output
- Audit trail emits permission-denied events when token doesn't grant requested section/field

**FAILED conditions (kill triggers):**
- Section-level granularity proves insufficient for any major MVP use case → consider Approach D (policy engine)
- Macaroon issuance UX exceeds 2-3 hours of admin work per role per tenant → fall back to Approach B (pure linked forms)
- Form drift between role-views creates real bugs in production → fall back to Approach A (field-level tags)
- Vendor magic-link tokens don't compose cleanly with section scope → revisit ADR 0032 model first

### 1.3 Assumptions & Validation

| Assumption | Validate by | Impact if wrong |
|---|---|---|
| Section-level granularity covers >90% of property-management permission needs | Sketch out 5 representative use cases (vendor work-order, leaseholder lease, prospect application, tenant maintenance request, bookkeeper financial views) and verify each maps to sections | If <90%, hybrid degrades to pure A (field-level tags); reverses simplicity win |
| ADR 0032 macaroon model accommodates section-set capabilities without amendment | Read ADR 0032 + Foundation.Macaroons code; verify caveat language can express "section in [list]" | Amendment needed; +1 turn of CTO work; not a structural blocker |
| Existing capability-token issuance flow is admin-friendly enough for non-developer use | Walk through Anchor admin UX for token issuance; compare with survey-system tokenized-link UX (Qualtrics-style) | If too developer-y, need new admin UX; ~1-2 weeks PM work |
| Survey-system patterns (section-based + tokenized link) compose well with ADR 0032 capability model | Trace through 2-3 use cases end-to-end | If poor composition, hybrid is awkward; degrade to B or C |

### 1.4 Phases (binary gates)

**Phase 1 — Section-based schema definition.**
- Schema language gains `Section` first-class concept with `role_access` declarations
- Each section is a named group of fields
- Schema editor (admin UX) allows authoring sections + role mappings
- PASS gate: schema can declare sections; schema validator enforces section integrity (no orphan fields; section references resolve)
- FAIL gate: any of the above missing

**Phase 2 — Macaroon section-scope.**
- Macaroon caveats extended to express section-set: `section_in [section_id_1, section_id_2]`
- Token issuance UX adds section-set selector
- Verification logic intersects token's section scope with schema sections
- PASS gate: macaroon issuance produces token with section scope; verification rejects out-of-scope section access; revocation works
- FAIL gate: ADR 0032 amendment needed (escalate; +1 turn CTO work)

**Phase 3 — Form engine integration.**
- Form rendering reads section declarations + actor's macaroon scope
- Renders only sections in intersection
- Graceful UX when section access changes mid-edit (read-only fallback; explicit re-auth prompt)
- PASS gate: end-to-end vendor magic-link demo works (CEO sends form to plumber; plumber sees plumber section; plumber doesn't see owner-only fields)
- FAIL gate: form drift, missing fields, broken validation

**Phase 4 — Field-level annotations (escape hatch).**
- Schema allows per-field `read_only_if`, `hidden_if`, `required_if` expressions evaluated against actor + record state
- Applied AFTER section filtering
- PASS gate: ≥3 representative field-level rules ship; tests cover them
- FAIL gate: rule expressiveness insufficient for representative cases (rare; means we need stronger conditional language)

**Phase 5 — Audit trail emission.**
- Permission-denied events emit to ADR 0049 audit substrate
- Rate-limited (don't flood the log on form-load attempts)
- Searchable per-tenant + per-actor + per-resource
- PASS gate: representative permission-denied events appear in audit trail
- FAIL gate: emission breaks form rendering performance

### 1.5 Verification

- **Automated:** unit tests for section-scope intersection; integration tests for vendor magic-link flow; permission-deny audit emission; section-set caveat parsing
- **Manual:** CEO walks through 5 representative scenarios: vendor work-order; leaseholder lease; prospect application; tenant maintenance; bookkeeper financial view
- **Ongoing observability:** permission-denied audit events monitored; spike → investigate (someone trying to access something they shouldn't, or schema/token misconfigured)

---

## Stage 1.5 — Adversarial Hardening

Six perspectives stress-test the recommendation.

### Outside Observer

> "You're proposing a hybrid that combines three approaches (B + C + A escape-hatch). Are you hedging because you don't know which is right, or because you've genuinely judged the trade-offs?"

Genuinely judged. Pure B (linked forms) drifts; pure C (macaroons) needs scope-source data anyway; pure A (field-level) overburdens admin. Hybrid F leverages survey-system simplicity (section-based) + cryptographic enforcement (macaroon) + flexibility (field-level escape hatch only when needed).

### Pessimistic Risk Assessor

> "What's the worst-case failure mode? Vendor sees a field they shouldn't?"

Worst case: section misconfiguration. Plumber's macaroon scope says `section_in [vendor_assignment, vendor_execution, tenant_coordination]` but admin accidentally puts a tenant-PII field in `tenant_coordination`. Plumber sees the PII.

Mitigations:
- Schema validator flags fields tagged with high-sensitivity metadata if they're in non-restricted sections
- Audit trail emits permission-grant events at token-issuance time; reviewable
- Add a "field sensitivity" classification (PII, financial, legal, etc.) that requires specific section tagging
- Stage 1.5 deferred from MVP: a pre-publish "permission lint" tool that runs over the schema before admin saves

### Pedantic Lawyer

> "Real-estate disclosures, FHA Fair Housing Act, FCRA — there are statutory restrictions on who can see what. Does your model handle those?"

Section-level granularity covers macro-categories (PII / Financial / Legal). For specific statutory rules (e.g., FHA prohibits screening by protected class via specific fields), the **field-level annotation escape hatch (Approach A within F)** handles them: tag protected-class fields with `read_by:[ComplianceOfficer]` only.

But the deeper concern is *audit-trail completeness for compliance defense*. Phase 5's audit emission needs to capture permission-grants + denies + token-issuances + section-membership-changes with enough fidelity that a compliance audit can reconstruct who-saw-what-when. ADR 0049 substrate handles this; verify in Phase 5 PASS gate.

### Skeptical Implementer

> "You're proposing 5 phases. How long is this actually?"

Honest estimate:
- Phase 1: ~2 weeks (schema language extension + validator + admin UX for section authoring)
- Phase 2: ~1 week (macaroon caveat extension + section-scope verification + revocation)
- Phase 3: ~2 weeks (form engine integration + vendor magic-link demo)
- Phase 4: ~1 week (field-level annotation escape hatch + tests)
- Phase 5: ~1 week (audit emission + monitoring)

**Total: ~7 weeks** for the permissions substrate. Concurrent with other dynamic-forms work — adds ~2-3 calendar weeks to MVP if parallelized cleanly; ~7 weeks if serial.

### The Manager

> "BDFL just wants to send a form to a plumber. Are we building too much?"

Possibly. Phase 1-3 alone (sections + macaroon scope + form engine integration) covers the BDFL scenario. Phase 4 (field-level escape hatch) is for edge cases that may not surface in Phase 2 commercial; defer to Phase 2.x. Phase 5 (audit emission) is for compliance + future commercial-grade tenants; can also defer.

**Aggressive scope cut: Phase 1 + 2 + 3 only for v1**, deferring Phase 4 + 5 to Phase 2.x. Reduces 7 weeks → 5 weeks.

### Devil's Advocate

> "Just use linked forms (Approach B). Simpler. Done in 2 weeks."

Approach B works for v1 *if* role definitions are static. CEO requires admin-defined types in v1, which implies admin-defined sections and roles. Linked-form templates would need to be regenerated on every schema change → drift risk. Not stable foundation.

But Devil's Advocate has a point: **for the BDFL-and-spouse-and-vendor scenario specifically, Approach B might be sufficient**. The decision is forward-looking. If the platform is going to support admin-defined types in v1, F is the right shape. If MVP is BDFL-and-vendors only and admin authoring deferred to v2, B is sufficient.

CEO's prior directive: "make admin-defined types v1 and JSONB required". This locks F over B.

---

## Stage 2 — Meta-Validation

### Check 1 — Delegation strategy clarity

Phases 1-2 are CTO + research-session work (schema language design + macaroon caveat extension). Phases 3-5 are PM (sunfish-PM) implementation work. Hand-off authoring per Phase: section-based schema → form engine → audit emission, in order.

### Check 2 — Research needs identification

Industry research (this Stage 0) is sufficient. No further research needed before committing.

### Check 3 — Review gate placement

Each Phase has a PASS/FAIL gate. CEO review at:
- End of Phase 1 (schema-section vocabulary lands)
- End of Phase 3 (vendor magic-link demo works end-to-end)
- End of Phase 5 (audit trail review)

### Check 4 — Anti-pattern scan (21-AP list)

- AP-1 Unvalidated assumptions: 4 named with validation steps ✓
- AP-2 Vague phases: 5 phases, binary gates, sized estimates ✓
- AP-3 Vague success criteria: 7 measurable success items + 4 FAILED conditions ✓
- AP-4 No rollback: ✓ Each phase is reversible (schema-section vocabulary can be removed; macaroon caveats roll back; form engine reverts to no-permission rendering)
- AP-5 Plan ending at deploy: Phase 5 includes ongoing observability ✓
- AP-6 Missing Resume Protocol: N/A — single multi-phase plan
- AP-9 Skipping Stage 0: Stage 0 explicitly conducted with industry research ✓
- AP-10 First idea unchallenged: 6 approaches considered with rejection rationale; CEO's AHA moment honored ✓
- AP-11 Zombie projects: kill triggers explicit ✓
- AP-12 Timeline fantasy: 7-week estimate with parallelization caveat ✓
- AP-13 Confidence without evidence: industry research cited per source ✓
- AP-19 Discovery amnesia: existing ADR 0032 + Foundation.Macaroons substrate explicitly leveraged ✓
- AP-21 Assumed facts without sources: each industry-system claim has system named ✓

**Critical APs: none fired.**

### Check 5 — Cold Start Test

Could a fresh contributor execute this plan alone? Phases 1-3 require deeper substrate context (schema language design, macaroon caveats, form engine integration). Probably need 2-3 reading sessions on prior ADRs (0032, 0043, dynamic-forms ADR when it lands) before starting. Acceptable.

### Check 6 — Plan Hygiene

- Sections clearly delimited
- No orphan paragraphs
- Cross-references resolve

### Check 7 — Discovery Consolidation

Stage 0 findings → Stage 1 plan flows clearly:
- Industry research (Check 0.4) → 6-approach bake-off (Check 0.3) → ROI analysis (Check 0.5) → recommendation
- Existing-substrate leverage (Check 0.1) → Phase 2 macaroon extension
- People risk (Check 0.6) → success criteria emphasizes admin simplicity

---

## Quality rubric self-check

- **C (Viable):** All 5 CORE + ≥1 CONDITIONAL. ✓
- **B (Solid):** C + Stage 0 + FAILED conditions + Confidence Level + Cold Start Test. ✓
- **A (Excellent):** B + sparring + Review Checkpoints + Reference Library + Knowledge Capture. ✓

**Confidence: HIGH.** Industry research is concrete; substrate exists; phases are sized realistically.

**Replanning triggers:**
- Cluster intake reconciliation (workstreams #18 #19 #25 #27 EXTEND-disposition) introduces a permission scenario that section-level can't model
- ADR 0032 amendment required for section-scope (escalate to CO if amendment substantial)
- Phase 3 vendor magic-link demo fails the "BDFL sends form to plumber" walkthrough
- Performance regression: form rendering + permission filtering exceeds 200ms p95

---

## Decisions for CEO

1. **Adopt Approach F (Hybrid: section-based + macaroon scope + field-level escape hatch)?**
   - Default = yes; reasons in Stage 0 ROI
   - Override = pick A / B / C / D / E (with rationale; CTO will revise plan)

2. **MVP scope: full 5 phases (~7 weeks) or aggressive cut to Phases 1+2+3 (~5 weeks)?**
   - Default = aggressive cut; defer field-level escape hatch + audit emission to Phase 2.x
   - Override = full 5 phases

3. **Section-level vs section + field-level granularity in v1?**
   - If aggressive cut adopted (default), section-level only in v1; field-level escape hatch deferred
   - If full 5 phases, both available

4. **Sequencing among CTO deliverables:** I committed to OSS primitives research, cross-field rules UPF, and dynamic-forms substrate ADR earlier. With this UPF in flight, sequence is now:
   - **This turn: ship this UPF** (current)
   - **Next turn: OSS primitives research artifact** (~3000-4000 words)
   - **Turn after: cross-field rules UPF** (~3000 words)
   - **Turn after that: dynamic-forms substrate ADR** synthesizing UPF + research + rules + JSONB + admin-defined types
   - Override the order if you want different sequencing

---

## Cross-references

- ADR 0032 — Multi-team Anchor + macaroon capability model (substrate this UPF builds on)
- ADR 0043 — Threat model (chain-of-permissiveness for capability tokens)
- ADR 0049 — Audit-trail substrate (Phase 5 emission target)
- Cluster intake — vendor magic-link pattern (`property-vendors-intake-2026-04-28.md` + `property-work-orders-intake-2026-04-28.md`)
- Forthcoming dynamic-forms substrate ADR (synthesizes this UPF + cross-field-rules UPF + OSS primitives research)

## Sign-off

CTO (research session) — 2026-04-29
