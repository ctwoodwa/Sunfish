# UPF — Contact / Address `use` Enum: Categorical Tag, Preference Workflow, or Alternative Pattern?

**Stage:** 00 Intake / 01 Discovery hybrid (UPF Stage 0–2 driving an architectural choice)
**Status:** Research + recommendation complete; awaiting CEO sign-off on chosen pattern
**Date:** 2026-04-29
**Author:** CTO (research session)
**Triggered by:** CEO directive 2026-04-29 — "conduct UPF on FHIR's use enum on contacts/addresses (home/work/temp/old/billing) as this could be a preference workflow pattern or another standards pattern could apply more globally."
**Resolves:** OSS primitives research open question #2 — "Adopt FHIR `use` enum on contacts/addresses?" — and the deeper question of how Sunfish should model contact-purpose semantics across domain modules.
**Companion:** [`oss-primitive-types-research-2026-04-29.md`](../../01_discovery/output/oss-primitive-types-research-2026-04-29.md) (PR #231); forthcoming dynamic-forms substrate ADR.

---

## Stage 0 — Discovery & Sparring

### Check 0.1 — Existing Work

Sunfish substrate that touches contact/address modeling:

- **`blocks-leases.Models.Party`** — has `PartyKind` (tenant/landlord/manager/guarantor) on the Party itself; no contact-method-level enum. Each Party currently models contact intrinsics implicitly (it's "the leaseholder's address" because the Party is a leaseholder).
- **`blocks-properties.Models.PostalAddress`** — value object with no `use` field. Property has one address.
- **`blocks-maintenance.Models.Vendor`** — has VendorContact entries (audit revealed) but no per-contact `use` discrimination.
- **Phase 2 commercial intake** — needs distinguishing "leaseholder's home address (the rental)" from "leaseholder's billing address (might be different)" from "leaseholder's emergency contact" — implicitly demands some discrimination mechanism.

The cluster has been **silently relying on per-Party 1:1 contact assumptions**. Adding a second address (billing differs from home) breaks the model. This UPF is overdue.

### Check 0.4 — Industry Research

Researched 8 systems' approaches to contact/address purpose-tagging.

#### FHIR `ContactPoint.use` and `Address.use`

```
ContactPoint.use ∈ {home, work, temp, old, mobile, anonymous}
Address.use      ∈ {home, work, temp, old, billing}
```

Plus: `ContactPoint.rank` (integer; lower = higher priority for selection) and `period: Period` (start/end of validity).

Pattern: each contact instance has a single categorical tag + ordered preference within rank + period bounds.

#### vCard (RFC 6350) — TYPE parameters

```
TEL;TYPE=cell,voice,pref:+1-555-1234
ADR;TYPE=home,pref:;;123 Main St;...
```

Pattern: each contact can have *multiple* TYPE values; `TYPE=PREF` (or `PREF=N`) marks preferred. The values are extensible (HOME, WORK, FAX, CELL, VOICE, MSG, VIDEO, TEXTPHONE).

#### LDAP / X.500 — attribute usage

LDAP doesn't tag values with use; instead, distinct attribute names: `homePhone`, `mobile`, `pager`, `fax`, `mail`, `workfax`. The "use" is encoded in the attribute name.

Trade-off: rigid (limited extensibility) but simple; type system enforces what each attribute is for.

#### Schema.org Person

Has separate properties: `email`, `telephone`, `faxNumber`, `address`, `workLocation`, `homeLocation`. Like LDAP — purpose encoded in property name. No `use` enum.

For multi-address: properties accept arrays. To distinguish, you'd use `homeLocation` vs `workLocation` (separate properties).

#### Apple Contacts / Google Contacts

Both use **free-form labels** with a small set of "standard" labels (Home, Work, Mobile, Fax, Other) that the user can override or add to.

```
Phone: { label: "iPhone", value: "+1-555-..." }
Phone: { label: "Vacation House", value: "+1-555-..." }
```

Pattern: open-set labels; UI suggests common ones; user-extensible. Marking a contact as "primary" is separate (often via UI default, not data field).

#### Microsoft Graph / Exchange

`Person` has typed properties: `homeAddress`, `businessAddress`, `otherAddress`. Plus `mobilePhone`, `homePhones[]`, `businessPhones[]`. Like LDAP / Schema.org — purpose encoded in property name.

#### Salesforce

`Contact` has typed fields: `MailingAddress`, `OtherAddress`, `MobilePhone`, `HomePhone`, `OtherPhone`. Plus the multi-address Salesforce object type for advanced cases. Pattern: typed fields, like LDAP.

#### iCalendar `ATTENDEE`

```
ATTENDEE;ROLE=REQ-PARTICIPANT;PARTSTAT=ACCEPTED;CN=Jane Doe:mailto:jane@...
```

`ROLE` is the function (chair / required-participant / optional-participant / non-participant). `CN` is the display. Different concern: role separates "what they're doing here" from "who they are." Worth comparing.

### Check 0.3 — Better Alternatives (the AHA effect)

Surveying the patterns above reveals a fundamental design tension I missed when adopting FHIR `use` directly:

> **"Home" is a *type* (where this contact intrinsically is). "Billing" is a *use* (what we use it for). FHIR conflates these into one enum.**

Calling an address `use=billing` doesn't tell you what kind of address it intrinsically is — could be home, work, P.O. box, third-party billing service. Calling it `use=home` doesn't tell you whether we use it for billing, mailing, both, or neither.

Apple/Google's free-form labels + Microsoft/Salesforce/LDAP's typed-property-name + FHIR's `use` enum are all *partial* solutions because they each encode one of two orthogonal concerns and fold the other in implicitly.

CEO's hint — "preference workflow pattern" — is the AHA: **the right model separates intrinsic type from functional assignment, and lets users assign multiple uses per contact.**

#### The eight approaches in the bake-off

| # | Pattern | Type/intrinsic | Use/assignment | Multi-use per record | Marketplace fit |
|---|---|---|---|---|---|
| **A** | FHIR `use` enum (single tag) | Conflated | Conflated | No | Healthcare-shaped |
| **B** | vCard TYPE parameters (multi-tag list) | Mixed (HOME, CELL = type; PREF = preference) | Implicit via PREF | Yes | Personal contacts |
| **C** | Free-form labels (Apple/Google) | User-defined string | Implicit | Yes | Consumer contacts |
| **D** | Typed property names (LDAP/Microsoft/Salesforce/Schema.org) | Encoded in property name | Implicit | Limited (one per property) | Enterprise schemas |
| **E** | Period-based (FHIR Period) | Independent | Independent | Yes — overlap by period | Long-term records |
| **F** | Hybrid: type tag + preference flags | Tag for type | Boolean flags per use | Yes | Property management ✓ |
| **G** | Role/Type + Function/Use (separation) | Categorical type | Map<UseContext, Priority> | Yes | Structurally cleanest ✓ |
| **H** | Explicit assignment table (separate records) | On the contact | On a separate "assignment" record | Yes — assignment-scoped | Enterprise multi-tenant ✓ |

#### Pattern G — Role/Type + Function/Use separation

The strongest match for property management's actual usage patterns. Each contact carries:

```yaml
ContactPoint:
  kind: enum {Home, Work, Mobile, Temporary, Other}     # intrinsic; what this is
  value: text                                            # the address / phone / email
  period: Period?                                        # start/end of validity
  usage_assignments: Map<UseContext, Priority>           # functional; what we use it for
  pronunciation: text?                                   # for phone numbers in vendor calls

UseContext: enum {
  Primary,           # the catch-all "use this if no specific use specified"
  Mailing,           # postal mail
  Billing,           # invoices, financial communication
  Emergency,         # urgent contact
  Marketing,         # opt-in promotional
  AfterHours,        # off-hours operational contact
  Public,            # publicly-listed contact
  Compliance,        # regulator-required contact (e.g., property notices to landlord-of-record)
}

Priority: int                                            # lower = higher priority within same UseContext
```

Examples:

**Leaseholder Alice** has one address (her rental):
- ContactPoint: kind=Home, value="123 Main St", usage_assignments={Primary: 0, Mailing: 0, Billing: 0}

**Leaseholder Bob** has two addresses:
- ContactPoint: kind=Home, value="123 Main St (rental)", usage_assignments={Primary: 0, Mailing: 0}
- ContactPoint: kind=Work, value="456 Office Ave", usage_assignments={Billing: 0}    // bills go to work

**Vendor Carlos**:
- ContactPoint: kind=Work, value="789 Plumbing Ave", usage_assignments={Primary: 0, Mailing: 0, Billing: 0}
- ContactPoint: kind=Mobile, value="+1-555-...", usage_assignments={Emergency: 0, AfterHours: 0}
- ContactPoint: kind=Home, value="Personal address", usage_assignments={}    // not used for any business purpose

**Property listing public surface**:
- Pulls "Public" UseContext from owner's ContactPoint set
- Owner can rotate which contact is Public without changing the contact intrinsics

This pattern captures the actual question Sunfish needs to answer at usage time: "give me the email to send the invoice to" → query for `usage_assignments contains Billing` ordered by Priority.

#### Pattern H — Explicit assignment table (cross-tenant variant)

A more generalized version separates assignments into their own records:

```yaml
ContactPoint:
  kind: enum {Home, Work, Mobile, Temporary, Other}
  value: text
  period: Period?

ContactAssignment:
  contact_ref: ContactPoint
  use_context: UseContext
  priority: int
  scope: AssignmentScope                  # e.g., per-tenant / per-relationship / per-record-class
  effective_period: Period?
  authorized_by: IdentityRef
```

Pattern H is overkill for SMB property management but **may be the right shape for cross-tenant scenarios** (e.g., a vendor-on-Sunfish-marketplace whose "primary contact" is different per tenant relationship). Defer to v2 unless property-ops cluster surfaces a real need.

### Check 0.5 — ROI Analysis

| Pattern | Implementation cost | Authoring UX | Query complexity | Cross-tenant scaling | Long-term flexibility |
|---|---|---|---|---|---|
| A — FHIR `use` enum | Lowest | Trivial dropdown | Trivial | Limited (single tag conflates concerns) | Limited |
| B — vCard TYPE | Low | Multi-select chips | Medium | Limited | Medium |
| C — Free-form labels | Lowest | Free text + suggestion | Trivial but ambiguous | Poor (label collisions) | Open but unstructured |
| D — Typed properties | Low (per type) | Trivial per property | Trivial | Poor (rigid) | Poor (each new use case = schema change) |
| E — Period-based | Low | Date pickers | Medium (filter by period) | Good (history preserved) | Good |
| **F — Hybrid type+flags** | Medium | Tag + flag toggles | Medium | Good | Good |
| **G — Role/Type + Function/Use** | Medium-high | Type dropdown + use-context multiselect | Medium (Map lookup) | Good | High |
| H — Assignment table | High | Most complex | Higher (cross-record join) | Excellent | Highest |

**ROI winner: Pattern G** for property-management Phase 2.x. Cleanly separates concerns; admin authoring is straightforward (type dropdown + use-context chips); query at usage time is a Map lookup; cross-tenant scaling is preserved through per-tenant `ContactPoint` ownership; future expansion to Pattern H is non-breaking (assignments become separate records, current Map-based shape preserved as denormalized projection).

### Check 0.6 — Updates / Constraints / People Risk

- **Constraint:** ContactPoint compound primitive will be referenced by every domain module (Property owner, Lease party, Equipment vendor, Inspection inspector, Receipt vendor, etc.). Pattern choice affects every downstream model.
- **Constraint:** CEO's revised primitive catalog (OSS primitives research, PR #231) specifies `ContactPoint` as a v1 primitive. The shape of that primitive is what's at stake here.
- **Constraint:** Foundation.I18n + locale-aware rendering applies — `kind: Home` displays as "Home" in en-US, "自宅" in ja-JP, etc.
- **People risk:** SMB property managers are not contact-management experts. Authoring UX must default to "easy" (one address marked as Primary covers everything) but allow advanced customization (separate billing address) without forcing it.
- **People risk:** Cluster intake authors (CTO and PM) need to internalize the type-vs-use distinction. Memory note codifies it.

---

## Stage 1 — The Plan

### 1.1 Context & Why (≤3 sentences)

Sunfish's `ContactPoint` compound primitive (per OSS primitives research) needs a model for distinguishing contact purpose. FHIR's `use` enum (the obvious choice) conflates intrinsic type ("home") with functional assignment ("billing"); industry survey shows 8 patterns with different trade-offs, and the AHA insight (CEO's "preference workflow") is that **type and use are orthogonal concerns** that should be modeled separately. **CTO recommends Pattern G — Role/Type (categorical kind) + Function/Use (Map<UseContext, Priority>) — for v1**, with Pattern H (assignment-table) as a non-breaking v2 expansion path.

### 1.2 Success Criteria (with FAILED conditions)

**Success:**
- ContactPoint primitive ships with `kind` + `usage_assignments: Map<UseContext, Priority>` shape
- `UseContext` enum extensible per tenant (admin can add `Marketing`, `Compliance` etc.)
- Authoring UX has a "simple mode" (mark contact as Primary; everything else inherits) and "advanced mode" (per-use-context assignment)
- Query API supports "give me the contact for UseContext X with highest priority"
- Same shape works for ContactPoint (phone/email/etc.) and Address
- Domain modules (Lease, Vendor, Property, Equipment) consume the primitive uniformly
- Period bounds preserve history ("former billing address" is queryable)

**FAILED conditions (kill triggers):**
- Authoring UX requires more than 3 clicks to mark a contact as billing-preferred → simple-mode failure
- Map-based assignments produce ambiguous resolution at query time (multiple contacts with same UseContext + same priority) → resolution rules are unclear; revisit
- Period-bounds + assignments interact unsafely (e.g., "old" billing address still resolves at query time) → semantics need tightening
- More than 5 cluster modules need `UseContext` extensions beyond the v1 set {Primary, Mailing, Billing, Emergency, Marketing, AfterHours, Public, Compliance} → enum is wrong size

### 1.3 Assumptions & Validation

| Assumption | Validate by | Impact if wrong |
|---|---|---|
| Type and use are genuinely orthogonal in property-management workflows | Sketch 5 representative scenarios (leaseholder, vendor, owner, manager, public-listing); verify each separates intrinsic-type from functional-use | If conflated in real use, simpler Pattern A or D wins |
| Map<UseContext, Priority> accommodates priority-tied contacts (multiple contacts both for "Billing", e.g., primary + secondary) | Try resolving "send to all Billing contacts ordered by priority" and "send only to highest-priority Billing contact" | If priority semantics ambiguous, refine to ordered-list shape |
| `UseContext` enum stays bounded (≤10 values) | Walk through all cluster modules and list UseContexts each needs | If admin-extensible-enum needed, Pattern G upgrades to "tenant-configurable enum" — small change |
| Period bounds compose cleanly with assignments | Model an old-billing-address scenario; query "current Billing contact"; verify period filter excludes old | If Period+Assignment interaction is buggy, sequence Period validation in query path |

### 1.4 Phases (binary gates)

**Phase 1 — Define ContactPoint v1 shape with type + assignments.**
- Spec the C# record / schema definition
- Implement in `Sunfish.Foundation.Compounds` (new package per OSS research recommendation)
- Add `UseContext` enum with v1 values {Primary, Mailing, Billing, Emergency, Marketing, AfterHours, Public, Compliance}
- PASS gate: type compiles; record validation passes; sample serialization round-trip works
- FAIL gate: type ergonomics rejected by review (too verbose, too implicit, etc.)

**Phase 2 — Adapt cluster shipped models to ContactPoint shape.**
- `blocks-leases.Models.Party` — adopt ContactPoint[]
- `blocks-properties.Models.Property` — switch from single `PostalAddress` to `Address[]` with usage_assignments
- `blocks-maintenance.Models.Vendor + VendorContact` — ContactPoint[] per Vendor
- PASS gate: existing tests pass; new tests cover use-context resolution
- FAIL gate: domain models can't accommodate the shape without breaking refactor

**Phase 3 — Form rendering integration.**
- Form engine reads ContactPoint primitive shape; renders type dropdown + value input + assignment chips
- Simple mode: mark Primary; assigns to Mailing+Billing+Primary defaults
- Advanced mode: per-use-context assignment with priority
- PASS gate: BDFL can author a vendor with two phones (work + emergency) in <60s
- FAIL gate: authoring UX takes more than 3 clicks for the simple case

**Phase 4 — Query API + downstream consumers.**
- `GetContactForUseAsync(party, useContext)` returns highest-priority match
- `GetAllContactsForUseAsync(party, useContext)` returns ordered list
- Statement email job calls `GetContactForUseAsync(leaseholder, Billing)`
- Right-of-entry notice calls `GetContactForUseAsync(tenant, Primary)`
- PASS gate: 5+ representative consumer call sites work; performance acceptable (single Map lookup per query)
- FAIL gate: query semantics unclear; consumers have to write workaround logic

### 1.5 Verification

- **Automated:** unit tests cover (a) priority resolution; (b) period bounds; (c) missing-assignment fallback to Primary; (d) Map serialization round-trip
- **Manual:** BDFL reviews ContactPoint authoring UX in dynamic-forms demo
- **Ongoing observability:** audit emission per ADR 0049 when contact assignments change ("Billing reassigned from ContactA to ContactB at 2026-05-15") for compliance trail

---

## Stage 1.5 — Adversarial Hardening

Six perspectives stress-test Pattern G.

### Outside Observer

> "You're rejecting FHIR despite FHIR being the healthcare interoperability standard. Why?"

FHIR's `use` enum works for healthcare's use cases (patient records with single primary contact + temporary contacts during episodes-of-care). Property management has a fundamentally different shape — long-running tenant relationships where billing/mailing/primary may diverge over years. The use-vs-type conflation is a known limitation discussed in FHIR working groups. Adopting Pattern G isn't rejecting FHIR — it's adopting FHIR's structural primitives (period + rank + display) while improving the categorical conflation. ContactPoint can be losslessly converted to FHIR's shape at the API boundary if Sunfish ever needs FHIR interop.

### Pessimistic Risk Assessor

> "Map<UseContext, Priority> with multiple contacts per UseContext — what happens when priority is tied?"

Priority ties need explicit semantics:
- For "send to one" queries: implementation-defined order (insertion order is a reasonable default; document explicitly)
- For "send to all" queries: return all contacts at the highest priority tier
- For audit / compliance scenarios: log which contact was chosen + the priority + tie-breaker reason

Add to Phase 4 PASS gate: "tied-priority resolution rules are documented + tested."

### Pedantic Lawyer

> "Statutory notice (e.g., right-of-entry, FCRA adverse-action) often *requires* notice to the address-of-record, not whatever's marked Primary. Will the data model preserve this?"

Yes — Period bounds + audit emission preserve the historical record. "Address-of-record at the time of notice" is queryable: filter ContactPoint by Period, take Primary in effect at the notice timestamp. Add to Phase 4: query API supports time-travel ("what was the Primary contact at timestamp X?").

### Skeptical Implementer

> "You're proposing Map<UseContext, Priority>. C# record with a Map property is awkward to author + serialize."

Real concern. Implementation options:
- `IReadOnlyDictionary<UseContext, int>` — clean read; awkward authoring (immutable-update pain)
- `IReadOnlyList<(UseContext, int)>` — easier authoring; loses Map semantics; allows duplicates
- Custom `UsageAssignmentSet` value object with builder pattern — most ergonomic; more code

Recommend Option C (custom value object with `WithAssignment(useContext, priority)` builder), per the existing pattern in shipped Sunfish records. Defers serialization details to its own JSON converter.

### The Manager

> "Pattern G is more complex than Pattern A. Is the complexity justified for SMB property management?"

The complexity surfaces only when needed. Simple mode in authoring UX (Phase 3) handles the common case (one address, marked Primary, all assignments inherit) in one click. Advanced mode unlocks for the cases that genuinely need it (vendor with separate emergency phone; leaseholder with separate billing address). The complexity is **opt-in at authoring time** but **always-available at query time**, which is exactly the right asymmetry for an SMB platform.

### Devil's Advocate

> "Just use FHIR `use` enum (Pattern A). It's the standard. Done."

FHIR `use` would need to be amended for every cross-cutting concern that surfaces (Marketing scope, Compliance scope, Public-listing scope). Each amendment to a primitive type ripples through every domain module that uses it. Pattern G's type-vs-use separation contains the change to the `UseContext` enum (extensible) without touching ContactPoint shape. **The rate-of-change argument favors G; the standards-conformance argument favors A.** Standards conformance can be achieved at the FHIR-API boundary if needed (lossless conversion is straightforward). Long-term flexibility wins.

---

## Stage 2 — Meta-Validation

### Check 1 — Delegation strategy

Phase 1 (define shape) is CTO + research-session work. Phase 2 (adapt models) is COB hand-off. Phase 3 (form rendering integration) couples to dynamic-forms substrate ADR; sequence after that ADR ships. Phase 4 (query API) is COB after Phase 2.

### Check 2 — Research needs

8 systems researched (FHIR, vCard, LDAP, Schema.org, Apple/Google, Microsoft Graph, Salesforce, iCalendar). Sufficient.

### Check 3 — Review gate placement

Each Phase has a binary PASS/FAIL gate. CEO review at:
- End of Phase 1 (ContactPoint shape lands)
- End of Phase 3 (authoring UX walkthrough)
- End of Phase 4 (query API exposed; downstream consumers integrated)

### Check 4 — Anti-pattern scan (21-AP list)

- AP-1 Unvalidated assumptions: 4 named with validation steps ✓
- AP-2 Vague phases: 4 phases with binary gates ✓
- AP-3 Vague success criteria: 7 measurable items + 4 FAILED conditions ✓
- AP-4 No rollback: ✓ Pattern A (FHIR enum) is reachable from G via one-line projection if G fails
- AP-9 Skipping Stage 0: 8 patterns researched + AHA insight surfaced ✓
- AP-10 First idea unchallenged: AHA explicitly challenged FHIR-as-default ✓
- AP-11 Zombie projects: kill triggers explicit ✓
- AP-13 Confidence without evidence: industry research cited per source ✓
- AP-19 Discovery amnesia: existing Sunfish substrate (Foundation.I18n, Period from FHIR adoption) leveraged ✓
- AP-21 Assumed facts without sources: each system claim cites the system explicitly ✓

**Critical APs: none fired.**

### Check 5 — Cold Start Test

A fresh contributor reading this UPF + Stage 0 OSS research could implement Phase 1 (define ContactPoint shape) without further clarification. Phases 2-4 reference cluster shipped models + dynamic-forms substrate ADR (forthcoming) — those need to be readable to execute. Acceptable.

### Check 6 — Plan Hygiene

- Sections delimited cleanly
- Cross-references resolve
- Pattern table is single source of truth

### Check 7 — Discovery Consolidation

Stage 0 → Stage 1 flows clearly:
- Industry research (Check 0.4) → 8-pattern bake-off (Check 0.3 AHA) → ROI (Check 0.5) → recommendation
- People risk (Check 0.6) → simple-mode authoring UX in Phase 3
- ContactPoint as cross-module dependency (Check 0.1) → Phase 2 cluster-model adaptation

---

## Quality rubric self-check

- C (Viable): All 5 CORE + ≥1 CONDITIONAL ✓
- B (Solid): C + Stage 0 + FAILED conditions + Confidence + Cold Start ✓
- A (Excellent): B + 6-perspective sparring + review gates + reference list + replanning triggers ✓

**Confidence: HIGH.** 8 industry sources researched; trade-offs articulated; pattern G is the structurally cleanest answer for property-management workload.

**Replanning triggers:**
- Cluster module needs cross-tenant assignments (Pattern H upgrade required)
- More than 8 UseContext values needed; admin-extensible enum required
- FHIR interop becomes a real customer requirement (boundary-conversion layer needed)
- Per-instance-conditional assignments needed (e.g., "this address is Billing only when state=TX")

---

## Decisions for CEO

1. **Adopt Pattern G — Role/Type + Function/Use separation?** Default = yes. Override = pin specific alternative (A through H).
2. **`UseContext` enum size + values?** Default v1 set: {Primary, Mailing, Billing, Emergency, Marketing, AfterHours, Public, Compliance}. Override = add or remove.
3. **Admin-extensible UseContext (per-tenant)?** Default = no for v1 (8 values cover known property-management cases); extensible in v2 if needed.
4. **Apply pattern to both ContactPoint and Address?** Default = yes (same shape; consistent ergonomics).
5. **Sequencing:** integrate ContactPoint refactor into the dynamic-forms substrate ADR (turn after next), or ship as standalone primitive substrate first? Default = bundle into dynamic-forms ADR (single coherent landing).

---

## Cross-references

- ADR 0001 (Schema Registry) — ContactPoint shape registered as built-in type
- ADR 0049 (Audit Trail) — assignment changes emit audit events
- OSS primitives research (PR #231) — proposed ContactPoint as compound primitive #10
- Forthcoming dynamic-forms substrate ADR — incorporates this UPF's recommendation
- [`taxonomy-management-substrate-intake-2026-04-29.md`](./taxonomy-management-substrate-intake-2026-04-29.md) — sibling intake (CEO directive same day)

## Sign-off

CTO (research session) — 2026-04-29
