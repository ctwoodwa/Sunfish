# Anti-Pattern Sweep — Batch 1

**Auditor:** Subagent (autonomous overnight run)
**Date:** 2026-04-28
**Framework:** Universal Planning Framework v1.2 — 21-AP scan only (Stages 0 and 5-CORE re-analysis skipped per procedure)
**ADRs in batch:** 0001, 0002, 0003

---

## ADR 0001 — Schema Registry Governance Model

**Decision:** Adopt a two-tier hybrid governance model: Tier 1 = repo-local schemas (no external gatekeeper); Tier 2 = cross-deployment `sunfish.io/schemas/*` namespace governed by a lightweight RFC process run out of the Sunfish OSS repo, with CNCF escalation deferred to v1+.

| AP # | Description | Hit | Severity | Recommendation |
|------|-------------|-----|----------|----------------|
| AP-1 | Unvalidated assumptions | partial | minor | annotate |
| AP-10 | First idea remaining unchallenged | partial | minor | annotate |
| AP-18 | Unverifiable gates | partial | minor | annotate |

**AP-1 detail:** The ADR assumes the lightweight RFC process ("issue → draft PR → two-week comment period → merge") will be sufficient to govern Tier 2 canonical schemas. This assumption is untested — no RFC template exists yet and the process is described as "informal and maintainer-trust-based." The ADR acknowledges this in Consequences but does not map it to a concrete validation step. Recommend annotating with: "Process adequacy validated when first Tier 2 RFC completes without a governance dispute."

**AP-10 detail:** "Why not W3C or a foundation?" is addressed, but an alternative of a simpler single-tier local-only model (with cross-deployment schema sharing simply documented as out-of-scope for v0) is not explicitly challenged. The two-tier design is arguably more complexity than v0 needs. This is a mild concern — the ADR's "v0 scope" section does acknowledge that no machinery ships with this ADR, so the overhead is low. Annotate: "Single-tier (local-only) alternative was considered and rejected because the v0 position statement is needed to guide schema authors before the machinery ships."

**AP-18 detail:** The revisit trigger "A second independent organization wants to publish Tier 2 schemas" is not verifiable from inside the project — there is no monitoring or tracking mechanism for external intent. Acceptable as-is for a governance ADR, but worth annotating that this trigger depends on inbound community signals.

**Overall grade: annotation-only**

---

## ADR 0002 — Kernel Module Format

**Decision:** NuGet is the default and primary module format; OCI artifacts are a v1+ add-on; Assembly + manifest hot-loading is explicitly out of scope for v0.

| AP # | Description | Hit | Severity | Recommendation |
|------|-------------|-----|----------|----------------|
| AP-1 | Unvalidated assumptions | partial | minor | annotate |
| AP-11 | Zombie project / no kill criteria | partial | minor | annotate |

**AP-1 detail:** The ADR states that when OCI support ships, "the same `.nupkg` artifact will be repackaged as an OCI layer; no new SDK or plugin API is anticipated." This is a forward assumption about a not-yet-designed system. The phrase "no new SDK or plugin API is anticipated" is confident without evidence — the actual OCI overlay design may require interface changes. Recommend annotating: "OCI-overlay assumption is best-effort; a separate ADR is required when OCI work begins and may supersede this claim."

**AP-11 detail:** The `IPluginManifest` gap is acknowledged as a "Phase 2 deliverable" but there is no kill criterion or timeout. If Phase 2 never ships (project pivots, de-prioritized), plugin discovery remains convention-based DI registration indefinitely with no explicit decision logged. The revisit trigger "IPluginManifest interface is designed in Phase 2" is helpful but passive. Recommend annotating: "If IPluginManifest is not designed by v1.0, a follow-up ADR should either close the gap or explicitly accept convention-based DI as permanent."

**Overall grade: annotation-only**

---

## ADR 0003 — Event-Bus Distribution Semantics

**Decision:** At-least-once is the normative `IEventBus` delivery contract; idempotency is a hard subscriber requirement; exactly-once via Kafka transactions is a pluggable v1+ backend option, not an API-level concern.

| AP # | Description | Hit | Severity | Recommendation |
|------|-------------|-----|----------|----------------|
| AP-1 | Unvalidated assumptions | partial | minor | annotate |
| AP-3 | Vague success criteria | partial | minor | annotate |

**AP-1 detail:** The ADR includes a hypothetical future registration snippet:
> `services.AddSunfishEventBus(bus => bus.UseMassTransit(mt => mt.UseKafka(...)))`
> "exact API TBD in that PR"

Presenting a concrete-looking API shape that is explicitly marked TBD risks downstream authors anchoring on it before it is validated. This is a documentation risk, not an architecture risk. Recommend annotating: "The Kafka registration snippet is illustrative only; the actual API is designed in the MassTransit backend PR and may differ materially."

**AP-3 detail:** The dedup TTL guidance ("24 h is a reasonable default") appears in the canonical idempotency code sample without a rationale or success criterion for choosing it. Different deployment scenarios (slow batch jobs, financial audit trails) may require TTLs orders of magnitude larger. There is no verifiable statement of when 24 h is sufficient. Recommend annotating: "The 24 h TTL is a starting-point heuristic. Application teams must validate their worst-case retry window against this value and document their chosen TTL in their own ADR or runbook."

**Overall grade: annotation-only**

---

## Batch Summary

| ADR | Title | Grade |
|-----|-------|-------|
| 0001 | Schema Registry Governance Model | annotation-only |
| 0002 | Kernel Module Format | annotation-only |
| 0003 | Event-Bus Distribution Semantics | annotation-only |

No ADRs in this batch require amendment. All three are well-scoped, acknowledge their own gaps via Consequences and Revisit Triggers, and avoid critical anti-patterns. The recurring theme across all three is **AP-1 (unvalidated forward assumptions)** about v1+ follow-on work — a natural artifact of v0 position statements, acceptable at this stage, but worth annotating so future readers understand the confidence level of each forward claim.
