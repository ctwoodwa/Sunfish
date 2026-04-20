# Product Vision

**Status:** Accepted
**Last reviewed:** 2026-04-20
**Companion docs:** [architecture-principles.md](architecture-principles.md), [roadmap-tracker.md](roadmap-tracker.md), [compatibility-policy.md](compatibility-policy.md).

## What Sunfish is

**Sunfish is an OSS full-stack platform for building local-first, federation-capable software — a component library, a set of reusable domain blocks, and a multi-tenant SaaS shell you can run on a laptop today and a cloud cluster tomorrow.** It exists so that a technical founder, a small operator, or a domain expert with an AI-savvy collaborator can ship a real product without paying platform rent, surrendering their data to a third party, or rewriting their stack when they outgrow the kitchen-table phase.

One stack. Three shapes: component library, domain blocks, platform. Three deployment modes: desktop, self-hosted, hosted SaaS. All from one codebase.

## Why now

Three forces converge in 2026:

1. **AI is collapsing SaaS margins.** Anyone with an AI assistant and a weekend can replicate the CRUD layer that used to justify a $49/seat/month subscription. The moat is no longer the code — it's the domain knowledge, the integrations, and the hosting. Everything else is becoming commodity. Sunfish is designed for the world after that shift: the code is free because the code can't be the moat anymore.
2. **The local-first movement has matured.** Martin Kleppmann's *Local-First Software* essay, the [offlinefirst.org](https://offlinefirst.org) community, and production CRDT libraries (Automerge, Yjs) have moved "your data is yours" from ideology to engineering practice. Federation and cryptographic capability delegation are no longer research topics — they're libraries you can integrate.
3. **The industry has more high-quality open-source building blocks than ever before** — Web Components, WebAssembly, typed language toolchains (C#/.NET, Rust, TypeScript, Go), CRDTs, Verifiable Credentials, capability systems, JSON Schema, OpenFeature, LLM-assisted coding. But every team reimplements the integration because no one has assembled these pieces into a coherent, opinionated stack. Sunfish's contribution is not new primitives. **Sunfish's contribution is consolidation** — collecting best-in-class open-source ideas, wiring them together with strong defaults, and keeping every extension seam open so the landscape can continue to evolve.

AI makes building Sunfish economically viable on a small budget. The local-first movement gives the architectural vocabulary to build it honestly. The industry gives the pieces to consolidate. All three were missing or incomplete five years ago. This window is why Sunfish exists now.

## Who Sunfish is for

Three audiences, layered:

### Technical founders building vertical SaaS

Senior developers and software principals who are going to ship a SaaS product for a specific business case — property management, medical offices, school administration, facility operations — and don't want to start from a blank repo. They use Sunfish as the stack beneath their product; their business is the vertical they know.

### Small operators who'd rather self-host

A small landlord, a small medical office, a small school district, a family-run services business. These operators have AI-savvy collaborators but not a full dev team. They want software that runs on hardware they own, with their data on their disk, and the option to hire someone when they need help.

### AI-savvy stakeholders on any team

People who aren't developers but know how to work with AI — product managers, domain experts, operators prototyping an idea. Commercial UI kits require a per-seat license that doesn't fit this audience (you don't buy Telerik for your marketing lead just so they can prompt Claude against your design system). Sunfish's OSS components remove that friction. When the prototype matures into a real app, the Telerik-compatible surface means the team can upgrade to a commercial license if they want — no rewrite.

## Five first-class pillars

### 1. Local-first by default

Every Sunfish bundle works offline on a single device. Sync is the mechanism that makes multi-device and multi-user possible, not a fallback when the network's down. Sunfish adopts the seven ideals from Kleppmann's *Local-First Software*:

1. No spinners — the UI is fast because reads are local.
2. Work offline — full read and write without a network.
3. Sync across devices — a phone, a laptop, a server all see the same data eventually.
4. Collaboration — multi-user without a central server being mandatory.
5. Longevity — your data outlives any vendor, including Sunfish.
6. Privacy and security by default — encryption and ownership are defaults, not features.
7. User retains ultimate ownership and control — export is a first-class operation.

This isn't aspiration. `Sunfish.Foundation.LocalFirst` (ADR 0012) is the contract surface that every bundle and accelerator implements against. Hosted SaaS mode is one deployment of local-first software, not a separate architecture.

### 2. Federation as foundation

Real-world operators cross organizational boundaries. A small landlord's inspector shares data with a city's code-enforcement office. A small medical office exchanges records with a specialist across town. A school district files reports with a state education agency. A construction team coordinates a building model across architects, engineers, and the facility owner. A business files tax returns with the IRS. These flows exist today — routed through email attachments, paid-per-seat clearinghouses, and proprietary gateways. Each vertical has evolved its own record-exchange standards; Sunfish is designed to speak them natively.

**Federation in Sunfish is not a replacement protocol.** It is the transport, trust, and audit substrate on which existing industry-standard record-exchange protocols ride. Peer-to-peer entity sync, cryptographic capability delegation, and CID-addressed blob replication (shipped in `federation-common`, `federation-entity-sync`, `federation-capability-sync`, `federation-blob-replication`) are protocol-agnostic — the same primitives carry FHIR resources, Ed-Fi entities, IFC models with BCF issues, and MeF tax packets.

Primary exchange standards per vertical, and what Sunfish contributes at the federation layer:

| Vertical | Primary exchange standards | What Sunfish contributes |
|---|---|---|
| Healthcare (small medical office → specialist → HIE → state agencies) | **FHIR R4 / R5** (HL7 REST API for clinical resources) · **USCDI v3** (mandatory by Jan 2026) / draft v7 · **TEFCA** via QHIN (national exchange framework — 500M+ records exchanged, 14k+ connected orgs) · **SMART on FHIR** (app launch / auth) · **DICOM** (imaging) | Federated peer-to-peer FHIR resource sync without mandatory QHIN gateway; capability-delegation-backed app authorization; signed audit trail that crosses node boundaries |
| Construction · BIM · facility management | **IFC 4.3** (ISO 16739-1:2024, the buildingSMART schema covering buildings + infrastructure) · **BCF** (BIM Collaboration Format for issue tracking across tools) · **COBie** (facility-management handover) · **IDS** (Information Delivery Specification) | CID-addressed blob replication for large IFC models; federated BCF topic exchange across design teams and owners; capability delegation for time-bound subcontractor access to specific model segments |
| Education (school district → state education agency → contractors) | **Ed-Fi Data Standard v6** (current state-of-the-art K-12 exchange, adopted by many SEAs for SY 2026-27+) · **SIF** (legacy vendor-neutral) · **CEDS** (Common Education Data Standards from NCES) | Federated Ed-Fi domain sync without each vendor running its own integration; capability delegation for contractor access with explicit time bounds (tutoring vendors, assessment partners); federated audit for compliance reporting |
| Government tax and regulatory filing | **IRS MeF** (Modernized e-File — per-tax-year XML schemas for 1040, 1120, 941, 990, 2290, 1099 series; multiple versions active concurrently) · **XBRL** (SEC financial reporting) · **NIEM** (cross-agency federal/state/local exchange model) | Federated envelope for filer → preparer → IRS submission with signed provenance; CID-addressed attachment replication; capability delegation for accountants / preparers with time-bound scope |
| Property management · real-estate operations | **MISMO** (mortgage data), **RESO** (real estate listings), mixed ad-hoc agency exchange (code enforcement, housing authority reporting) — no single dominant standard yet | Federated entity sync with code-enforcement agencies; capability delegation for inspectors and vendors; CID-addressed evidence (photos, inspection reports) with tamper-evident hash chains |

Sunfish's job at the federation layer is the **transport, trust, and audit substrate**. Implementing each vertical's wire format — FHIR endpoints, Ed-Fi domain APIs, IFC importers, MeF XML envelopes — is bundle-level work. The federation primitives are designed so that a `blocks-fhir`, `blocks-edfi-domain`, `blocks-bim-ifc`, or `blocks-irs-mef` module can slot in as a format adapter without reinventing peer discovery, delegation, or replication. This is how the same federation primitives serve every vertical: they handle identity, trust, sync, and blob storage; bundles handle wire format.

#### Credentialing and professional authority

Every vertical also needs to verify **authority**, not just data: a lease signed by an attorney in good standing, an engineering drawing sealed by a licensed PE, a prescription written by a DEA-registered provider, a diploma conferred by an accredited institution. Credentials flow in a distinct three-party pattern — **issuer → holder → verifier** — typically with the issuer offline at verification time. Sunfish's cryptographic capability delegation is a natural substrate for this model and aligns directly with W3C's **Verifiable Credentials 2.0** (published as a W3C Standard on 2025-05-15) and **Open Badges 3.0** / **Comprehensive Learner Record 2.0** (1EdTech, both built on VC 2.0).

| Profession | Credentialing authorities and standards | What Sunfish adds |
|---|---|---|
| Medical providers | **NPI** (federal provider ID) · **CAQH ProView** (de facto US credentialing database — 80% of US physicians, accepted in all 50 states, 900+ health plans, 120-day re-attestation cycle) · state medical boards · **DEA** registration (controlled substances) · **ABMS** board certifications · **FHIR Practitioner / PractitionerRole** resources | Federated credential verification without mandatory routing through a centralized clearinghouse; delegation chains linking a provider's credentials to the FHIR resources they author |
| Professional engineers | **NCEES Record** (interstate portability of PE credentials) · state engineering boards · PKI-based digital seals (state rules vary; seal must be unique to the PE, under exclusive control, document-linked, and tamper-detectable) | VC-2.0-backed engineering seals that travel with the sealed document; revocation propagation when a license lapses or is suspended |
| Attorneys | State bar databases (44 states + DC publish online status) · **ABA**-accredited JD programs · **NCBE** Uniform Bar Exam · ABA National Lawyer Regulatory Data Bank (multi-jurisdictional discipline) · **PACER** for federal filings | Federated bar-verification proofs bound to filed documents; time-bound delegation for paralegals and co-counsel; cross-jurisdiction discipline propagation |
| Academic credentials (undergraduate, graduate, professional) | **W3C Verifiable Credentials 2.0** (2025 W3C Standard) · **Open Badges 3.0** (1EdTech, VC-2.0-based) · **CLR 2.0** (transcripts as bundled VCs) · **PESC** (legacy JSON transcript format from 2019) · institutional and specialty accreditors (**ABET** for engineering, **LCME** for medicine, **AACSB** for business, **ABA** for law, **CCNE** for nursing) | OSS issuer, holder, and verifier nodes running on federation primitives; interoperability with any VC-2.0-compatible credential wallet; accreditor-signed institutional credentials |
| Skilled trades (electrical, plumbing, HVAC, general construction, solar, fire / life safety) | State and municipal occupational licensing boards (each state licenses separately) · **NATE** (HVAC technician excellence) · **EPA Section 608** (refrigerant handling — federal) · **NICET** (electrical, fire alarm, water-based systems) · **NABCEP** (solar / renewable-energy practitioners) · **NEC / NFPA** (electrical code compliance) · **OSHA 10 / OSHA 30** (safety training) · **LEED** / green-building credentials · union journeyman cards (IBEW, UA, IUPAT) · contractor insurance certificates (COI) and bonds | Federated vendor-verification proofs attached to work orders; capability delegation for time-bound property access (e.g., day-of-job lockbox codes); diligence-backed onboarding that aggregates license + insurance + COI + bond status into a signed credential bundle |

For small landlords and property managers, vendor onboarding is where credentialing becomes day-to-day work: every new electrician, plumber, HVAC tech, or contractor added to the system triggers a due-diligence flow — license verification, insurance-certificate confirmation, bond status, optional background check (residential access), references, performance history. The PM bundle composes these using `blocks-diligence` (checklist + evidence + approval) and a future `blocks-vendors`; federation carries the signed credential bundle between the authority (state board, NATE, insurance issuer) and the landlord's Sunfish node so verification doesn't require the landlord to chase paperwork manually. The same pattern applies when a hospital onboards a locum physician, a law firm engages co-counsel, or a school district contracts a tutoring vendor — vendor due diligence is a cross-vertical concern that rides on the credentialing primitives.

Credentials and records share the same federation primitives. A signed FHIR clinical document carries the provider's credential chain as a delegation proof; a sealed engineering drawing carries the PE's NCEES record; a filed pleading carries the attorney's bar status; a university transcript is a CLR 2.0 bundle of Open Badges 3.0 credentials. The underlying problem — trustably exchanging artifacts across organizational boundaries, with provenance — is the same for data and for authority, so the primitives are the same.

Federation is invisible when you don't need it. A solo user sees a local app. Two nodes see sync. A many-node mesh sees federation across organizations — whatever format they speak, whatever credentials they carry.

### 3. Framework-agnostic UI with commercial upgrade paths

Sunfish's UI Core is headless and framework-agnostic (ADR 0014). **The component library is designed to integrate with any front-end UI** — UI Core contracts carry no rendering assumptions, and adapter packages (`ui-adapters-*`) implement those contracts for a specific framework. **Blazor is the first adapter shipped** because it's closest to Sunfish's .NET core and the framework the founding team knows best. **React is the next planned adapter** because it dominates the broader front-end industry. Angular, Vue, Web Components, .NET MAUI, Avalonia, and others are open targets for the community or commercial services to build once UI Core proves out through the first two. Within each adapter, provider themes (FluentUI, Bootstrap, Material) give visual variety without changing component contracts.

**At the technical layer, Sunfish's components are authored as Web Components** — Custom Elements, Shadow DOM, HTML Templates, ES Modules. Web Components are the W3C-standard primitives that let a component run in any framework's render tree without modification. A Sunfish Custom Element works natively in Blazor (via JS interop), React (via property / attribute passing), Angular, Vue, Web-Components-only apps, and plain HTML pages — because it is a browser primitive, not a framework abstraction. Adapter packages provide idiomatic wrappers (typed props, framework-native event wiring, lifecycle integration), but the underlying component is the same Web Component everywhere. This is what makes "any front-end UI" a technical reality rather than a marketing promise: the same code, literally, runs in every adapter.

Browser-platform features shipped or shipping through 2026 strengthen this further. **Declarative Shadow DOM** (HTML-authored shadow trees via the `shadowrootmode` attribute, no runtime JavaScript required) enables server-rendered components — natural for Blazor Server's rendering model and for any SSR-first app. **Scoped Custom Element Registries** (enabled by default in Chromium-based browsers in 2026) prevent collisions when multiple component libraries coexist: a bundle composing components from Sunfish, a third-party provider, and a customer's own library can all register without naming conflicts.

**Two consumption tracks: JavaScript and WebAssembly.** Web Components authored once serve both major tracks for typed code in the browser:

- **WASM track** — compiled typed languages consuming Web Components in the browser. **Blazor** (C# / .NET compiled to WebAssembly) is the reference adapter because it's closest to Sunfish's .NET core and the founding team's depth. **Rust** via `wasm-bindgen` or `yew` is an equally valid path. Other typed-language-to-WASM toolchains (Go via TinyGo, Kotlin/Wasm) are supported wherever they produce viable browser consumption. Blazor is the best-known example of this track; it is not the only one.
- **JavaScript track** — React, Angular, Vue, Solid, and vanilla TypeScript consumers use Web Components through their framework's standard integration (React via props / refs, Angular via native element support, Vue via v-model).

**Typed code is the default across both tracks** — TypeScript on the JS side, C# / Rust / Go on the WASM side. Untyped JavaScript is not rejected, but every canonical example, template, and piece of documentation Sunfish ships assumes typed code. This is an opinionated stance: type systems matter for correctness, for AI code generation quality, and for the "other people can take it further" commitment.

**Polyglot server participation via API layer.** Services written in languages that can't reach Web Components directly — Python, Ruby, Java, older Go, legacy .NET Framework, anything server-side — participate through Sunfish's HTTP / gRPC / WebSocket API surfaces. The API layer is the universal participation mechanism; a language does not have to compile to WASM or run in the browser to be part of a Sunfish deployment.

**Reach extends beyond the desktop browser.** Because Web Components are a browser primitive, anything that ships a WebView can host them — which in 2026 means every major mobile and desktop platform. Concrete delivery paths:

- **PWAs** (Progressive Web Apps) — installable on iOS, Android, Windows, macOS, and Linux; service-worker offline support aligns with Pillar 1; the lightest-weight distribution path and the one Sunfish targets by default.
- **.NET MAUI Blazor Hybrid** — native iOS, Android, Windows, and macOS apps that render Razor (wrapping Web Components) via `BlazorWebView`; the natural fit for Sunfish's .NET-heavy stack and the strongest single-codebase mobile + desktop path for teams already in .NET.
- **Tauri 2** (released January 2025) — Rust-backed cross-platform desktop *and* mobile shells with a very small footprint; good for lean standalone distributions of a Sunfish-built app.
- **Capacitor** (Ionic) — mature web-to-native wrapper for iOS / Android hybrid apps with a deep plugin ecosystem for camera, GPS, biometrics, push, file system.
- **Electron** — traditional desktop shell where a heavier JavaScript runtime is already part of the team's tooling.

A Web Component authored once is deliverable through any of these. Provider themes (Pillar 3, continued below) adapt visual weight per screen class — touch-target sizes meeting WCAG 2.2 AA (≥24×24 CSS px) on mobile, compact density on desktop — without the component code changing. This is how Sunfish's single codebase literally serves the deployment growth path: the same component runs on a small landlord's iPhone in a WebView-based mobile shell, on their desktop for office work, and on a staff laptop hitting a hosted Bridge SaaS tenant.

Uniquely, Sunfish ships with **compatibility surfaces for leading commercial UI vendors** — API-compatible shims that let code written against a commercial vendor's public API run equivalently against Sunfish's OSS components. The first is [compat-telerik](../../packages/compat-telerik/) for **Progress Telerik**, which focuses on .NET technologies (Blazor, ASP.NET, WPF, WinForms). Planned additions include **Progress Kendo UI** — the parallel product line from the same parent company, covering JavaScript frameworks (React, Angular, Vue, jQuery) — along with **Infragistics Ignite UI** (100+ components spanning Blazor, Angular, React, Web Components), **Syncfusion Essential Studio** (enterprise-ready suites for .NET, Blazor, and JavaScript frameworks), **Oracle JET** (custom web components and grouped JET Packs), and other best-in-class commercial vendors over time. Each vendor line gets its own `Sunfish.Compat.<Vendor>` package.

The workflow this enables: prompt AI against the OSS components to build a prototype — no per-seat commercial license required for AI-savvy stakeholders, PMs, or domain experts on the team. When the product ships, optionally license whichever commercial library fits your needs and swap providers; your code mostly doesn't change. Commercial UI vendors are not competitors — they are upgrade paths for teams that want commercially-supported components for specific screens or deeper component breadth than the OSS core provides. This removes the friction that per-seat commercial licenses impose during the AI-driven prototyping phase.

### 4. AI-native development as a cost primitive

Sunfish is built *with* AI because AI is what makes the business model viable. A platform this ambitious couldn't be built in a small-team budget five years ago. It can now — and the same AI leverage is available to anyone extending Sunfish. This is why the stack is OSS: code is no longer the scarce resource; judgment, domain knowledge, and execution are. We give away the code because withholding it would waste its most valuable property — that other builders can take it further.

**AI-generated artifacts are first-class platform inputs, not just a development tool.** Outputs like [Claude Artifacts](https://www.anthropic.com/news/artifacts) — code snippets, Markdown documents, HTML fragments, SVG images, Mermaid diagrams, React components, Web Component definitions — are consumable by Sunfish bundles and templates. A report template can embed an AI-generated Mermaid diagram. A form can be scaffolded by prompting an assistant against the OSS components and dropped into a bundle's template registry. A custom component authored as a Web Component artifact can be registered alongside reference components and styled via the active provider theme. Artifacts travel between users with provenance and capability delegation like any other Sunfish entity — the AI-assistant remix pattern (modify, build upon, share) extends from the chat window to production surfaces without changing form. The same OSS component library that AI prompts against during prototyping is the library that runs the shipped product, so artifacts produced by AI during the design phase keep working in production.

### 5. Inclusive by default — accessibility and internationalization

Every operator running Sunfish serves people who don't share the operator's language, culture, or physical and sensory abilities. A small medical office's patients speak different languages; a school district's parents read at different levels; a small landlord's tenants use screen readers; a state agency must meet statutory accessibility regulations. These are baseline reality, not edge cases. Sunfish treats accessibility and internationalization as **non-negotiable platform commitments** wired into UI Core contracts, components, templates, data models, and deployment modes — not opt-in features or enterprise-edition upsells.

**Accessibility.** The baseline target is **WCAG 2.2 AA** across the UI Core component library and every provider theme. Every component's public contract specifies its ARIA role and attributes, its keyboard interaction map (every action reachable without a pointing device), its focus behavior (initial focus, focus-trap for overlays, focus-restore on dismissal), its screen-reader expectations, its reduced-motion support (`prefers-reduced-motion` honored), and color-contrast ratios that meet the target in every provider — including dark mode. Automated accessibility testing (axe-core or equivalent) is part of the component test harness; an accessibility regression is a build failure, not a backlog item.

**Internationalization and culture.** Sunfish is Unicode end-to-end. Every component, template, notification, report, and document template carries an implicit or explicit locale, resolved per-tenant with per-user overrides using BCP-47 tags. Translated UI strings ship as resource files alongside each component family; tenants can override strings per locale without forking. Locale-aware formatting for dates, numbers, currency, and sort order flows through standard .NET globalization. **RTL layout** for Arabic, Hebrew, Persian, and other right-to-left scripts is handled automatically by every layout component — no per-component special-casing. **Pluralization** uses ICU MessageFormat where plural semantics matter. **Timezones are always explicit**; datetimes are never stored or rendered naively. Template overlays (ADR 0005) add languages without forking the base template — a tenant in Québec can ship a French variant of a form without the English base shifting underneath them.

**Regulatory alignment follows naturally.** Section 508 and ADA for US government customers (including the first commercial school-district customer), EN 301 549 for EU, and AODA / provincial regulations for Canadian deployments. Commercial accessibility audit and remediation services are available through Sunfish for customers needing formal third-party attestation.

## Three entry points

Consumers adopt Sunfish at whichever layer fits their need today and move deeper as requirements grow.

### Entry 1 — Components only

"I'm prototyping a UI with AI and I want reusable, well-tested, theme-able components."
→ Add `Sunfish.UIAdapters.Blazor` + a provider to a project. Use what you need. Ignore the rest of the stack.

### Entry 2 — Domain blocks

"My prototype is becoming a real app. I need leases, tasks, scheduling, documents, workflow — not just UI widgets."
→ Add `blocks-*` modules that fit your domain. Compose them into whatever application shell you prefer.

### Entry 3 — Full platform (Bridge + bundles)

"I'm running a multi-tenant product. I need tenant provisioning, subscriptions, bundle activation, admin backoffice."
→ Use Bridge as the shell. Pick a bundle (or author one). Run on your laptop, your office server, or a managed cloud tenant.

## Reference verticals

Bundles ship to validate the platform against real operators. Current and planned:

| Vertical | Status | Driver |
|---|---|---|
| Property Management | First reference bundle shipped | Small-landlord operator needs |
| Small Medical Office | Planned (P2/P3) | Independent practice needs |
| School District | Planned (P3/P4) — **first commercial customer** | Federation-heavy (cross-org data sharing) |
| Asset Management | Manifest shipped | Fleet / equipment / IT-hardware operators |
| Project Management | Manifest shipped | Client-services and consulting shops |
| Facility Operations | Manifest shipped | Campuses, labs, commercial buildings |
| Acquisition / Underwriting | Manifest shipped | Deal-flow and diligence teams |

The school-district customer is what justifies building federation as a first-class pillar, not a future option. Other verticals benefit from federation opportunistically.

## How Sunfish grows — extensibility at every layer

Sunfish cannot solve every problem in every vertical. The industry changes faster than any single platform can ship features, and users know their own domain better than we ever will. Rather than pursuing comprehensiveness, Sunfish is designed so that **every capability can be added, replaced, or extended at every layer** — what ships in the open-source core is a deliberate minimum, and the parts a customer keeps are the parts that serve them. Community contributors, customers, and commercial service providers can *add* what Sunfish doesn't ship, *swap out* what Sunfish does ship, or *blend* the two — without reaching into Sunfish itself.

| Layer | Extension mechanism | What anyone can add without touching Sunfish core |
|---|---|---|
| Business case | `BusinessCaseBundleManifest` JSON seed | New bundles for verticals Sunfish doesn't ship (dental practice, NGO management, fleet logistics, veterinary, hospitality, agricultural operations) |
| Domain module (new or replacement) | `blocks-*` package — add a new module or ship an alternative implementation of an existing module's contracts | New bounded contexts (`blocks-prescriptions`, `blocks-legal-filings`, `blocks-insurance-claims`, `blocks-vendor-qualification`); **or** a swap of Sunfish's reference implementation — for example, replace `blocks-accounting` with an OSS alternative (Akaunting, Frappe Books, ERPNext accounting) or a commercial adapter (QuickBooks Online, Xero, Sage, NetSuite). Same pattern for any `blocks-*`. |
| Provider adapter | `Sunfish.Providers.*` package implementing `Foundation.Integrations` contracts | New external services (payment gateways, messaging, banking feeds, channel managers, AI backends, credential authorities) |
| Feature evaluator | `IFeatureProvider` (OpenFeature-style seam) | Flag backends (LaunchDarkly, flagd, Statsig, custom in-house) |
| UI adapter | `ui-adapters-*` package | New rendering frameworks of any kind — React, Angular, Vue, Web Components, .NET MAUI, Avalonia, Flutter, native mobile — any stack that can consume the UI Core headless contracts |
| UI theme | `Providers/*` inside an adapter | New visual systems (Tailwind, shadcn, custom corporate theme) |
| Commercial UI compatibility surface | `Sunfish.Compat.<Vendor>` package mirroring the vendor's public API | New compat shims for commercial UI vendors (Telerik today; Kendo UI, Infragistics, Syncfusion, Oracle JET, DevExpress, ComponentOne, and others planned) |
| Icon set | `Sunfish.Icons.*` package | Any icon system |
| Entity extension fields | `IExtensionFieldCatalog` registration | Per-tenant extension fields on canonical entities, stored as JSON or promoted to columns |
| Templates | JSON Schema + UI schema + tenant overlay | Custom forms, diligence checklists, reports, document templates, notification bodies |
| Sync engine | `ISyncEngine` contract | Alternative sync implementations (client-server REST, WebRTC, CRDT via Automerge or Yjs, federation) |
| Conflict resolver | `ISyncConflictResolver` per entity class | Domain-appropriate merge strategies beyond last-writer-wins |
| Credential issuer or verifier | Verifiable Credentials 2.0 / Open Badges 3.0 over federation primitives | New credential types for any profession, trade, or program |

**Replacement, not just addition.** The same principle that governs front-end frameworks — any UI stack is a legitimate target — governs the rest of the stack too. If a customer wants to replace Sunfish's accounting with QuickBooks Online, their document store with SharePoint, their messaging with Twilio, their feature flags with LaunchDarkly, their scheduling with a team's existing Google Calendar, or their bundle of blocks with an entirely custom composition — they plug in an adapter, not fork the project. Each `blocks-*` module ships **two things**: a set of domain contracts (interfaces, records, events) and a reference OSS implementation. Bundle manifests reference the module *key*, not a specific implementation; a tenant's configuration picks which implementation runs. The reference implementation is the default; it is never the only option.

The whole stack ships OSS because code is no longer the scarce resource — judgment, domain knowledge, and execution are. Making it easy for outside developers to extend what's built here is Sunfish's most important design commitment. No feature on the roadmap matters more than keeping extension points healthy.

**No locked-away extensibility.** Every extension point above is available in the free OSS release. Commercial services help customers *use* extensions, not *gain access to them*.

## Deployment growth path

Sunfish supports the progression from kitchen table to enterprise without a re-platform:

| Stage | Deployment | Who runs it |
|---|---|---|
| Solo | Desktop app / local Podman or Docker on a personal PC | The operator themselves |
| Small team | Self-hosted on a small server or office NUC | The operator or a local IT contractor |
| Growing business | Self-hosted on cloud VMs, still tenant-controlled | The operator with light ops help |
| Fully hosted | Managed SaaS tenant on Bridge-operated infrastructure | Sunfish's commercial services, or a third party |

The same bundles run at every stage. Migration between stages is a data-export + data-import event, not a rewrite. This is the commitment Kleppmann's ideal #7 requires.

## Business model

**OSS, free and unrestricted.** The full stack — components, blocks, bundles, Bridge, federation, local-first — ships under a permissive license. No feature-gated enterprise edition. No "community" version with deliberate limits. The thing you run in production is the thing anyone can fork.

**Commercial revenue comes from services.** Specifically:

- **Managed SaaS hosting** for operators who don't want to self-manage (a small medical office that would rather pay for a running instance than learn Docker).
- **Implementation help** — setup, integration with existing systems, custom bundle authoring.
- **Federation onboarding** — getting a school district's node talking to a state agency's node.

The rule: **we make money when we add value.** If a customer can self-host competently, we want them to — not because we're purists, but because forcing them into a managed tier would be charging for nothing. When they need us, we're there. When they don't, the OSS stack is complete enough that they don't.

AI is the cost primitive that makes this economic. A two-person services company using AI can support customers a traditional 20-person SaaS vendor couldn't.

## What Sunfish is not

- **Not a low-code platform.** Authoring speed isn't the pitch. Depth of domain, production-readiness, and data ownership are.
- **Not a vertical-SaaS product.** Sunfish isn't the PM product competing with AppFolio on feature count. It's the stack someone uses to build such a product — and maybe that someone is you.
- **Not a framework lock-in.** Framework-agnostic UI, provider-swappable themes, commercial upgrade paths via compat surfaces (Telerik today; Infragistics, Syncfusion, Oracle JET, Kendo UI, and others planned), permissive OSS license.
- **Not a cloud lock-in.** Every deployment mode works without a specific cloud vendor. Bridge runs on Aspire + Postgres; substitute what you have.
- **Not a feature-complete-everything.** Sunfish ships the platform pieces every vertical SaaS needs — tenancy, bundles, local-first sync, federation, UI core. Vertical-specific features live in bundles authored by whoever cares about that vertical.

## What winning looks like

Milestones, not forecasts. Each is a binary check — did it happen, or not.

### 1 year out (2027)

- A small-landlord operator is running the Property Management bundle on their own hardware, syncing to a mobile device in the field.
- A small medical office is piloting the Medical bundle (scaffolded; not yet shipped).
- The first commercial school-district customer is in onboarding, driving federation design concrete.
- Sunfish has an active OSS contributor footprint beyond the founding team.

### 3 years out (2029)

- Three or more reference bundles are in GA (PM, Medical, School District at minimum).
- Third-party developers are publishing their own bundles for verticals Sunfish hasn't touched.
- A handful of technical founders run sustainable services businesses on Sunfish for verticals each knows well.
- The AI-native development workflow is documented well enough that non-team contributors can extend a bundle without onboarding help.

### 5 years out (2031)

- Sunfish is a recognized default stack for technical founders shipping vertical SaaS.
- The OSS ecosystem (bundles, providers, adapters) is broader than what the founding team could have built alone.
- Federation is shipping real cross-organizational data flow in at least one vertical (school districts, code enforcement, multi-office healthcare, or similar).
- The commercial services business is sustainable without requiring Sunfish to become a traditional SaaS vendor.

## Principles behind the vision

These are the commitments the rest of the platform is built to honor. They're repeated in [architecture-principles.md](architecture-principles.md) with ADR citations:

- **Framework-agnostic UI.** The code you write today runs on your chosen renderer tomorrow.
- **Data ownership.** Export is a first-class operation at every layer, every mode.
- **Provider neutrality.** Every vendor (Stripe, Plaid, Twilio, IPFS, Finbuckle) is behind a Sunfish contract. Swap is a configuration change, not a rewrite.
- **Progressive deployment.** The same bundle runs on a laptop, on a Podman box in your office, and on a managed cloud tenant.
- **Bundle composition.** Business cases are declarative manifests. Switching a tenant from one bundle to another is configuration, not migration.
- **AI as force multiplier.** The stack is designed to be extended by humans-with-AI, not humans-alone. Documentation, manifests, and typed contracts are optimized for AI comprehension.
- **Accessibility by default.** Every UI component carries a documented accessibility contract. WCAG 2.2 AA baseline across the component library and every provider theme; regressions fail the build.
- **Culture and language as platform primitives.** Unicode end-to-end, BCP-47 locale tags, per-tenant and per-user overrides, RTL and pluralization support built into every layout and every template.
- **Typed code by default.** C# on .NET, TypeScript on JS, Rust in WASM — Sunfish's own code and the canonical examples it ships use typed languages. Untyped JavaScript exists at the edges (tenant-authored scripts, AI-generated one-off snippets) but never in Sunfish's core or reference examples.
- **Consolidation over invention.** Sunfish's contribution is the opinionated wiring of industry-best open-source building blocks — Web Components, WASM, CRDTs, Verifiable Credentials, OpenFeature, JSON Schema, Lit, Automerge, and the rest — not new primitives. Every extension seam stays open so the landscape can keep evolving faster than any single platform can ship features.
- **Open, transparent decision-making.** Sunfish adopts Red Hat's [Open Decision Framework](https://github.com/red-hat-people-team/open-decision-framework) for governance, Primeline AI's [Universal Planning Framework](https://github.com/primeline-ai/universal-planning-framework) for plan rigor, and Sunfish's own ICM pipeline for workflow. Problems, constraints, and criteria are shared publicly before decisions close; plans pass a 21-pattern anti-pattern scan before ratification. See [`GOVERNANCE.md`](../../GOVERNANCE.md).

## Cross-references

- [architecture-principles.md](architecture-principles.md) — the structural commitments this vision requires.
- [roadmap-tracker.md](roadmap-tracker.md) — milestone and phase status.
- [compatibility-policy.md](compatibility-policy.md) — pre-1.0 and post-1.0 commitments that keep the vision honest over time.
- [`docs/adrs/0006-bridge-is-saas-shell.md`](../../docs/adrs/0006-bridge-is-saas-shell.md) — Bridge as shell, not vertical.
- [`docs/adrs/0012-foundation-localfirst.md`](../../docs/adrs/0012-foundation-localfirst.md) — local-first as a first-class contract surface.
- [`docs/adrs/0014-adapter-parity-policy.md`](../../docs/adrs/0014-adapter-parity-policy.md) — multi-adapter framework-agnostic commitment.
- [Martin Kleppmann, *Local-First Software: You Own Your Data, in spite of the Cloud*](https://www.inkandswitch.com/local-first/) — the seven ideals we build against.
- [offlinefirst.org](https://offlinefirst.org) — the offline-first community and reference practice.

### Industry exchange standards referenced in the Federation pillar

- [**HL7 FHIR**](https://www.hl7.org/fhir/) — healthcare interoperability, R4 (certification baseline) and R5 (emerging).
- [**USCDI**](https://isp.healthit.gov/united-states-core-data-interoperability-uscdi) — US Core Data for Interoperability, the mandated data-element set.
- [**TEFCA**](https://rce.sequoiaproject.org/) — Trusted Exchange Framework and Common Agreement; national US health-data exchange.
- [**SMART on FHIR**](https://smarthealthit.org/) — app-launch and authorization spec layered on FHIR.
- [**IFC (ISO 16739-1:2024)**](https://technical.buildingsmart.org/standards/ifc/) — Industry Foundation Classes, the open BIM data schema.
- [**BCF**](https://technical.buildingsmart.org/standards/bcf/) — BIM Collaboration Format for model-based issue exchange.
- [**COBie**](https://www.nibs.org/projects/cobie) — Construction-Operations Building information exchange (facility-management handover).
- [**Ed-Fi Data Standard**](https://www.ed-fi.org/ed-fi-data-standard/) — K-12 state/district/vendor interoperability; v6 for SY 2026-27+.
- [**CEDS**](https://ceds.ed.gov/) — Common Education Data Standards from NCES.
- [**IRS MeF**](https://www.irs.gov/e-file-providers/modernized-e-file-mef-schemas-and-business-rules) — Modernized e-File XML schemas, per tax year and form.
- [**XBRL**](https://www.xbrl.org/) — eXtensible Business Reporting Language for financial / regulatory filings.
- [**NIEM**](https://www.niem.gov/) — National Information Exchange Model for cross-agency US government exchange.
- [**MISMO**](https://www.mismo.org/) — Mortgage Industry Standards Maintenance Organization (real-estate lending data).
- [**RESO**](https://www.reso.org/) — Real Estate Standards Organization (listings and property data).

### Credentialing and professional-authority standards

- [**W3C Verifiable Credentials 2.0**](https://www.w3.org/TR/vc-data-model-2.0/) — cryptographic digital-credential data model (published as a W3C Standard on 2025-05-15).
- [**Open Badges 3.0**](https://www.imsglobal.org/spec/ob/v3p0) and [**CLR 2.0**](https://www.1edtech.org/clr/faq) from 1EdTech — academic and skill credentials built on VC 2.0; CLR 2.0 bundles credentials as transcripts.
- [**CAQH ProView**](https://www.caqh.org/solutions/provider-data/credentialing-suite) — US healthcare provider credentialing database (80% of US physicians, 900+ health plans, all 50 states).
- [**NCEES Records Program**](https://ncees.org/ncees-services/records-program/) — interstate portability of professional engineering credentials.
- [**ABA**](https://www.americanbar.org/groups/legal_education/accreditation/) — attorney admissions, JD accreditation, National Lawyer Regulatory Data Bank.
- [**NATE**](https://www.natex.org/) — HVAC technician certification.
- [**EPA Section 608**](https://www.epa.gov/section608) — federal refrigerant-handling certification.
- [**NICET**](https://www.nicet.org/) — engineering-technology certifications (electrical, fire alarm, water-based systems).
- [**NABCEP**](https://www.nabcep.org/) — solar / renewable-energy practitioner certification.
- [**ABET**](https://www.abet.org/) — engineering, computing, and technology program accreditation. Sister specialty accreditors: **LCME** (medicine), **AACSB** (business), **ABA** (law), **CCNE** (nursing).
- [**PESC**](https://www.pesc.org/) — Postsecondary Electronic Standards Council (legacy transcript formats).

### Accessibility and internationalization standards

- [**WCAG 2.2 (W3C)**](https://www.w3.org/TR/WCAG22/) — Web Content Accessibility Guidelines; AA is Sunfish's baseline target.
- [**WAI-ARIA 1.2 (W3C)**](https://www.w3.org/TR/wai-aria-1.2/) — Accessible Rich Internet Applications; the role and attribute vocabulary every Sunfish component documents.
- [**Section 508**](https://www.section508.gov/) — US federal accessibility requirements.
- [**EN 301 549**](https://www.etsi.org/deliver/etsi_en/301500_301599/301549/) — EU accessibility standard referencing WCAG.
- [**AODA**](https://www.ontario.ca/laws/statute/05a11) — Accessibility for Ontarians with Disabilities Act; Canadian regulatory reference.
- [**axe-core**](https://github.com/dequelabs/axe-core) — open-source accessibility testing engine used in Sunfish's component test harness.
- [**BCP-47 (IETF RFC 5646)**](https://www.rfc-editor.org/rfc/rfc5646) — locale and language-tag format used throughout Sunfish.
- [**Unicode CLDR**](https://cldr.unicode.org/) — Common Locale Data Repository; the canonical source for locale-specific data.
- [**ICU MessageFormat**](https://unicode-org.github.io/icu/userguide/format_parse/messages/) — the pluralization and formatting grammar Sunfish templates use for complex localized text.
- [**.NET Globalization**](https://learn.microsoft.com/en-us/dotnet/core/extensions/globalization) — the BCL APIs that carry locale and timezone through Sunfish's data layer.

### UI technical foundations

- [**Web Components (MDN)**](https://developer.mozilla.org/en-US/docs/Web/API/Web_components) — Custom Elements, Shadow DOM, HTML Templates, ES Modules; the browser-native primitives Sunfish components are built on.
- [**Declarative Shadow DOM** (web.dev)](https://web.dev/articles/declarative-shadow-dom) — HTML-authored shadow trees via `shadowrootmode`; enables SSR.
- [**Scoped Custom Element Registries** (WICG proposal)](https://wicg.github.io/webcomponents/proposals/Scoped-Custom-Element-Registries.html) — isolated element registries so multiple component libraries coexist without name collisions; default in Chromium browsers in 2026.
- [**Why You Should Use Web Components Now** (Proud Commerce)](https://www.proudcommerce.com/web-components/why-you-should-use-webcomponents-now) — framing for Web Components as a framework-agnostic, standards-based foundation.
- [**Claude Artifacts** (Anthropic)](https://www.anthropic.com/news/artifacts) and [**Claude Artifacts beginner's guide** (madewithclaude)](https://madewithclaude.com/guides/beginners-guide) — the AI-output pattern Sunfish treats as first-class platform input.

### Commercial UI component vendors (current and future compat surfaces)

- [**Progress Telerik**](https://www.telerik.com/) — .NET component suite covering Blazor, ASP.NET (Core and MVC), WPF, and WinForms; the first compat surface shipped (`compat-telerik`).
- [**Progress Kendo UI**](https://www.telerik.com/kendo-ui) — JavaScript component suite covering React, Angular, Vue, and jQuery; same parent company as Telerik but a distinct product line targeting a different stack; a planned second compat surface.
- [**Infragistics Ignite UI**](https://www.infragistics.com/products/ignite-ui) — 100+ high-performance UI components spanning Blazor, Angular, React, and Web Components.
- [**Syncfusion Essential Studio**](https://www.syncfusion.com/) — enterprise-ready component suites (charts, grids, schedulers, editors) across .NET, Blazor, and JavaScript frameworks.
- [**Oracle JET (JavaScript Extension Toolkit)**](https://www.oracle.com/webfolder/technetwork/jet/index.html) — standalone custom web components and grouped "JET Packs"; open-source-licensed by Oracle with commercial support.
- Additional vendors (**DevExpress**, **ComponentOne / GrapeCity**, and others) are candidates as the community or commercial services build out compat surfaces.
