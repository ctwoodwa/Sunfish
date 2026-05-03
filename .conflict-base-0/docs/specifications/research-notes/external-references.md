# External Research References

**Document type:** Cataloged prior-art references, organized by Sunfish spec concern
**Audience:** Architects, spec editors, phase planners
**Date:** 2026-04-17
**Status:** v0.1 — initial catalog
**Companion docs:** `automerge-evaluation.md`, `ipfs-evaluation.md` (deeper dives on specific technologies)

---

## Purpose

This document catalogs external systems, protocols, and specifications that inform the Sunfish platform design. Each entry includes: what it is, why Sunfish cares, fit with current spec, and a recommendation (adopt as candidate implementation, use as design reference, or note-and-skip).

The aim is a single place to find "what has been done before" when writing or reviewing Sunfish spec sections. The deeper evaluations live in companion notes (Automerge, IPFS); entries here that warrant similar depth may graduate to their own documents later.

Entries are organized by spec section primarily affected. Some systems cut across multiple concerns — cross-references call this out inline.

---

## 1. Form building and input ingestion (spec §3.4 schema registry, §6 MVP, §7 input modalities, Phase 5 blocks-forms)

Sunfish's form story spans three layers: the **schema** (what data is captured — §3.4), the **runtime form** (how users enter it — Phase 5 `blocks-forms` + `ui-adapters-blazor`), and the **ingestion pipeline** (normalizing forms alongside other inputs — §7). The form-builder market has mature exemplars that inform both API design and end-user UX expectations.

### 1.1 Typeform — AI-enhanced form building

- **Source:** https://help.typeform.com/hc/en-us/articles/33777155298708-AI-with-Typeform-FAQ
- **What it is:** SaaS form builder positioned on conversational UX and, more recently, AI-assisted form generation and editing. Natural-language prompts produce question sets; AI can suggest follow-up questions, validate answers in context, and generate summaries.
- **Why Sunfish cares:** The property-management MVP (spec §6) needs inspection forms, maintenance request intake, and lease-application forms. A landlord shouldn't hand-author JSON Schemas — they should describe what they want ("a move-in inspection form with a photo per room, signature, and a deficiency rubric") and get a draft. AI-enhanced form building is a realistic 2026 baseline expectation.
- **Fit:** Informs Phase 5 `blocks-forms` and the Sunfish schema-registry UX. Typeform is a SaaS product, not an adoptable library — we take the **interaction model** as a design reference, not the implementation.
- **Recommendation:** **Design reference.** Phase 5 blocks-forms should include an AI-assisted authoring flow ("describe your form → Sunfish generates a schema + form layout") as an optional companion to hand-authored schemas. Not a blocker for the initial block.

### 1.2 Formstack — enterprise form builder with approvals and workflows

- **Source:** https://www.formstack.com/features/workflows
- **What it is:** Enterprise form platform emphasizing workflows, approvals, conditional routing, and integrations (CRM, signatures, payments). Workflow graph editor routes submissions through multi-stage approval chains with branching, parallelism, and escalation.
- **Why Sunfish cares:** Forms don't exist in isolation — a maintenance request form generates a work item that goes through approvals (tenant submits → PM reviews → contractor quotes → PM approves → work order issued). The combined form+workflow product is what property-management consumers actually need.
- **Fit:** Direct overlap with the composition of `blocks-forms` + `blocks-tasks` + workflow orchestration. Validates the spec's decision to treat forms as one primitive and workflows as another that **compose** — Formstack's "Workflows" concept is essentially Sunfish's combination of schema-registered forms + event-driven orchestration.
- **Recommendation:** **Design reference** for Phase 5 block composition UX. Pay attention to their approval-chain editor and escalation-on-timeout patterns.

### 1.3 Feathery — advanced conditional logic with flows

- **Source:** https://docs.feathery.io/platform/build-forms/logic/available-conditions
- **What it is:** Developer-focused form builder with deep conditional-logic support — conditions on any field, nested groups, rule-based field visibility, cross-field validations, logic flows that act on answers.
- **Why Sunfish cares:** Inspection forms have heavy conditional logic ("if deficiency severity = High, require a photo and a description"). Lease forms have state-specific conditional requirements. The primitive needed is a **condition language** that the schema registry (§3.4) understands. Feathery's catalog of available conditions is a useful baseline.
- **Fit:** Informs the schema registry's condition/validation-rule surface. Their condition types (equal, contains, matches-regex, in-set, numeric-compare, date-compare, is-empty) map well to a JSON Schema + JSON Logic extension.
- **Recommendation:** **Design reference.** Sunfish schema rules should cover the same conditional space at minimum. Don't reinvent — adopt JSON Logic or a similar standardized expression syntax.

### 1.4 Budibase — open-source, API-integrated form builder

- **Source:** https://budibase.com/blog/open-source-form-builder/
- **What it is:** Open-source low-code platform (Apache 2.0) for building internal tools and forms with direct API/database integrations, deployable self-hosted or cloud.
- **Why Sunfish cares:** Same mission shape as Sunfish's commercial/OSS split (per spec §13). Budibase demonstrates a viable open-source form-builder model with commercial support. Their approach to connectors (PostgreSQL, REST, GraphQL) informs how Sunfish blocks integrate with external systems.
- **Fit:** Closer to a peer than a reference — Budibase is a low-code platform, Sunfish is a component framework for building asset management systems. Common ground: self-hostability, permissive license, connector pattern.
- **Recommendation:** **Note-and-skip** for direct adoption. Revisit when Phase 5 is being designed; their connector architecture may inform how Sunfish blocks attach to external data sources.

---

## 2. Workflow orchestration (spec §3.6 event bus, §6 MVP workflows, Phase 5 blocks-tasks, Phase 9 Bridge)

The kernel's event bus (§3.6) is the substrate; workflows are composed on top as blocks. Three external references cover the space from case-management to durable execution to .NET messaging.

### 2.1 Pega Platform — case lifecycles and child cases

- **Source:** https://academy.pega.com/topic/child-cases/v5
- **What it is:** Enterprise case-management platform. Core concept is a **case** — a long-running stateful unit of work with a lifecycle (stages, steps, sub-stages, parallel flows), child cases (sub-cases automatically linked to a parent, with inherited context and independent lifecycles), and configurable SLA/escalation rules.
- **Why Sunfish cares:** The property-management MVP (§6) is full of case-shaped work: an inspection is a case (scheduled → conducted → report generated → deficiencies cataloged → each deficiency becomes a child case → each child case has its own contractor → parent inspection closes when all child deficiencies are resolved). Pega's child-case model is the most mature exemplar of this pattern in the enterprise space.
- **Fit:** **Direct influence** on Sunfish's workflow-block primitive. The spec §6 inspection + maintenance workflows already hint at child cases (inspection → deficiency → repair work-order). Phase 5 `blocks-tasks` should adopt Pega's parent/child-case semantics explicitly.
- **Recommendation:** **Design reference — high fidelity.** Lift the case/child-case vocabulary. Document that Sunfish workflows are case-lifecycle-shaped, not linear flowcharts. Phase 5 block design should include: cases, stages, child cases, parallel flows, SLA/escalation.

### 2.2 Temporal — durable async workflow orchestration

- **Source:** https://temporal.io/blog/durable-execution-in-distributed-systems-increasing-observability
- **What it is:** Durable-execution platform. You write workflow code as normal (sequential, with `await`); Temporal persists every step's state so if the process crashes mid-workflow, it resumes from the last checkpoint on any worker. Handles timers (wait 30 days for a response), retries, compensation, and cross-service orchestration.
- **Why Sunfish cares:** Property-management workflows span long timescales — an inspection scheduled for April runs its workflow for months (scheduling → execution → deficiency tracking → vendor quotes → repair → final signoff). These workflows can't live in memory. Temporal's durable-execution model is the reference for how Sunfish's workflow-block primitive actually executes.
- **Fit:** Architectural pattern Sunfish should **adopt**, not necessarily Temporal itself as a dependency. .NET alternatives: Dapr workflows, Elsa Workflows, DurableTask (same Microsoft team — Azure Durable Functions is a wrapper). Direct Temporal SDK for .NET exists (`Temporalio.Sdk`) and is well-maintained.
- **Recommendation:** **Strong candidate for actual adoption.** Evaluate Temporal .NET SDK vs Dapr workflows for Sunfish Phase 5 blocks-tasks. Temporal has the stronger durable-execution story; Dapr has tighter .NET-ecosystem integration. Revisit at Phase 5 plan-execution time.

### 2.3 MassTransit — .NET event-driven messaging

- **Source:** https://masstransit.io
- **What it is:** Mature .NET message-bus abstraction over RabbitMQ, Azure Service Bus, Amazon SQS, and in-memory transports. Handles request/response, publish/subscribe, routing slips, sagas (long-running stateful correlation), retry/redelivery, circuit breakers.
- **Why Sunfish cares:** The Sunfish kernel event bus (§3.6) is the transport for domain events between blocks and across federation boundaries. Bridge already uses Wolverine (a peer to MassTransit) for its workflow-adjacent messaging. MassTransit is more widely adopted in the broader .NET ecosystem and has a richer saga story.
- **Fit:** Candidate for the Sunfish .NET reference implementation of `IEventBus`. Wolverine (currently in Bridge) and MassTransit are both viable; the kernel's event-bus contract should be transport-agnostic and support either.
- **Recommendation:** **Candidate implementation** for `IEventBus` backend. Document both MassTransit and Wolverine as supported transports; the kernel primitive stays neutral. Bridge's current Wolverine usage continues; a production Sunfish deployment might choose either based on team familiarity.

---

## 3. Authorization (spec §3.5 permission evaluator, §10 delegation)

Spec §3.5 calls for a permission evaluator; §10 covers delegation. Section 10 was revised in spec v0.2 to adopt a Keyhive-inspired group-membership capability model (see `automerge-evaluation.md`). Section 3.5's **policy language** (PolicyL in the spec) is a separate concern from capabilities and has its own prior art.

### 3.1 OpenFGA — fine-grained relationship-based access control

- **Source:** https://openfga.dev
- **What it is:** Open-source implementation of Google Zanzibar's relationship-based access control (ReBAC) model. Authorization decisions answer "can principal X do action Y on resource Z?" by traversing a typed graph of relationships (user, group, team, document, folder, etc.) with configurable rules. CNCF sandbox project as of late 2024.
- **Why Sunfish cares:** Spec §3.5's policy evaluator needs a concrete model. ReBAC fits Sunfish's domain well:
  - "Inspector Jim can write the inspection because Jim is a member of Acme Inspection Firm, which has the inspector role on Property 42."
  - "Contractor Alice can close the deficiency because Alice is the assigned-worker on the repair task, which is a child of the deficiency."
  These are relationship graph traversals, not attribute comparisons.
- **Fit:** **Strong overlap** with Keyhive's group-membership model (§10.2.1). OpenFGA is the ReBAC evaluator; Keyhive is the *capability-and-crypto* layer. They complement cleanly: Keyhive stores membership in a CRDT-synced graph with cryptographic enforcement; OpenFGA expresses the authorization rules that consult that graph.
- **Recommendation:** **Strong candidate for §3.5 policy evaluator implementation.** The spec should reference OpenFGA's authorization-model DSL as the reference syntax for PolicyL. Sunfish's permission evaluator becomes: "an OpenFGA-style ReBAC evaluator backed by the Keyhive capability graph as its membership store." This collapses two design decisions into one coherent stack.

---

## 4. Schema (spec §3.4 schema registry)

### 4.1 JSON Schema — schema definition standard

- **Source:** https://json-schema.org/specification
- **What it is:** IETF-draft specification for describing the shape of JSON data. Types, required fields, constraints (min/max, regex, enum), nested structures, `$ref` cross-references, format hints, conditional subschemas (`if/then/else`, `allOf/anyOf/oneOf`).
- **Why Sunfish cares:** Spec §3.4 calls for a runtime schema registry. JSON Schema is the obvious format — it's ubiquitous, language-agnostic, tooling-rich (validation libraries in every major language), and already the de-facto schema standard for REST APIs (via OpenAPI), form builders, and configuration systems.
- **Fit:** **Direct adoption candidate.** Sunfish schemas ARE JSON Schemas (with Sunfish-specific extensions for entity identity, version ranges, and Sunfish-specific conditional rules).
- **Recommendation:** **Adopt JSON Schema as the schema primitive's format.** Specifically JSON Schema 2020-12 draft. Extensions namespaced under `x-sunfish` (permissions, entity-kind declarations, migration hints). .NET support via `JsonSchema.Net` (Greg Dennis) or `Newtonsoft.Json.Schema`. The schema registry stores schemas as content-addressed blobs keyed by CID (§3.7) — reproducible, verifiable, versioned for free.

---

## 5. Content-addressed storage (spec §3.7 blob store, §10 federation)

### 5.1 IPFS — original paper and recent overviews

- **Source:** https://research.protocol.ai/publications/ipfs-content-addressed-versioned-p2p-file-system/ (Benet, 2014)
- **What it is:** The canonical academic/technical paper introducing IPFS — content-addressing as a primitive, Merkle DAG for versioning, Kademlia DHT for discovery, BitTorrent-inspired block exchange.
- **Why Sunfish cares:** Spec §3.7 is built on IPFS's semantics. Reading the original paper is useful for understanding the *design choices* behind CIDs, chunking strategies, and the DHT — things the practical IPFS documentation glosses over.
- **Fit:** Foundational reference. Already covered in depth in `ipfs-evaluation.md`.
- **Recommendation:** **Design reference — read the paper.** The evaluation note `ipfs-evaluation.md` is the pragmatic analysis; the paper is the theoretical grounding.

---

## 6. BIM / IFC (spec §9 BIM integration)

### 6.1 IFC — Industry Foundation Classes

- **Source:** https://technical.buildingsmart.org/standards/ifc/ifc-schema-specifications/
- **What it is:** Open standard for 3D building information models, maintained by buildingSMART International. Defines an EXPRESS schema covering building elements (walls, doors, HVAC, structural, spatial hierarchy, properties) plus serialization formats (STEP, XML, JSON, ifcOWL). IFC 4.3.2 is the latest as of 2026; IFC 5 is in development with modernized modeling.
- **Why Sunfish cares:** Spec §9 positions BIM as an optional enrichment layer. IFC is the only open BIM interchange format with meaningful industry traction — Revit, ArchiCAD, Allplan, Tekla, and other major BIM tools all export IFC. If Sunfish's property entities reference BIM models, IFC is the format.
- **Fit:** **Adopt as the BIM interchange format.** Sunfish stores IFC files as content-addressed blobs (§3.7); property entities hold CID references. IFC-JSON is the preferred serialization for Sunfish's needs because it's diff-friendly, streaming-friendly, and directly parseable from .NET without a specialized EXPRESS parser.
- **.NET parsing landscape:**
  - `Xbim Toolkit` (MIT, actively maintained) — the reference .NET IFC toolkit. Supports IFC 2x3, 4.x, 4.3. Can read STEP, XML, JSON.
  - `IFC.NET` alternatives exist but are less maintained.
- **Recommendation:** **Adopt IFC + Xbim Toolkit** for the BIM enrichment layer. Spec §9 should explicitly name IFC 4.3.2 as the primary format and Xbim as the canonical .NET parser.

---

## 7. Quick reference — mapping to spec sections

| Spec section | External references | Recommendation category |
|---|---|---|
| §3.4 Schema Registry | JSON Schema 2020-12 | **Adopt** (format) |
| §3.5 Permission Evaluator | OpenFGA (ReBAC) | **Strong candidate** (evaluator model) |
| §3.6 Event Bus | MassTransit, Wolverine, Temporal | **Candidates** (transport / durable exec) |
| §3.7 Blob Store | IPFS paper, IPFS stack | **Adopt semantics** (see `ipfs-evaluation.md`) |
| §6 Property Management MVP | Typeform AI, Formstack, Feathery, Budibase | **Design references** (UX, conditional logic, workflow composition) |
| §7 Input Modalities | Typeform AI (form authoring) | **Design reference** |
| §9 BIM Integration | IFC 4.3.2, Xbim Toolkit | **Adopt** (format + parser) |
| §10 Delegation | OpenFGA + Keyhive (see `automerge-evaluation.md`) | **Layered combo** |
| Phase 5 blocks-forms | Typeform, Formstack, Feathery | **Design references** |
| Phase 5 blocks-tasks | Pega case lifecycles, Temporal durable exec | **Design reference** + **candidate** |
| Phase 9 Bridge event bus | MassTransit, Wolverine | **Candidates** (both viable) |

---

## 8. Follow-up actions

Each of these should flow back into the spec or phase plans:

- [ ] Spec §3.4 — cite JSON Schema 2020-12 as the required format; link `JsonSchema.Net` as the .NET reference parser
- [ ] Spec §3.5 — revise PolicyL description to reference OpenFGA's authorization-model DSL as the syntactic model; document the Keyhive+OpenFGA composition
- [ ] Spec §3.6 — add MassTransit, Wolverine, and Temporal as candidate `IEventBus` / durable-execution backends; clarify that workflow-block execution (long-running) is distinct from kernel event publishing (short-running transport)
- [ ] Spec §6 — add design-reference citations (Typeform AI for form authoring UX, Formstack for approval-chain editor, Feathery for conditional-logic catalog)
- [ ] Spec §9 — specify IFC 4.3.2 + Xbim Toolkit as the canonical BIM stack
- [ ] Phase 5 `blocks-tasks` plan (`2026-04-17-sunfish-phase5-domain-blocks.md`) — update to cite Pega's case-lifecycle model as the reference for the task-block primitive; note Temporal as a candidate for durable execution
- [ ] Phase 5 `blocks-forms` plan — cite Typeform AI + Formstack + Feathery; add AI-assisted form authoring to the parking-lot items
- [ ] Phase 9 ROADMAP — mention MassTransit as an alternative to Wolverine for cross-accelerator messaging

These follow-ups are not blocking; they're editorial revisions that can be applied when the corresponding sections are next opened for change.

---

## 9. Sources

### Forms
- [Typeform AI FAQ](https://help.typeform.com/hc/en-us/articles/33777155298708-AI-with-Typeform-FAQ)
- [Formstack Workflows](https://www.formstack.com/features/workflows)
- [Feathery conditional logic docs](https://docs.feathery.io/platform/build-forms/logic/available-conditions)
- [Budibase open-source form builder](https://budibase.com/blog/open-source-form-builder/)

### Workflow
- [Pega Academy — Child cases (v5)](https://academy.pega.com/topic/child-cases/v5)
- [Temporal — Durable execution in distributed systems](https://temporal.io/blog/durable-execution-in-distributed-systems-increasing-observability)
- [MassTransit](https://masstransit.io)

### Authorization
- [OpenFGA](https://openfga.dev)

### Schema
- [JSON Schema specification](https://json-schema.org/specification)

### Content addressing
- [IPFS — Content-Addressed, Versioned, P2P File System (Benet, 2014)](https://research.protocol.ai/publications/ipfs-content-addressed-versioned-p2p-file-system/)

### BIM
- [IFC schema specifications (buildingSMART)](https://technical.buildingsmart.org/standards/ifc/ifc-schema-specifications/)
