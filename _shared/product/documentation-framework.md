# Documentation Framework

**Status:** Accepted
**Last reviewed:** 2026-04-19
**Governs:** The structure of `apps/docs/` and every authored Markdown page published to the Sunfish documentation site. Does not govern internal workflow artifacts under `icm/`, ADRs under `docs/adrs/`, or engineering standards under `_shared/`.
**Companion docs:** [component-principles.md](../design/component-principles.md), [tokens-guidelines.md](../design/tokens-guidelines.md), [architecture-principles.md](architecture-principles.md), [vision.md](vision.md).

Sunfish ships for three audiences at once — technical founders, small operators without a dev team, and AI-savvy stakeholders ([vision.md](vision.md)). A single prose style can't serve all three unless the docs are organized by *what the reader is trying to do* rather than by *what part of the product is being described*. This document adopts the Diátaxis framework as that organizing principle for `apps/docs/`.

## Adoption

Sunfish adopts **Diátaxis** — a documentation framework by Daniele Procida, published at <https://diataxis.fr/> — as the authoritative organizing model for the user-facing docs site. Diátaxis identifies four distinct modes of documentation corresponding to four distinct reader needs, and holds that conflating them is the root cause of most documentation failure.

Adoption is deliberate and not experimental. Diátaxis is used by the Django project (Procida's original context), NumPy, SciPy, Google developer documentation, Gatsby, Cloudflare, and Canonical (Ubuntu, MAAS, Juju) among others. In each case the migration produced measurable reductions in reader bounce, support-forum repetition, and content duplication. Sunfish commits to the same model for the same reasons.

Where this document uses "mode," "quadrant," and "type" interchangeably, they all refer to the four Diátaxis modes.

## The four modes

Each mode answers a different reader question and has a different shape.

| Mode | Reader question | Orientation | Characteristic shape |
|---|---|---|---|
| **Tutorial** | "I'm new — teach me." | Learning-oriented | A guided lesson. The author takes responsibility for the reader's success. Every step works. |
| **How-to guide** | "I have a specific problem — solve it." | Task-oriented | A recipe. Assumes the reader already knows the basics and wants the shortest path to a result. |
| **Reference** | "What exactly does X do?" | Information-oriented | A technical description. Dry, precise, complete. Structured so lookup is fast. |
| **Explanation** | "Why is it this way?" | Understanding-oriented | A discussion. Provides context, alternatives considered, trade-offs. The reader is learning concepts, not steps. |

The distinctions that matter most in practice:

- **Tutorial vs. how-to.** Both have steps. A tutorial's steps are a learning path — the reader is not expected to have a goal beyond "understand how Sunfish works." A how-to's steps serve a concrete goal the reader brought with them.
- **Reference vs. explanation.** Both are descriptive. Reference tells you *what* is there. Explanation tells you *why* it is there. Reference does not argue; explanation may.
- **Tutorial vs. explanation.** Both educate. A tutorial teaches by doing. An explanation teaches by describing.

Common pitfalls to avoid (these are the failures Diátaxis was built to prevent):

- A "tutorial" that is actually a feature tour — jumps around, demonstrates capability, doesn't land the reader on a working artifact.
- A how-to that teaches concepts it should be linking to — the reader came for the task, not the lesson.
- Reference pages that argue for or against a design — that argument belongs in explanation.
- Explanation pages that embed procedural steps — the step sequence belongs in a tutorial or how-to that the explanation links to.

## The four modes mapped to Sunfish surfaces

Concrete examples, drawn from existing plans under `docs/superpowers/plans/` and the `_shared/` / ADR corpus:

### Tutorials (learning-oriented)

- *Build your first Sunfish bundle from scratch* — clone the template, add one entity, wire one block, see it run.
- *Run Bridge locally with a small-landlord demo* — clone, `dotnet run` the AppHost, log in with mock Okta, walk through the seeded tenant.
- *Compose a kitchen-sink page with three components* — pick a button, a grid, a dialog; see them theme together via the active provider.
- *Federate two Sunfish nodes on one machine* — issue a capability, sync an entity, watch the audit trail.

### How-to guides (task-oriented)

- *Add a new extension field to an entity* — ADR 0005 four-layer model, end-to-end.
- *Swap `blocks-accounting` for QuickBooks Online* — replace the default block with an adapter.
- *Migrate a Razor component to a WC-first shell* — ADR 0017 Lit basis, concrete steps.
- *Publish a bundle to the registry* — manifest, version, signature.
- *Enable dark mode in a custom provider* — token override path per [tokens-guidelines.md](../design/tokens-guidelines.md).
- *Mock Okta during local development* — pointer into the `MockOktaService` accelerator wiring.

### Reference (information-oriented)

- API reference for every Foundation package (generated from XML doc comments via DocFX — already in `apps/docs/api/`).
- Component contract reference — the canonical behavior spec for each `SunfishX` component (already under `apps/docs/component-specs/`, 107 entries).
- ADR index with one-line summaries and links (derived from `docs/adrs/`).
- Bundle-manifest schema reference (ADR 0007).
- Token catalog — the canonical `--sf-*` list with semantic meanings.
- Federation wire-format reference — the transport and envelope contracts.

### Explanation (understanding-oriented)

- *Why Web Components?* — ADR 0017's rationale in prose form.
- *How federation works* — the trust/transport/audit substrate from [vision.md §2](../product/vision.md).
- *The ODF + UPF + ICM stack explained* — how the three orchestration layers relate.
- *The four-layer type-customization model* — ADR 0005 as discussion, not procedure.
- *Local-first as a default, not a feature* — the seven Kleppmann ideals applied to Sunfish (ADR 0012).
- *Why adapter parity is mandatory* — ADR 0014 reframed for the casual reader.

## Where each type lives in the repo

`apps/docs/` is reorganized to mirror the four modes at the top level. Proposed layout:

```
apps/docs/
  docfx.json
  toc.yml
  index.md                 landing page — routes readers into the four modes
  tutorials/               learning-oriented, ordered, self-contained lessons
    first-bundle/
    bridge-local/
    federated-pair/
    kitchen-sink/
  how-to/                  task-oriented recipes, grouped by subject
    entities/
    blocks/
    bundles/
    theming/
    federation/
    bridge/
  reference/               information-oriented, generated + authored
    api/                   ← DocFX-generated, existing api/ moves here
    components/            ← existing component-specs/ moves here
    adrs/                  ← auto-generated index + links to docs/adrs/
    manifests/             bundle-manifest schema + examples
    tokens/                token catalog
    federation/            wire formats
  explanation/             understanding-oriented long-form
    architecture/
    federation/
    local-first/
    customization/
    governance/
  _contentTemplates/       shared fragments (unchanged)
  images/                  (unchanged)
```

Existing top-level folders under `articles/` (`getting-started`, `theming`, `accessibility`, `common-features`, `globalization`, `security`, `testing`, `troubleshooting`) are decomposed into the four modes during migration; most of their contents are either tutorials (`getting-started/*`), how-tos (`theming/*`, `globalization/*`, `security/*`), or explanation (`accessibility/*` principles). See Migration below.

## Authoring guidelines per mode

### Tutorials

- **Length.** 15–40 minutes of reader time. If longer, split into chapters.
- **Voice.** First-person plural or second-person. "We'll add a field." "You should see a green check."
- **Assumed knowledge.** As little as the tutorial's subject permits. The first Sunfish tutorial assumes .NET SDK and a shell, nothing else.
- **Success criterion.** Every reader who follows every step produces the same working artifact.
- **Links to other modes.** Freely link *out* to reference for details the reader may want later, but never require the reader to leave the tutorial to complete it.

### How-to guides

- **Length.** 5–15 minutes. Short and specific.
- **Voice.** Imperative, direct. "Create a file…", "Run…", "Verify the output matches…"
- **Assumed knowledge.** The reader has done a relevant tutorial or is already a practitioner. Name the assumed prerequisite in the opening paragraph.
- **Success criterion.** The reader's stated goal is achieved.
- **Links.** Link to reference for exact signatures; link to explanation for the *why* a curious reader may ask after the task is done.

### Reference

- **Length.** As long as completeness demands. Structured for scannability (tables, headings, short paragraphs).
- **Voice.** Third-person, descriptive. No opinions, no motivation, no narrative.
- **Assumed knowledge.** The reader knows what they're looking up.
- **Success criterion.** The reader finds the exact fact they needed and leaves.
- **Links.** Link to explanation for rationale; link to how-to for common usages. Do not embed extended examples — a code sample of 3–10 lines is fine; a walkthrough belongs in a tutorial or how-to.

### Explanation

- **Length.** 1,000–3,000 words typical. Longer if the subject warrants.
- **Voice.** Discursive, thoughtful. The author may say "we chose X because" and may name alternatives considered.
- **Assumed knowledge.** Varies; name it up front.
- **Success criterion.** The reader leaves understanding *why*, not just *what* or *how*.
- **Links.** Link to reference for exact facts; link to tutorials for readers who now want to try the thing.

## Migration from current docs

| Current location | Target mode | Notes |
|---|---|---|
| `apps/docs/articles/getting-started/*` | Tutorials | Already learning-oriented; rename and relocate. |
| `apps/docs/articles/theming/*` | How-to + Explanation | Split: "switch to dark mode" is how-to, "how provider theming works" is explanation. |
| `apps/docs/articles/accessibility/*` | Explanation + Reference | Principles to explanation; per-component a11y shapes to reference. |
| `apps/docs/articles/common-features/*` | How-to | Most entries are tasks. |
| `apps/docs/articles/globalization/*` | How-to | Locale/translation recipes. |
| `apps/docs/articles/security/*` | Mostly explanation, some how-to | Authn/authz models → explanation; "configure Okta" → how-to. |
| `apps/docs/articles/testing/*` | How-to | Test recipes. |
| `apps/docs/articles/troubleshooting/*` | How-to | "When you see X, do Y." |
| `apps/docs/component-specs/*` | Reference | Relocate under `reference/components/`. |
| `apps/docs/api/*` (generated) | Reference | Relocate under `reference/api/`. |
| `docs/adrs/*` | Source for explanation + reference index | ADRs remain at source; `reference/adrs/` hosts an index; select ADRs are re-narrated as explanation pages. |
| `docs/specifications/*` | Source for explanation + reference | Long-form specs stay at source; digest pages under `reference/` and `explanation/` link back. |
| `docs/superpowers/plans/*` | Not migrated | Internal planning artifacts; not user-facing. |
| `_shared/**/*.md` | Not migrated | Internal engineering/design standards; not user-facing. |
| `icm/**/*.md` | Not migrated | Workflow orchestration; not user-facing. |

Migration is phased, not atomic. The existing `articles/` tree keeps working (redirects via DocFX) until each section has landed in its new home.

## Cross-linking policy

Modes reference each other freely but never blur their boundaries. The rules:

1. **A tutorial may link to any mode but does not require the reader to leave.** A tutorial is self-sufficient; outbound links are optional enrichment.
2. **A how-to guide links primarily to reference** for exact signatures and to tutorials for foundational concepts the reader may be missing. A how-to avoids linking into explanation mid-task — the reader wants to finish first.
3. **Reference links out for context, not back-fills.** An API page may link to the explanation of the concept it implements and to one or two canonical how-tos; it does not link to tutorials (tutorials are for first encounter, reference is not).
4. **Explanation links freely into reference and tutorials** to ground abstract claims in concrete artifacts. It rarely links into other explanation pages except to acknowledge related discussion.
5. **Every page declares its mode in frontmatter** (`mode: tutorial | how-to | reference | explanation`). A reviewer who sees mode-mixing (e.g., a reference page that argues) treats it as a defect.
6. **One fact, one canonical home.** If the same fact appears in two modes, the reference version is canonical; others link to it.

## Relationship to ADRs, `_shared/` standards, and the ICM workflow

Sunfish keeps three classes of documentation, and Diátaxis governs only one of them.

| Corpus | Audience | Governed by Diátaxis? |
|---|---|---|
| `apps/docs/` | External users — founders, operators, AI-savvy stakeholders, evaluators, contributors reading to learn | **Yes.** This document is the authority. |
| `docs/adrs/`, `docs/specifications/`, `_shared/engineering/`, `_shared/design/`, `_shared/product/` | Contributors and maintainers — people writing Sunfish, not using it | **No.** These are internal-facing design records. They feed *into* user-facing docs but keep their own conventions (ADR template, YAML-header standards docs, specification format). |
| `icm/**`, `docs/superpowers/plans/` | Workflow orchestration | **No.** These are process artifacts, not documentation. |

ADRs, specifications, and `_shared/` standards **are inputs to** the user-facing docs. A new ADR typically yields:

- A reference entry in `reference/adrs/`.
- An explanation page when the ADR has meaningful rationale (most do).
- Optionally a how-to when the ADR changes a task the reader performs.

Those derivatives are written in Diátaxis shape; the source ADR keeps its own shape. Do not try to rewrite ADRs into Diátaxis form — they serve a different purpose (point-in-time architectural decisions with full context, including rejected alternatives).

Similarly, `_shared/engineering/coding-standards.md` is not a Sunfish docs-site page; it is an internal document read by contributors. If a convention in it is relevant to external users, capture it in `reference/` (as a fact) or `explanation/` (as a reason) — do not copy the file.

## Cross-references

- [vision.md](vision.md) — three-audience commitment that motivates the framework.
- [architecture-principles.md](architecture-principles.md) — framework-agnostic claim that explanation pages reify.
- [component-principles.md](../design/component-principles.md) — headless contracts; component reference pages describe these.
- [tokens-guidelines.md](../design/tokens-guidelines.md) — token catalog backs the `reference/tokens/` entries.
- `apps/docs/README.md` — DocFX build and deploy workflow.
- `docs/adrs/README.md` — ADR index (source for `reference/adrs/`).
- `docs/superpowers/plans/2026-04-17-sunfish-phase8-docs.md` — the docs-phase plan that this framework guides.
- Diátaxis — <https://diataxis.fr/> — Daniele Procida, the canonical framework reference.
- Django documentation structure — <https://docs.djangoproject.com/> — reference adopter.
- NumPy documentation structure — <https://numpy.org/doc/> — reference adopter.
