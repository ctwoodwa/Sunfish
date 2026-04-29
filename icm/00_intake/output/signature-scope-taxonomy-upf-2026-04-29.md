# UPF — `SignatureScope` Alignment with Foundation.Taxonomy Substrate

**Stage:** 00 Intake / 01 Discovery hybrid (UPF Stages 0–2 driving an architectural choice within ADR 0054)
**Status:** Research + recommendation complete; awaiting CEO sign-off to amend ADR 0054 accordingly
**Date:** 2026-04-29
**Author:** CTO (research session)
**Triggered by:** CEO directive 2026-04-29 — "conduct UPF on 54 SignatureScope ensure this complies/supports the concepts in the Foundation.Taxonomy package."
**Resolves:** Architectural alignment between ADR 0054's `SignatureScope` enum and the forthcoming Foundation.Taxonomy substrate (intake at [`taxonomy-management-substrate-intake-2026-04-29.md`](./taxonomy-management-substrate-intake-2026-04-29.md), PR #234).
**Companion:** ADR 0054 — Electronic Signature Capture & Document Binding (Status: Proposed; council-reviewed B grade; amendments queued).

---

## Stage 0 — Discovery & Sparring

### Check 0.1 — Existing Work

ADR 0054 currently defines `SignatureScope` as a hard-coded C# enum:

```csharp
public enum SignatureScope
{
    Lease,
    WorkOrderCompletion,
    InspectionMoveIn,
    InspectionMoveOut,
    CriteriaAcknowledgement,
    Other
}
```

This enum is referenced in:
- `SignatureEvent.Scope` (the cryptographically-bound scope of each signature)
- `SignatureCaptureRequest.Scope` (scope passed at capture time)
- 5 cluster modules that consume signatures (Leases, Work Orders, Inspections, Leasing Pipeline, iOS App)

The Foundation.Taxonomy substrate intake (PR #234, captured from CEO's vision 2026-04-29) defines:
- Taxonomies as versioned, governed products with lineage
- Stable references via `{taxonomy_id, taxonomy_version, node_id}` triples
- Authoritative vs marketplace ownership
- Clone / Extend / Alter derivation operations
- Per-vertical governance regimes (corporate/government strict; civilian flexible)
- CRDT-backed tenant-owned editing
- Marketplace distribution

The question CEO is asking: should `SignatureScope` be a hard-coded enum (the current ADR 0054 shape) or a Taxonomy reference (composing the substrate Sunfish is committing to)?

### Check 0.4 — Industry Research

Researched 7 signature-system standards + classification taxonomies for signature scope semantics.

#### FHIR Signature

```
Signature {
  type: [Coding],          // <-- multiple Codings; each from a registered code system
  when: instant,
  who: Reference,
  onBehalfOf: Reference,
  targetFormat: code,
  sigFormat: code,
  data: base64
}
```

Each signature carries a **list of `Coding`** values; each Coding points at a code system (a taxonomy). FHIR ships starter ValueSets for signature purposes (e.g., `urn:iso-astm:E1762-95:2013` "Signature Type Codes" with values like `1.2.840.10065.1.12.1.1` = Author's Signature, `1.2.840.10065.1.12.1.2` = Coauthor's Signature, etc.).

**Key insight:** FHIR has already converged on **Taxonomy-backed signature scope** rather than a hardcoded enum. Multiple `Coding` values per signature handle the cases where one signature serves multiple purposes (e.g., "this is both an Author's Signature and a Witness Signature").

#### DocuSign envelope types + reasons

DocuSign separates two concerns:
- **Envelope type** — what the document is (Real Estate, Sales, HR, etc.)
- **Signing reason** — why this signer is signing (review, approve, witness, notary)

Both are configurable per organization. DocuSign ships defaults; organizations can extend.

#### Adobe Sign signature reasons

Similar to DocuSign — admin-configurable list of signing reasons. Defaults exist but tenants override.

#### W3C Verifiable Credentials

`VerifiableCredential.credentialSubject` carries claim types as URIs pointing at registered vocabularies. The signature itself ('proof') has a `proofPurpose` field with values like `assertionMethod`, `authentication`, `keyAgreement`, `capabilityInvocation`, `capabilityDelegation` — each a URI from a registered vocabulary.

#### XAdES / CAdES

XML/CMS Advanced Electronic Signatures support `SignaturePolicyIdentifier` (URI pointing at signature policy document) + `CommitmentTypeIndication` (URI pointing at commitment type — what the signer is committing to). Both are URI references to registered vocabularies, not hardcoded enums.

#### XAdES `CommitmentTypeIdentifier` examples

ETSI ships standard URIs:
- `1.2.840.113549.1.9.16.6.1` — proof of origin
- `1.2.840.113549.1.9.16.6.2` — proof of receipt
- `1.2.840.113549.1.9.16.6.3` — proof of delivery
- `1.2.840.113549.1.9.16.6.4` — proof of sender
- `1.2.840.113549.1.9.16.6.5` — proof of approval
- `1.2.840.113549.1.9.16.6.6` — proof of creation

Plus organizations can register their own URIs.

#### HelloSign / Dropbox Sign

Tags + custom fields per template; less granular than DocuSign/Adobe but moves toward configurability.

#### SignNow / PandaDoc

Per-tenant signature category configuration; admin-defined categories.

### Cross-source synthesis

| System | Signature scope shape | Tenant-extensible? | Lineage tracking |
|---|---|---|---|
| FHIR | Multiple `Coding` from registered code systems | Yes (per FHIR ValueSet pattern) | Via ValueSet versioning |
| DocuSign | Per-org configurable list | Yes | No formal lineage |
| Adobe Sign | Per-org configurable list | Yes | No formal lineage |
| W3C Verifiable Credentials | URI references to registered vocabularies | Yes | URI versioning |
| XAdES / CAdES | OID references to commitment types | Yes (organizations register OIDs) | OID hierarchy |
| HelloSign / SignNow / PandaDoc | Per-tenant tags / categories | Yes | No formal lineage |

**Universal pattern:** Every mature signature system uses **registered-vocabulary references** (not hardcoded enums). FHIR is the most rigorous (formal ValueSets with versioning); XAdES is the most legally-defensible (OIDs with ETSI standardization); DocuSign/Adobe are the most pragmatic (admin-configurable lists). **None use hardcoded enums.**

### Check 0.3 — Better Alternatives (the AHA effect)

The current ADR 0054 enum is **inconsistent with industry signature systems** AND **inconsistent with Sunfish's own Foundation.Taxonomy substrate direction.** This is dual-misalignment.

The AHA insight: `SignatureScope` should be a `TaxonomyClassification` (per the Taxonomy substrate intake), not a hardcoded enum. Specifically:

- Sunfish ships a default taxonomy `taxonomy://sunfish.dev/signature-scopes/v1.0` with the current enum's values plus a richer scope hierarchy
- Tenants can derive (clone/extend/alter) the taxonomy if they need custom scopes (e.g., "RegulatoryFiling.HOA-Disclosure", "VendorAgreement.MutualNDA", "EmploymentContract.W2-Acknowledgement")
- Each signature event carries `Scope: TaxonomyClassification` (a `{taxonomy_id, taxonomy_version, node_id}` triple), not an enum value
- Audit trail records the precise taxonomy + version + node, so the legal-defense narrative is "this signature attests to commitment-type X as defined by Sunfish Signature Scope Taxonomy v1.0 node Y" — verifiable + reproducible

#### Five approaches in the bake-off

| # | Approach | Signature → Scope coupling | Tenant-extensible | Cross-vertical reuse | Legal-defensibility |
|---|---|---|---|---|---|
| **A** | Hardcoded enum (current ADR 0054) | Tight (enum + signature in same package) | No | No | Limited |
| **B** | Taxonomy-classification reference (AHA) | Loose (signature → TaxonomyClassification → taxonomy node) | Yes (clone/extend/alter) | Yes | High (ETSI/FHIR-aligned) |
| **C** | String-tag (free-form scope: text) | Loose | Yes | Yes (but no governance) | Low (no controlled vocabulary) |
| **D** | Hybrid: hardcoded high-level kind + Taxonomy-classification scope | Mixed | Partial | Partial | Medium |
| **E** | Multiple Coding (FHIR-shape; list of TaxonomyClassifications) | Loose; richer than B | Yes | Yes | Highest (FHIR mirror) |

#### Pattern E (FHIR-mirror) deep-dive

```csharp
public sealed record SignatureEvent
{
    // ...
    public required IReadOnlyList<TaxonomyClassification> Scopes { get; init; }
    //   ^ multiple scopes per signature; each is a TaxonomyClassification
    // ...
}

public sealed record TaxonomyClassification
{
    public required TaxonomyId TaxonomyId { get; init; }
    public required SemanticVersion TaxonomyVersion { get; init; }
    public required string NodeId { get; init; }
}
```

Examples:
- A lease signature: `Scopes: [{Sunfish.Signature.v1, "contract.lease.execution"}]`
- A signature that's both an Author and a Witness: `Scopes: [{Sunfish.Signature.v1, "author.attestation"}, {Sunfish.Signature.v1, "witness.attestation"}]`
- A jurisdiction-specific notarized signature: `Scopes: [{Sunfish.Signature.v1, "contract.lease.execution"}, {US.California.Notary.v2, "jurat.lease"}]`

**Pattern E is the strongest answer.** Mirrors FHIR's industry-validated shape; aligns with Foundation.Taxonomy substrate; supports legal-defense narratives across multiple jurisdictions; allows multi-purpose signatures naturally.

### Check 0.5 — ROI Analysis

| Approach | Implementation cost | Tenant authoring complexity | Cluster module impact | Future flexibility |
|---|---|---|---|---|
| A — Hardcoded enum | Lowest (it's in place) | None | Cluster modules tightly coupled | Limited (every new scope = code change) |
| B — Single Taxonomy reference | Medium (substrate composition) | Low (admin selects from taxonomy) | Cluster modules consume substrate | High |
| C — Free-form string | Low | Trivial but ungoverned | Cluster modules consume strings | Anti-pattern for legal-defense |
| D — Hybrid kind + Taxonomy | Medium-high | Medium | Cluster modules consume both | Medium |
| **E — Multiple Codings (FHIR-shape)** | **Medium-high** | **Low** | **Cluster modules consume substrate uniformly** | **Highest** |

**ROI winner: Pattern E** for property-management and broader Sunfish-as-platform. The Foundation.Taxonomy substrate is being built anyway (PR #234 intake captured); composing `SignatureScope` on it is structurally clean and unlocks per-tenant + per-jurisdiction extensibility.

### Check 0.6 — Updates / Constraints / People Risk

- **Constraint:** ADR 0054 is at Status: Proposed; council-reviewed B grade with 6 amendments queued. SignatureScope-as-Taxonomy is a 7th amendment but architecturally complementary.
- **Constraint:** Foundation.Taxonomy substrate is itself queued (PR #234 intake; ADR not yet drafted). SignatureScope refactor depends on Taxonomy substrate being available.
- **Constraint:** Existing typed `SignatureScope` enum is referenced by 5 cluster modules (Leases, Work Orders, Inspections, Leasing Pipeline, iOS App) — refactor surface is bounded; no shipped code yet.
- **People risk:** SMB property managers (BDFL) don't typically extend signature scopes; default taxonomy v1.0 covers the vast majority of cases. Authoring extension UX is for the rare case (custom legal forms).
- **People risk:** Compliance-sensitive verticals (corporate/government) need authoritative taxonomy support; this aligns with Foundation.Taxonomy's `authoritative_vs_marketplace` regime.
- **People risk:** Cold-start for new admins — they shouldn't have to learn the Taxonomy substrate to send a lease for signature. Default taxonomy is selected automatically; admin only engages Taxonomy authoring if customizing.

---

## Stage 1 — The Plan

### 1.1 Context & Why (≤3 sentences)

ADR 0054's `SignatureScope` enum is structurally inconsistent with both industry signature systems (FHIR / XAdES / DocuSign all use registered-vocabulary references) and Sunfish's own Foundation.Taxonomy substrate (per CEO directive 2026-04-29). The right shape composes the Taxonomy substrate: `SignatureEvent.Scopes` becomes a list of `TaxonomyClassification` references, with Sunfish shipping a default `Sunfish.Signature.v1` taxonomy that tenants can derive from. **CTO recommends Pattern E (FHIR-mirror — multiple Codings)** for ADR 0054 amendment, replacing the enum and aligning with Foundation.Taxonomy.

### 1.2 Success Criteria (with FAILED conditions)

**Success:**
- ADR 0054's `SignatureScope` enum amended to `IReadOnlyList<TaxonomyClassification> Scopes`
- Sunfish ships `taxonomy://sunfish.dev/signature-scopes/v1.0` with a richer hierarchy than the current 6-value enum (~15-25 starter nodes covering common property-management + cross-vertical cases)
- Cluster modules (Leases, Work Orders, Inspections, Leasing Pipeline, iOS App) consume `TaxonomyClassification` references uniformly
- Tenant-derived signature taxonomies work (clone/extend/alter operations)
- Legal-defense narrative is intact: every signature audit-trail record names taxonomy + version + node
- Multi-scope signatures supported (one signature for multiple commitment types)
- FHIR boundary-conversion layer feasible (Sunfish Codings ↔ FHIR Codings) for future interop

**FAILED conditions (kill triggers):**
- Foundation.Taxonomy substrate ADR not accepted before ADR 0054 acceptance — sequencing problem; ADR 0054 amendment depends on Taxonomy substrate being available
- Per-tenant taxonomy authoring UX too complex for typical signature-scope-extension use case — fall back to Pattern D (hybrid: hardcoded kind + Taxonomy)
- TaxonomyClassification serialization too heavy for embedded signature events (>1KB per scope) — investigate compact representation
- Multi-scope semantics ambiguous (e.g., "is this signature valid if at least one scope is satisfied, or all?") — need explicit resolution rules

### 1.3 Assumptions & Validation

| Assumption | Validate by | Impact if wrong |
|---|---|---|
| Foundation.Taxonomy substrate ships in v1 alongside dynamic-forms substrate (ADR 0055) | ADR 0055 implementation checklist Phase 1 confirms; Foundation.Taxonomy ADR drafted next | If Taxonomy substrate slips, fall back to enum + later refactor |
| Sunfish's default signature taxonomy v1.0 covers >95% of property-management use cases | Sketch the taxonomy node hierarchy; cross-check with cluster modules' signature scenarios | If gaps, add nodes; iterate on default taxonomy |
| Multi-scope semantics work cleanly (one signature attests to multiple scopes) | Sketch 5 representative multi-scope cases; verify legal-defense narrative for each | If semantically ambiguous, fall back to Pattern B (single scope) |
| FHIR interop is foreseeable but not v1 | Identify customers who need FHIR signature interop; defer until concrete | If not realistic, drop FHIR-mirror complexity; Pattern B sufficient |

### 1.4 Phases (binary gates)

**Phase 1 — Author Sunfish.Signature.v1 default taxonomy.**
- Define hierarchical signature scope node tree (root = "Signature"; children = Contract, Attestation, Acknowledgement, Witness, Notary, RegulatoryFiling, Other; each with sub-nodes)
- Property-management starter coverage: lease execution + amendment + renewal + termination, work-order completion, inspection move-in/out + annual + post-repair, criteria acknowledgement, vendor agreement, disclosure
- Locale-aware display labels via `InternationalizedText`
- Status: Published (per Foundation.Taxonomy substrate semantics)
- PASS gate: 5 cluster modules' signature scenarios all map to nodes in the taxonomy
- FAIL gate: gaps; iterate

**Phase 2 — Amend ADR 0054 with TaxonomyClassification reference.**
- Replace `SignatureScope` enum with `IReadOnlyList<TaxonomyClassification> Scopes`
- Update `SignatureCaptureRequest.Scope` similarly
- Update `IDocumentSigningService` contracts
- Update audit emission per ADR 0049 to include taxonomy + version + node references
- PASS gate: amended contract surface compiles (mock implementation); cluster modules consume `TaxonomyClassification` cleanly
- FAIL gate: serialization size or query complexity prohibitive

**Phase 3 — Cluster module integration.**
- Leases use `Sunfish.Signature.v1.contract.lease.execution`
- Work Orders use `Sunfish.Signature.v1.attestation.work_order_completion`
- Inspections use `Sunfish.Signature.v1.attestation.inspection.<trigger>`
- Leasing Pipeline uses `Sunfish.Signature.v1.acknowledgement.criteria`
- iOS App passes `TaxonomyClassification` to `IDocumentSigningService.CaptureAsync`
- PASS gate: 5 cluster modules invoke signature substrate with appropriate Scopes
- FAIL gate: any cluster module's scope not representable in v1 taxonomy

**Phase 4 — Tenant taxonomy authoring + lineage support.**
- Per-tenant Clone/Extend/Alter operations available via Foundation.Taxonomy
- BDFL tenant can derive `Sunfish.Signature.v1` and add custom scopes (e.g., custom HOA-disclosure type)
- Audit emission tracks both default-taxonomy classifications AND derived-taxonomy classifications
- PASS gate: representative tenant-derived taxonomy works end-to-end
- FAIL gate: lineage tracking breaks during Alter operations

**Phase 5 — FHIR boundary-conversion (deferred to v2 unless customer requires).**
- Sunfish.Signature.v1 taxonomy maps to FHIR signature type ValueSets
- Boundary-conversion layer translates Sunfish Codings ↔ FHIR Codings
- Defer to first FHIR-interop customer demand

### 1.5 Verification

- **Automated:** unit tests cover (a) TaxonomyClassification serialization round-trip; (b) audit emission with taxonomy refs; (c) multi-scope signature events; (d) tenant-derived taxonomy resolution
- **Manual:** CEO walks through 3 representative signature scenarios — lease execution; work-order completion; tenant-derived custom scope
- **Ongoing observability:** taxonomy version drift monitored (when Sunfish.Signature.v1.x bumps, are old signatures still resolvable?)

---

## Stage 1.5 — Adversarial Hardening

Six perspectives stress-test Pattern E.

### Outside Observer

> "You're amending ADR 0054 before it's even Accepted. Why not finish ADR 0054 acceptance first?"

ADR 0054 acceptance has 6 amendments queued from council review (canonicalization, algorithm-agility, ADR 0046 amendment authoring, etc.). This UPF surfaces a 7th amendment (SignatureScope → Taxonomy reference). **Better to land the amendments together** than to flip ADR 0054 to Accepted, then immediately re-amend with an architectural change. The 7th amendment is consistent with the 6 council-review amendments and shouldn't delay acceptance further than ~1-2 turns.

### Pessimistic Risk Assessor

> "Foundation.Taxonomy substrate is itself a future ADR. SignatureScope refactor depends on Taxonomy being shipped. What if Taxonomy slips?"

Real risk. Mitigations:
- Foundation.Taxonomy substrate is sequenced as part of ADR 0055 dynamic-forms substrate work (it's a sibling substrate; both ship in the same MVP timeline)
- If Taxonomy substrate proves harder than estimated, **fallback path**: ship ADR 0054 with the original enum + a TaxonomyClassification placeholder field that's null in v1; refactor when Taxonomy lands
- ADR 0055's implementation checklist Phase 1 covers schema registry; Foundation.Taxonomy can ship in parallel without blocking

Risk accepted; fallback documented.

### Pedantic Lawyer

> "Multi-scope signatures — what does 'this signature attests to scope X AND scope Y' mean legally? Is it one signature or two?"

Legally: a single cryptographic signature with multiple commitment-type-indicators is exactly XAdES's `CommitmentTypeIndication` pattern. The signature is one act; the commitment scope is the union of declared scopes. This is well-established. ETSI standard 319 122 explicitly handles this case.

For property management v1: most signatures will have a single Scope. Multi-scope is the exception (e.g., notarized + lease execution simultaneously). Document the resolution rules explicitly: "Signature is valid for any scope listed; the signer attests to all listed scopes."

### Skeptical Implementer

> "TaxonomyClassification serialization is `{taxonomy_id, version, node_id}` — that's ~50-100 bytes per scope. A signature event with 3 scopes is ~300 bytes just for scope refs. Inflation."

Real concern but bounded. Mitigations:
- Most signatures have 1 scope; serialization overhead minimal
- TaxonomyClassification can intern frequently-used scopes (interning via reference dictionary; serialized references are short IDs)
- Actual signature payload is much larger than scope refs; relative overhead is small
- Audit-trail emission is already per-event; scope ref bloat is bounded

### The Manager

> "BDFL just wants to send a lease for signature. Are we over-engineering?"

For BDFL's day-1 use case, the user experience doesn't change:
- Anchor's "send for signature" UX has a "Lease" preset that auto-fills `Sunfish.Signature.v1.contract.lease.execution`
- BDFL never sees the taxonomy ID; just clicks "Send for signature"
- The taxonomy infrastructure is invisible at signature-capture time

The complexity surfaces only when admin needs custom scopes (rare). The default-taxonomy path is fast.

### Devil's Advocate

> "Just keep the enum. Sunfish doesn't need FHIR interop or multi-jurisdiction support yet. YAGNI."

YAGNI argument has merit but loses to two factors:
1. **Foundation.Taxonomy substrate is shipping anyway** for dynamic forms; SignatureScope-as-Taxonomy reuses substrate, doesn't introduce new infrastructure
2. **Cluster Phase 2 commercial scope** already includes vendor-agreement signatures + criteria-acknowledgement signatures — both not in current 6-value enum; they need to be added either way; doing it as Taxonomy nodes is cheaper than enum amendments

Pure-enum approach has the lowest *immediate* cost but the highest *cumulative* cost as scopes proliferate.

---

## Stage 2 — Meta-Validation

### Check 1 — Delegation strategy

Phase 1 (taxonomy authoring) is CTO + research-session work. Phase 2 (ADR amendment) is CTO. Phases 3-4 (cluster integration + tenant authoring) are COB after Foundation.Taxonomy substrate ships.

### Check 2 — Research needs

7 signature systems researched. Sufficient.

### Check 3 — Review gate placement

CEO review at:
- End of Phase 1 (default taxonomy v1.0 definition)
- End of Phase 2 (ADR 0054 amendment language; ready for Acceptance)
- End of Phase 4 (representative tenant-derived taxonomy works)

### Check 4 — Anti-pattern scan (21-AP list)

- AP-1: 4 named assumptions with validation steps ✓
- AP-2: 5 phases with binary gates ✓
- AP-3: 7 success criteria + 4 FAILED conditions ✓
- AP-4: rollback = revert to enum; bounded ✓
- AP-9: Stage 0 with 7 industry sources ✓
- AP-10: AHA challenged hardcoded enum framing ✓
- AP-19: existing Sunfish substrate (Foundation.Taxonomy intake) leveraged ✓
- AP-21: each industry-source claim cited ✓

**Critical APs: none fired.**

### Check 5 — Cold Start Test

A fresh contributor reading this UPF + Foundation.Taxonomy intake (PR #234) + ADR 0054 could implement the SignatureScope amendment. Phase 1 (taxonomy authoring) needs more context on Sunfish's Taxonomy substrate but that's covered in the intake.

### Check 6 — Plan Hygiene

- Sections delimited
- Cross-references resolve

### Check 7 — Discovery Consolidation

Stage 0 → Stage 1 flows clearly:
- Industry research → AHA (FHIR/XAdES/DocuSign all use registered-vocabulary refs) → Pattern E recommendation
- Existing substrate (Foundation.Taxonomy intake) → Phase 2 amendment composes it

---

## Quality rubric self-check

- C (Viable): All 5 CORE + ≥1 CONDITIONAL ✓
- B (Solid): C + Stage 0 + FAILED conditions + Confidence + Cold Start ✓
- A (Excellent): B + 6-perspective sparring + review gates + reference list + replanning triggers ✓

**Confidence: HIGH.** Industry alignment unambiguous (every mature signature system uses Taxonomy refs); Sunfish's own substrate direction validates; refactor surface bounded.

**Replanning triggers:**
- Foundation.Taxonomy substrate slips beyond ADR 0054 acceptance timeline
- TaxonomyClassification serialization size unacceptable
- Multi-scope semantics surface ambiguity at audit-trail review
- Customer concrete requirement for FHIR interop (accelerates Phase 5)

---

## Decisions for CEO

1. **Adopt Pattern E (multiple TaxonomyClassifications per signature)?** Default = yes; per FHIR-mirror + Foundation.Taxonomy alignment.
2. **Sunfish.Signature.v1 default taxonomy hierarchy** — confirm starter node set: Contract.Lease.{Execution, Amendment, Renewal, Termination}; Attestation.{WorkOrderCompletion, Inspection.{MoveIn, MoveOut, Annual, PostRepair}, MaintenanceSignOff}; Acknowledgement.{Criteria, Disclosure}; Witness.Generic; Notary.Jurat; Other. Default = adopt as-is.
3. **Sequence with ADR 0054 acceptance** — bundle this UPF's amendment with the 6 council-review amendments? Default = yes (single coherent amendment wave).
4. **Defer FHIR boundary-conversion to v2?** Default = yes (no current customer demand).
5. **Single scope vs multi-scope in v1** — ship multi-scope from day 1 (Pattern E) or single-scope simpler (Pattern B) and upgrade to multi later? Default = ship multi-scope (Pattern E); the type signature `IReadOnlyList<TaxonomyClassification>` accommodates list-of-1 cleanly; deferring multi-scope means a non-breaking-but-still-disruptive refactor later.

---

## Cross-references

- ADR 0054 — Electronic Signature Capture & Document Binding (this UPF amends)
- [`taxonomy-management-substrate-intake-2026-04-29.md`](./taxonomy-management-substrate-intake-2026-04-29.md) — Foundation.Taxonomy substrate (this UPF's load-bearing dependency)
- ADR 0001 (Schema Registry) — Coding/CodeableConcept primitives
- ADR 0049 (Audit Substrate) — taxonomy-bound signature events emit per substrate
- ADR 0055 (Dynamic Forms Substrate) — ships Foundation.Taxonomy as part of MVP
- FHIR Signature.type + ValueSet specifications
- XAdES / CAdES CommitmentTypeIndication
- ETSI EN 319 122

## Sign-off

CTO (research session) — 2026-04-29
