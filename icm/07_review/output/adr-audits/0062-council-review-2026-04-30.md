# ADR 0062 — Mission Space Negotiation Protocol (runtime layer) — Council Review

**Date:** 2026-04-30
**Reviewer:** research session (XO; adversarial council, UPF Stage 1.5)
**Subject:** ADR 0062 v. 2026-04-30 (Proposed; auto-merge intentionally DISABLED per cohort discipline)
**ADR under review:** [`docs/adrs/0062-mission-space-negotiation-protocol.md`](../../../docs/adrs/0062-mission-space-negotiation-protocol.md) on branch `docs/adr-0062-mission-space-negotiation-protocol`; PR **#406**.
**Companion intake:** [`icm/00_intake/output/2026-04-30_mission-space-negotiation-protocol-intake.md`](../../00_intake/output/2026-04-30_mission-space-negotiation-protocol-intake.md)
**Driver discovery:** [`icm/01_discovery/output/2026-04-30_mission-space-matrix.md`](../../01_discovery/output/2026-04-30_mission-space-matrix.md) §5.6 + §6.2 + §7.2 — third item in the W#33 follow-on authoring queue
**Companion artifacts read:** ADR 0062 (591 lines, single document); ADR 0028 post-A8 surface (`origin/main:docs/adrs/0028-crdt-engine-selection.md` — A5/A8 `FormFactorProfile`; A6/A7 `VersionVector` post-A7.3 + post-A7.6 + post-A7.8 shape); ADR 0036 (`SyncState Multimodal Encoding Contract`; not a state machine — encoding contract); ADR 0041 (`Dual-Namespace Components by Design (Rich vs. MVP)`); ADR 0009 (`Foundation.FeatureManagement` — Edition / EditionResolver / FeatureSpec; NO "trust tier" or "commercial tier" naming); ADR 0049 (audit substrate); ADR 0061 (three-tier peer transport); paper §13.2 (AP/CP visibility tables); `packages/foundation/Capabilities/ICapabilityGraph.cs`; `packages/foundation-localfirst/`; `packages/kernel-audit/AuditEventType.cs` (full constant set); `packages/ui-adapters-blazor/Components/LocalFirst/SyncState.cs`; `packages/ui-core/src/components/syncstate/sunfish-syncstate-indicator.ts`. Prior cohort councils: A6 (PR #396); A5 (PR #403); 0061 (PR #341).

**Council perspectives applied:** (1) Distributed-systems / runtime-protocol reviewer; (2) Industry-prior-art reviewer; (3) Cited-symbol / cohort-discipline reviewer (3-direction spot-check per the A7 lesson — positive-existence, negative-existence, structural-citation correctness); (4) UX / user-communication reviewer.

---

## 1. Verdict

**Accept-with-amendments — grade B (low-B).** ADR 0062 is doing a **necessary** thing: paper §13.2 + ADR 0036 + ADR 0041 are downstream consumers of *something* the paper has not specified, and the W#33 §6.2 framing names this protocol as the highest-priority follow-on. The Stage-0 sparring is honest (Options A / B / C / D triangulated; A's pure-coordinator is rejected for a hybrid Option-D specialization that matches realistic deployments), the DirectX-Feature-Level prior-art selection is the right anchor (enumerated tiers, runtime-queryable, OS-guarantees-behavior, developer-owns-graceful-degradation), and the protocol surface (`IMissionEnvelopeProvider` + `ICapabilityGate<TCapability>` + 5-value `DegradationKind` taxonomy + 4-value `EnvelopeChangeSeverity`) is internally coherent.

**However** — six substrate-vocabulary, structural-citation, and prior-art-rigor problems prevent this ADR from clearing A on first review:

- **F1 (Critical, structural-citation, A7-third-direction class) — ADR 0041 is `Dual-Namespace Components by Design (Rich vs. MVP)`, NOT a "rich-vs-MVP UI degradation primitive."** ADR 0041 specifies that `SunfishGantt` / `SunfishScheduler` / `SunfishSpreadsheet` / `SunfishPdfViewer` each ship under TWO namespaces (`DataDisplay/Gantt/SunfishGantt.razor` rich variant for kitchen-sink demos; `Scheduling/SunfishGantt.razor` MVP variant for the canonical leaf catalog) — this is a *component-pair coexistence policy*, not a graceful-degradation primitive. ADR 0062's Context paragraph + Decision-driver bullet + Phase 4 migration step ("migrate ADR 0041 rich-vs-MVP surface to consume `ICapabilityGate<TCapability>` verdicts") all cite ADR 0041 as if it specified a runtime degradation taxonomy. It doesn't. This is the same A7-third-direction failure shape that fired on ADR 0028-A6.2 rule 3 (`required: true` cited on `ModuleManifest` when it lives on `ProviderRequirement`) and ADR 0028-A5.7 (per-tenant key surface cited on `IFieldDecryptor` when it lives on `ITenantKeyProvider`).

- **F2 (Critical, AP-19 discovery-amnesia / substrate-vocabulary collision) — `Sunfish.Foundation.MissionSpace.ICapability` (marker interface) + `ICapabilityGate<TCapability>` collide with existing `Sunfish.Foundation.Capabilities.ICapabilityGraph`** — both `Sunfish.Foundation.*` namespaces, both using "Capability" as the load-bearing noun, but with **completely different semantics**. `Foundation.Capabilities` is the *authorization* substrate (signed-op log; `ICapabilityGraph.QueryAsync(subject, resource, action, asOf)` answers "may this principal perform this action on this resource?"). `Foundation.MissionSpace.ICapability` (proposed) is the *runtime feature-availability* surface ("can this device run this feature right now?"). Two senses of "capability" sharing the foundation namespace will cause Stage 06 implementer confusion at the imports level (`using Sunfish.Foundation.Capabilities;` vs `using Sunfish.Foundation.MissionSpace;` — but both export `ICapability*`). Same shape as the ADR 0061 council's `NodeId` vs `PeerId` finding.

- **F3 (Major, AP-21 cited-symbol drift / substrate-vocabulary) — `Instant CapturedAt` uses NodaTime's `Instant`, but the substrate uses `DateTimeOffset` everywhere.** `MissionEnvelope.CapturedAt`, `IBespokeSignal.CapturedAt`, and `CapabilityForceEnableRequest.ExpiresAt` all cite `Instant` (NodaTime). Verified: `packages/kernel-audit/AuditRecord.cs` declares `DateTimeOffset OccurredAt`; `Sunfish.Foundation.Crypto.IOperationSigner.SignAsync<T>(T payload, DateTimeOffset issuedAt, ...)` uses `DateTimeOffset`; `Sunfish.Foundation.Recovery.IRecoveryClock.UtcNow()` returns `DateTimeOffset`. **No** package under `packages/` uses `NodaTime.Instant`. The `feedback_decision_discipline` industry-defaults memory names NodaTime as a default — but the substrate adopted `DateTimeOffset`. Adopting `Instant` here forks the convention.

- **F4 (Major, AP-1 unvalidated assumption) — coordinator-as-chokepoint is named in Option-A "Con" but mitigation handwaves through "change events are async; gates don't wait synchronously."** What happens when (a) `IMissionEnvelopeProvider.GetCurrentAsync` is called concurrently by N gates while a probe is in-flight (does the second caller wait, get stale, or kick off its own probe?); (b) `ProbeAsync` blocks on the High-cost `CommercialTier` probe (Bridge HTTP round-trip) and a thread-pool-starvation scenario fires; (c) the coordinator's process is held up in a finalizer or GC pause and `Subscribe`'d observers' events queue unboundedly. The protocol pseudocode says "stale-while-revalidate for High-cost dimensions" but never specifies the **single-flight semantics** (does N concurrent `GetCurrentAsync` produce N probes, 1 probe + N awaiters, or 1 probe + (N-1) immediate-stale-returns?). Same failure-mode class as the ADR 0061 `ITransportSelector` partial-failure gap.

- **F5 (Major, AP-21 prior-art-citation) — DirectX Feature Levels analogy is load-bearing but partially miscited; SDP RFC 5939 dismissed without analysis.** Three problems:
   - DirectX Feature Levels (FL_9_1, FL_10_0, FL_11_0, FL_12_0) are **single-axis enumerated tiers** — one identifier per device. ADR 0062's 10 Mission Space dimensions are **10 orthogonal axes**, NOT a single tier. The analogy holds for "enumerated, not arithmetic" but breaks for "discrete tiers" and "OS-guarantees-the-tier behaviorally" (the OS does NOT promise that all 10 dimensions on a device are jointly stable — `Network` flips constantly; `CommercialTier` flips on subscription events; `User` flips on sign-in/sign-out).
   - Vulkan's `VkPhysicalDeviceFeatures` is the closer analog (~50 boolean flags) — but isn't cited.
   - SDP RFC 5939 ("Negotiation of Generic Image Attributes in the Session Description Protocol") is named in §"Considered options" intro but rejected without analysis. RFC 5939 has a publish/subscribe + offer/answer semantic that *would* engage with Option C's bus-vs-state-record gap; the dismissal is hand-waved.

- **F6 (Major, AP-1 / AP-21) — `1-hour cache TTL for High-cost dimensions like CommercialTier` is operationally wrong for Bridge subscription billing-cycle UX.** `CommercialTier` is High-cost (Bridge HTTP round-trip per ADR 0009/0031 — though see F8 — with 1-hour cache). **Operational reality:** when a user upgrades their Bridge subscription (e.g., from `anchor-self-host` to `bridge-anchor`), they expect the upgrade to be reflected within **seconds**, not up to an hour. The 1-hour cache means an upgraded user would continue to see "Upgrade to Bridge tier" upsells (`DegradationKind.DisableWithUpsell`) for up to an hour AFTER paying. This is the exact "user has paid; UI still shows 'pay to upgrade'" failure mode the protocol's own §"User-communication policy" should prohibit. Stale-while-revalidate is named in §"Cache vs live-probe" but doesn't define what the user sees during the stale window — and the §"Re-evaluation triggers" canonical list does not mention "subscription upgrade event from Bridge" as a forced re-evaluation trigger (it mentions "Subscription start/end events from Bridge per ADR 0031" in the dimension table but ADR 0031 is **not** a subscription-event-emitter — see F8).

Beyond those six, six more substantive findings warrant amendments — none are blockers, but each addresses a real gap or a hardening opportunity.

**Cohort batting-average update.** With ADR 0062, the substrate-amendment cohort is now **13-of-13 needing post-acceptance amendments after council review** (0046-A2, 0051, 0052, 0053, 0054, 0058, 0059, 0061, 0046-A4, 0028-A6 → A7, 0028-A5 → A8, 0048-A1, 0028-A1, plus 0062). Pre-merge council on substrate ADRs is now **structurally canonical**, not a guideline.

Structural-citation-failure rate (the A7-third-direction class) tracking: this council finds **2 structural-citation failures** (F1 ADR 0041 mis-citation; F8 ADR 0031 subscription-event-emitter mis-citation — flagged below). Adding to the running count: **6-of-13 XO-authored substrate amendments now have at least one structural-citation failure caught by pre-merge council**. The pattern remains: grep alone is insufficient; reading the cited ADR's actual surface beats keyword-match for structural claims.

Council false-claim rate this review: **0 false-existence + 0 false-non-existence + 0 false-structural** as written below. Spot-checked all three directions before finalization.

---

## 2. Findings

### F1 (Critical, structural-citation; A7-third-direction class) — ADR 0041 is NOT a "rich-vs-MVP UI degradation primitive"

**Where:** ADR 0062 §"Context" — *"ADR 0041 specifies the rich-vs-MVP UI degradation primitive."* Repeated in §"Decision drivers" — *"ADR 0041 rich-vs-MVP surface — all consumed; none replaced."* And in §"Compatibility plan / Migration order / Phase 4" — *"migrate ADR 0041 rich-vs-MVP surface to consume `ICapabilityGate<TCapability>` verdicts."*

**Reality (verified `git show origin/main:docs/adrs/0041-dual-namespace-components-rich-vs-mvp.md`):** ADR 0041's title is "Dual-Namespace Components by Design (Rich vs. MVP)" and its Decision is:

> Four component pairs exist today in `packages/ui-adapters-blazor/Components/`: `SunfishGantt` / `SunfishScheduler` / `SunfishSpreadsheet` / `SunfishPdfViewer`. Each pair shares the type name `Sunfish*` but lives in two distinct namespaces. [...] The pattern was introduced when the rich variants were authored to satisfy kitchen-sink's Telerik-verbose demo standard while the MVP variants remained as the canonical small-surface contract under the framework-agnostic taxonomy. Both are intentional; both serve different roles.

ADR 0041 is a **component-pair-coexistence policy** that prevents future dedup passes from destroying one half of each pair. It pairs with [ADR 0022](../../docs/adrs/0022-example-catalog-and-docs-taxonomy.md) (catalog tier system). It does NOT define a runtime degradation primitive, a capability-evaluation interface, or any UX state taxonomy.

**Impact:** A Stage 02 / Stage 06 reader following ADR 0062's references would `grep -nE "degradation|primitive|MVP" docs/adrs/0041-*.md` and find only the *catalog framing* meaning of "rich-vs-MVP" — not the runtime-degradation meaning ADR 0062 imputes. Phase 4's migration step ("migrate ADR 0041 rich-vs-MVP surface to consume `ICapabilityGate<TCapability>`") is undefined work — the rich-vs-MVP surface is a pair of `.razor` files per component, not a runtime evaluation surface that consumes verdicts.

**Two equally-good fixes:**

- **(a)** Drop the ADR 0041 citation entirely. ADR 0062's actual antecedent for "graceful degradation" is the paper's §13.2 visibility-treatments table (already cited) + ADR 0036's `SyncState` channel agreement (color/icon/label/ARIA — also a visibility primitive, not a degradation one). The runtime-graceful-degradation primitive is what ADR 0062 ITSELF introduces; it has no in-repo predecessor. Phase 4 of the migration becomes unneeded; Phase 5+ "per-feature opt-in migrations" already covers it.
- **(b)** Cite the canonical primitive that DOES exist for MVP-vs-rich runtime degradation: ADR 0022 example-catalog Tier 3 (which defines the rich-vs-MVP catalog tier framing). But ADR 0022 is a **docs taxonomy** ADR, not a runtime ADR — same shape error, different ADR. There's no in-repo runtime degradation primitive to cite.

**Recommended:** (a). Remove the ADR 0041 citation; restate the actual antecedents as paper §13.2 + ADR 0036.

**Severity:** Critical. Structural-citation. Same A7-third-direction shape as the ADR 0028-A6 council found on `required: true / ModuleManifest`.

### F2 (Critical, AP-19 discovery-amnesia / substrate-vocabulary collision) — `Foundation.MissionSpace.ICapability` collides with existing `Foundation.Capabilities.ICapabilityGraph`

**Where:** ADR 0062 §"Decision / Per-feature gate contract":

```csharp
public interface ICapabilityGate<TCapability> where TCapability : ICapability
```

and §"Per-feature force-enable surface":

```csharp
ValueTask ForceEnableAsync<TCapability>(...) where TCapability : ICapability;
```

**Reality (verified via `git grep -nE "interface ICapability|ICapabilityGraph" origin/main -- packages/`):**

- `packages/foundation/Capabilities/ICapabilityGraph.cs:15` — `public interface ICapabilityGraph` — the *authorization* substrate. `QueryAsync(PrincipalId subject, Resource resource, CapabilityAction action, DateTimeOffset asOf, CancellationToken ct)` returns "may this principal perform this action on this resource at this time?"
- `packages/blocks-public-listings/Capabilities/ICapabilityPromoter.cs:17` — `public interface ICapabilityPromoter` — promotes capabilities.
- `packages/federation-capability-sync/ICapabilityOpStore.cs:11` + `ICapabilitySyncer.cs:10` — federation surfaces for the capability log.

ADR 0062 introduces `Sunfish.Foundation.MissionSpace.ICapability` as a marker interface whose semantic is "this is a feature whose runtime availability the negotiation protocol gates," NOT "this is an authorization right granted to a principal."

**Impact:** Two senses of "capability" both rooted in `Sunfish.Foundation.*` is a substrate-vocabulary fork. Stage 02 implementer reading `ICapabilityGate<TCapability> where TCapability : ICapability` will need to disambiguate: which `ICapability`? The authorization one (whose query-shape is principal/resource/action) or the new MissionSpace one (whose query-shape is "given the current Mission Envelope, are you available")? `using Sunfish.Foundation.Capabilities; using Sunfish.Foundation.MissionSpace;` in the same file would surface a name collision the C# compiler resolves by namespace, but the *cognitive* collision is harder.

**Three options:**

- **(a)** Rename `ICapability` → `IFeatureCapability` (or `IRuntimeCapability`); rename `ICapabilityGate<T>` → `IFeatureCapabilityGate<T>`. Costs: 4 type renames; the `TCapability` generic-parameter name stays. Eliminates the cognitive collision.
- **(b)** Rename the package's noun from "Capability" to "Feature." `IFeatureGate<TFeature> where TFeature : IFeature`. The "Mission Space" framing is preserved at the package + envelope level (`MissionEnvelope` stays) but the per-feature evaluation surface uses "feature" — matching ADR 0009's `FeatureKey`/`FeatureSpec`/`IFeatureEvaluator` vocabulary.
- **(c)** Move the namespace to `Sunfish.Foundation.MissionSpace.Gates.*` (sub-namespace) and explicitly disambiguate at the package boundary. Doesn't fix the cognitive issue but reduces import-collision surface.

**Recommended:** (b). The "Feature" framing matches ADR 0009 (the *one* in-repo precedent for runtime-feature-availability) and avoids the term overload entirely. ADR 0009 is currently NOT cited by 0062 — but it's the closer prior-art and uses the closer vocabulary; folding 0062's evaluation surface into the same noun continues an established pattern.

**Severity:** Critical. AP-19 discovery-amnesia. Same shape as ADR 0061's `NodeId` vs `PeerId` (which the council also rated Critical).

### F3 (Major, AP-21 cited-symbol drift / substrate-vocabulary) — `Instant` is NodaTime; substrate uses `DateTimeOffset`

**Where:** ADR 0062 contract listings cite `Instant CapturedAt` in `MissionEnvelope`, `Instant CapturedAt` in `IBespokeSignal`, `Instant? ExpiresAt` in `CapabilityForceEnableRequest`.

**Reality (verified `git grep`):**

- No `using NodaTime` or `NodaTime.Instant` references exist in `packages/`.
- `packages/kernel-audit/AuditRecord.cs:53` — `DateTimeOffset OccurredAt`.
- `packages/foundation/Crypto/IOperationSigner.cs` — `SignAsync<T>(T payload, DateTimeOffset issuedAt, Guid nonce, CancellationToken ct = default)`.
- `packages/foundation-recovery/IRecoveryClock.cs` — `DateTimeOffset UtcNow()` (per ADR 0046 council F2).
- `_shared` industry-defaults table mentions NodaTime as a *default for new packages*, but the substrate has not adopted it.

**Impact:** Stage 02 / Stage 06 implementer copy-pasting from ADR 0062 will:
- (a) Introduce `NodaTime` as a `Sunfish.Foundation.MissionSpace.csproj` dependency, forcing audit-payload conversions at the seam between `Foundation.MissionSpace` (Instant) and `Kernel.Audit` (DateTimeOffset); OR
- (b) Silently drop the NodaTime reference and use `DateTimeOffset` matching the substrate, drifting from the ADR's signed-off type names.

Both are bad. The first introduces a substrate fork; the second invalidates the ADR as Cold-Start-Test-passing documentation.

**Two equally-good fixes:**

- **(a)** Replace every `Instant` with `DateTimeOffset` in the contract listings + JSON encoded form (`"capturedAt": "2026-04-30T22:00:00Z"` already encodes as ISO-8601 string, which is the canonical `DateTimeOffset` round-trip). Mechanical.
- **(b)** Adopt NodaTime across the substrate as a follow-up — but that's a separate ADR with substantial blast radius (kernel-audit + foundation-crypto + foundation-recovery all need refactor); out of scope for ADR 0062.

**Recommended:** (a). Match the existing substrate convention.

**Severity:** Major. Pure cited-symbol drift; same class as the ADR 0046-A4 council's `IRecoveryClock.UtcNow` finding.

### F4 (Major, AP-1 unvalidated assumption) — coordinator concurrency semantics undefined

**Where:** ADR 0062 §"Decision / Initial contract surface" + §"Cache vs live-probe":

> `IMissionEnvelopeProvider.GetCurrentAsync` returns the current Mission Envelope (cached if available; probes if not).
> [...] The coordinator returns the cached envelope IMMEDIATELY on `GetCurrentAsync` if any dimension is stale, and asynchronously kicks off a re-probe; when the re-probe completes, an `EnvelopeChange` event fires.

**Three operational pathologies the protocol does not address:**

1. **Single-flight under N concurrent callers.** Gates A, B, C, D, E all call `GetCurrentAsync` simultaneously; the cache for `CommercialTier` (High-cost) is stale. Does the coordinator (a) launch 5 parallel probes, (b) launch 1 probe + 4 awaiters that share the result, or (c) return stale-immediately to all 5 + launch 1 background probe? Each has different semantics. The §"Trust impact / Security & privacy" §"Probe-cost classes prevent denial-of-service" bullet says "a misbehaving feature requesting `ProbeAsync` repeatedly is rate-limited at the coordinator (1-per-second cap on force-fresh probes per process)" — but `GetCurrentAsync` is not `ProbeAsync`. The cap doesn't apply to the cache-stale path.

2. **Probe-blocking under thread-pool starvation.** `IDimensionProbe<CommercialCapabilities>.ProbeAsync` makes an HTTP call to Bridge. The HTTP stack uses thread-pool threads. Under a thread-pool-starvation scenario (e.g., a thundering herd of synchronous-over-async waits in unrelated code), the probe hangs. What does `GetCurrentAsync` do — wait for the hung probe, return stale, or short-circuit with a probe-failure sentinel? Spec is silent.

3. **Observer event-queue under burst.** `IMissionEnvelopeProvider.Subscribe(IMissionEnvelopeObserver)` returns `IDisposable`. If 100 dimension changes fire in a 1-second window (e.g., a hot-plug storm of 50 sensor connect/disconnect cycles + a network flap + a sign-in/sign-out cycle), does the coordinator (a) await each observer's `OnEnvelopeChangedAsync` sequentially (back-pressure to the storm), (b) fire-and-forget concurrently (observer ordering non-deterministic), or (c) coalesce same-second events into a single envelope-change? No spec.

**Impact:** The same `ITransportSelector` partial-failure-semantics gap that the ADR 0061 council flagged at Critical-Major level. Stage 06 implementer will guess; production behavior under load will be implementation-dependent and surprise the operator.

**Recommended:** Add §"Coordinator concurrency semantics" specifying: (i) single-flight on `GetCurrentAsync` cache-miss for the same dimension (1 probe + N awaiters share result); (ii) per-probe wall-clock timeout per cost class (Low: 1s; Medium: 2s; High: 5s; Live: N/A) with timeout-falling-back to last-known-cached value + emitting `CapabilityProbeFailure`; (iii) observer-fanout policy — concurrent fire-and-forget with bounded queue depth per observer (default 100); back-pressure-style coalescing of same-dimension changes within a 100ms window.

**Severity:** Major.

### F5 (Major, AP-21 prior-art-citation-rigor) — DirectX Feature Levels analogy is partially miscited; SDP RFC 5939 dismissed without analysis

**Where:** ADR 0062 §"Decision drivers" — *"Industry prior art has a clear winner: DirectX Feature Levels. [...] DirectX Feature Levels (FL_9_1 / FL_10_0 / FL_11_0 / FL_12_0) are the closest engineering analog: discrete tiers; runtime-queryable; degrade gracefully; OS surfaces them through a uniform API that game engines consume directly."*

**Three rigor problems:**

1. **DirectX Feature Levels are a single-axis enumeration, NOT 10 orthogonal axes.** A device reports ONE Feature Level (e.g., `D3D_FEATURE_LEVEL_11_0`); the OS does NOT expose 10 separate axes. ADR 0062's `MissionEnvelope` has 10 dimensions that flip independently (`Network` flips constantly; `CommercialTier` on subscription events; `User` on sign-in; `SyncState` continuously). The "OS guarantees the tier behaviorally" property of FL_x does NOT carry over — the Mission Space coordinator cannot guarantee that a reported `MissionEnvelope` is jointly stable for any meaningful duration. The right way to cite the analogy: "DirectX FLs inspire the *enumerated-not-arithmetic* property + the *runtime-queryable* property + the *developer-owns-graceful-degradation* property — but NOT the *OS-guarantees-behavior* or *single-axis-tier* properties, which Mission Space explicitly rejects in favor of orthogonal-multi-dimensional semantics."

2. **Vulkan's `VkPhysicalDeviceFeatures` is the closer multi-axis analog.** ~50 boolean flags reported per physical device, each independently queryable, gates `vkCmd*` calls. Same shape as Mission Space's 10 dimensions. ADR 0062 names "OpenGL/Vulkan extension queries" once in the prior-art list but does not engage with `VkPhysicalDeviceFeatures` specifically. The Vulkan analog would also surface a sub-question ADR 0062 doesn't address: should some Mission Space dimensions split into sub-flags? `Hardware` is one record with `cpuArch + memoryBytes + diskBytes + displayClass + sensorSurface` — VulkanDeviceFeatures-style would split each into its own boolean flag. Granularity choice unaddressed.

3. **SDP RFC 5939 dismissed without analysis.** §"Considered options" intro names "SIP/SDP per RFC 5939" as one of "the canonical capability-negotiation prior art" but doesn't engage with it. RFC 5939 (and its predecessor RFC 4317 / 3264 offer/answer) has a publish/subscribe + offer/answer semantic that is ACTUALLY closer to Option C's "push-based capability bus" — the bus + state-record gap that Option C's rejection rests on is exactly what RFC 5939 solves (offer = state-record snapshot; answer = subscribe-decision). Dismissing RFC 5939 without analyzing its offer/answer pattern leaves the Option C rejection thin.

**Impact:** Low operational impact (the chosen Option-A-with-D-specialization is still the right architecture); but the prior-art rationale is shallow, and Stage 02 reviewers asking "why this shape and not Vulkan-style flags or SDP offer/answer?" will find the answer hand-waved. Recommend: tighten §"Decision drivers" to (a) name the enumerated-not-arithmetic + runtime-queryable + developer-owns-degradation properties as the inheritances FROM DirectX, explicitly NOT inheriting OS-guarantees-behavior or single-axis; (b) add `VkPhysicalDeviceFeatures` as the multi-axis analog and acknowledge granularity choice; (c) one paragraph engaging with RFC 5939's offer/answer pattern + naming why Mission Space's coordinator-owns-state model is preferred (e.g., diagnostics + telemetry-shape + operator-debugging all benefit from a state record).

**Severity:** Major. Prior-art rigor; not a substrate defect. Same class as the ADR 0061 council's Option-C "build our own WireGuard control plane" thin-rejection finding.

### F6 (Major, AP-1 / AP-21) — 1-hour cache TTL for `CommercialTier` is operationally wrong for Bridge subscription billing-cycle UX

**Where:** ADR 0062 §"Probe mechanics / Probe-cost classes":

> **High:** network round-trip (Bridge subscription verification; remote feature flag); 1s–5s wall-clock; cache TTL 1 hour with stale-while-revalidate

And the dimension table: *"CommercialTier | High | On-demand + 1-hour cache | Subscription start/end events; explicit operator action."*

**Operational-reality problem:** when a user upgrades their Bridge subscription (e.g., from `anchor-self-host` to `bridge-anchor`), they expect the upgrade reflected within seconds, NOT up to an hour. The 1-hour cache means an upgraded user sees `DegradationKind.DisableWithUpsell` ("Upgrade to Bridge tier") for up to an hour AFTER paying — the exact "user paid; UI still shows pay-to-upgrade" failure mode the protocol's §"User-communication policy" should structurally prevent.

The §"Re-evaluation triggers" canonical list mentions "Subscription start/end events from Bridge per ADR 0031 trigger CommercialTier re-probe" — but **see F8 below**: ADR 0031 does not specify a subscription-event-emitter contract that the coordinator could subscribe to. Without that contract, the only thing the coordinator has is the 1-hour TTL — and the user-paid-but-still-sees-upsell failure mode is structural.

**Three orthogonal fixes:**

- **(a)** Reduce `CommercialTier`'s cache TTL from 1 hour to 30–60 seconds. Cost: ~120× the Bridge-HTTP traffic (every minute vs every hour, per active client). For a Phase 2 deployment with N active clients, that's N HTTP calls/minute hitting Bridge. Quantify the cost; is it acceptable?
- **(b)** Add a Bridge → Anchor server-sent-events / WebSocket push channel that delivers subscription-change events. The coordinator subscribes; on event, `CommercialTier` is force-reprobed. This is the right architecture but requires Bridge-side machinery ADR 0031 has not specified and ADR 0062 cannot retroactively introduce.
- **(c)** Have the upgrade UI (which is already Bridge-side, per ADR 0031) call back to Anchor with a "subscription-just-changed; please re-probe" signal as part of the upgrade flow. Cost: ADR 0031 + Anchor coupling.

**Recommended:** Either (a) with explicit acceptance of the bandwidth cost + explicit naming of the upper-bound user-perceived staleness, OR document that the 1-hour cache is provisional pending a Bridge subscription-event-emitter (which becomes a halt-condition for Phase 2). The current ADR has neither.

**Severity:** Major. The user-experience failure mode is exactly what the negotiation protocol is supposed to prevent.

### F7 (Major, AP-1 / AP-3) — probe ordering / dependencies undefined

**Where:** ADR 0062 §"Probe mechanics" specifies per-dimension cost classes and re-evaluation triggers, but does NOT specify probe **ordering** or inter-dimension **dependencies**.

**Concrete operational gap:** the `Regulatory` dimension probes "jurisdiction × consent × policy"; jurisdiction is "IP geo + user-set jurisdiction." IP geo is a **network** operation. If the `Network` dimension probe says "no network available" (offline), the `Regulatory` jurisdiction-from-IP-geo probe cannot complete — but the coordinator runs probes either in parallel or in some implementation-defined order. Probes that depend on other probes' results need explicit ordering.

Other plausible probe-ordering dependencies:
- `CommercialTier` (Bridge HTTP round-trip) depends on `Network` (online status).
- `VersionVector` (federation handshake) depends on `Network` + `Trust`.
- `User` (identity-store query) may depend on `Hardware` (biometric sensor available).

**Impact:** Implementer-dependent. Stage 06 will either (a) implement probes as topologically-sorted sequential (correct but slow), (b) implement parallel with try-catch (handles missing-prereq probe failures via `CapabilityProbeFailure` audit but gates see partial-envelope verdicts), or (c) mix them ad-hoc. Spec is silent.

**Recommended:** Add §"Probe dependencies" specifying for each of the 10 dimensions: which other dimensions it depends on; what the coordinator does when a dependency is unavailable (default: emit `CapabilityProbeFailure` for the dependent dimension; mark the dimension's value as `Unreachable`/`Unknown`; coordinator's verdict for any gate consulting that dimension is `DegradationKind.HardFail` until the dependency probes successfully). This is also the right place to introduce the OQ-0062.5 "soft probe" mode the ADR's open question raises.

**Severity:** Major.

### F8 (Major, structural-citation; A7-third-direction class) — ADR 0031 is not a subscription-event-emitter; ADR 0009 has no "trust tier" or "commercial tier" enum

**Where:** ADR 0062 §"Decision / Initial contract surface":

```csharp
CommercialTier        CommercialTier, // per ADR 0009/0031
[...]
TrustCapabilities     Trust,          // per ADR 0009 trust tiers
```

And §"Re-evaluation triggers": *"Subscription start/end events from Bridge per ADR 0031."*

**Reality (verified `git show origin/main:docs/adrs/0009-foundation-featuremanagement.md` and `0031-bridge-hybrid-multi-tenant-saas.md`):**

- **ADR 0009** defines `FeatureKey`, `FeatureValueKind`, `FeatureValue`, `FeatureSpec`, `FeatureEvaluationContext`, `IFeatureCatalog`, `IFeatureProvider`, `IEntitlementResolver`, `IEditionResolver`, `IFeatureEvaluator`. It uses the noun **"Edition"** (e.g., `Edition?` on `FeatureEvaluationContext`; `FixedEditionResolver`). The terms "trust tier" and "commercial tier" do NOT appear in ADR 0009. (`grep -nE "trust tier|commercial tier" /tmp/adr-0009.md` returned 0 matches; verified.)

- **ADR 0031** is the Bridge Zone-C hybrid hosted-node-as-SaaS architecture; its scope is the multi-tenant SaaS overlay on top of the Anchor accelerator. It does NOT specify a subscription-event-emitter contract — it specifies the Bridge-as-managed-node deployment shape, per-tenant data isolation, and the relay-as-substrate use case from paper §17.2. The phrase "subscription start/end events from Bridge" is an ADR 0062 invention that ADR 0031 has not committed to.

**Two coupled errors:**

- ADR 0062 imports the noun **"trust tier"** (via `Trust per ADR 0009 trust tiers`) and **"commercial tier"** (via `CommercialTier per ADR 0009/0031`) — but neither phrase is in either cited ADR. The closer in-repo noun is `Edition` (ADR 0009).
- ADR 0062 cites a re-evaluation trigger (subscription events from Bridge) that ADR 0031 has not defined. F6 above named the operational consequence; F8 names the structural-citation gap behind it.

**Two equally-good fixes:**

- **(a)** Rename `CommercialTier` → `EditionCapabilities` (matching ADR 0009's `Edition` noun); rename `TrustCapabilities` → `TrustAnchorCapabilities` (closer to what's actually being captured); drop the ADR 0031 citation entirely (or replace with paper §17.2 Bridge-relay-substrate).
- **(b)** Add a halt-condition: ADR 0062 Stage 06 build cannot begin until ADR 0031 has been amended to specify a subscription-event-emitter contract (and ADR 0009 has been amended to introduce `TrustTier` / `CommercialTier` enums if those nouns survive the F2 rename). Pushing the work to follow-on ADR amendments.

**Recommended:** (a). The "Edition" noun is already the in-repo precedent and avoids two structural-citation gaps in a single rename pair.

**Severity:** Major. Two structural-citation failures in one finding. Same A7-third-direction shape as F1.

### F9 (Major, AP-1 / AP-3) — force-enable on a hardware-incapable device produces a Schrödinger feature

**Where:** ADR 0062 §"Per-feature force-enable surface":

> When a force-enable is active, capability gates that would have returned `Unavailable` instead return `DegradedAvailable` with `DegradationKind = DisableWithExplanation` and a `UserMessage` indicating the capability is force-enabled (so end-users see a "Force-enabled by admin" indicator on the affected feature, not a hidden state).

**Failure-mode question:** what happens when an admin force-enables a feature whose `Unavailable` verdict was driven by a **substrate-level** dimension the user's device cannot satisfy?

- **Example A (hardware):** admin force-enables "GPU-accelerated rendering" on a device with no GPU. Gate returns `DegradedAvailable` per the force-enable rule. Feature attempts to use GPU at runtime, crashes. Audit emits `CapabilityForceEnabled` (silent on the crash); user sees a crash with no usable explanation.
- **Example B (regulatory):** admin force-enables "background-check report generation" in a jurisdiction (`Regulatory.jurisdiction`) where ADR 0064 (queued) prohibits it. Gate returns `DegradedAvailable`; feature runs; user is now non-compliant.
- **Example C (commercial-tier):** admin force-enables a Bridge-paid feature on a self-host install. Gate returns `DegradedAvailable`; feature consumes Bridge resources without payment.

**Impact:** The force-enable surface as specified treats all dimensions uniformly — which is wrong. Some dimensions are **operator-overridable** (e.g., `User`-permission gates; "let this user use the feature even though their role doesn't normally allow it"); others are **substrate-hard** (`Hardware`, `Runtime` — force-enabling doesn't conjure capabilities the silicon doesn't have); and others are **policy-hard** (`Regulatory`, `CommercialTier` — force-enabling has legal/contractual consequences the operator may not have authority for).

**Recommended:** Add a `ForceEnablePolicy` taxonomy per dimension:

- **`Overridable`** — operator force-enable produces `DegradedAvailable` (e.g., `User`, some `Trust`).
- **`OverridableWithCaveat`** — operator force-enable produces `DegradedAvailable` + a hard-warning UX surface that names the substrate-level risk (e.g., `Regulatory` with jurisdiction caveat: "force-enable is legally your responsibility").
- **`NotOverridable`** — operator force-enable returns `Unavailable` regardless; the override request is rejected at `ForceEnableAsync` time with a clear error (e.g., `Hardware`, `Runtime`).

Spec which dimensions fall into which policy class. Default `Hardware` and `Runtime` to `NotOverridable`; `Regulatory` to `OverridableWithCaveat`; everything else to `Overridable`.

**Severity:** Major. Without this, force-enable is a vector for substrate-violation-via-admin.

### F10 (Major, AP-3 vague success criteria) — probe-failure UX surface unspecified for substrate-tier failures

**Where:** ADR 0062 mentions `CapabilityProbeFailure` audit emission but does NOT specify the user-facing UX surface for probe failures.

**Concrete gap:** `IDimensionProbe<HardwareCapabilities>.ProbeAsync` throws (e.g., OS API returns transient error). The coordinator catches, emits `CapabilityProbeFailure` audit, and... what? Does the coordinator return the last-known-good envelope? An envelope with `Hardware = null`? An envelope with `Hardware.IsProbeFailed = true` (a property the type doesn't have)? Whatever it does, what does the gate consuming `envelope.Hardware` do — return `Available`, `Unavailable`, `DegradedAvailable.HardFail`, throw, or return a probe-failure-specific verdict?

The §"Re-evaluation triggers" list does not mention "probe-failure" as a trigger; the §"User-communication policy" does not name probe-failure as one of its 4 cases (Expected / Unexpected / Recoverable / Informational). Probe-failure is a fifth case the protocol has not modeled.

**Recommended:** Add a `EnvelopeChangeSeverity = ProbeUnreliable` value (or equivalent) + spec the coordinator's behavior on probe-failure: (i) return the last-known-good envelope with the failed dimension flagged via a `ProbeStatus` enum (`Healthy` / `Stale` / `Failed` / `Unreachable`); (ii) gates that consume the failed dimension produce `DegradationKind.HardFail` verdicts with operator-targeted error; (iii) the failure is surfaced through the standard sync-state UI per ADR 0036 (if extending the 5-state encoding) or through a new envelope-state primitive (if extending). Pick one and spec it.

**Severity:** Major.

### F11 (Minor, AP-3) — localization mechanism for `UserMessage` unspecified

**Where:** ADR 0062 §"Decision / Per-feature gate contract" — `string? UserMessage; // localized; nullable if State == Available`.

**Reality:** The "localized" qualifier is not actionable. The substrate has multiple localization patterns (.resx for .NET on Anchor; i18next for React; Apple .strings for iOS). Without a spec, gates ship hardcoded English strings (the migration example in the ADR shows `UserMessage: "MyFeature requires Bridge subscription."` — hardcoded English).

**Recommended:** Spec the localization mechanism. Two options:

- **(a)** `UserMessage` is a localization key (e.g., `"capability.bridge.subscription_required"`); the rendering layer resolves the key against the active localization framework. Gates ship keys; resources hold the localized strings.
- **(b)** `UserMessage` is a `LocalizedString` value type (`{ key, defaultValue }` pair); the rendering layer prefers the key if a resource exists, falls back to the default-value if not. Friendlier to incremental localization rollout.

Recommended: (b). Friendlier to bootstrap; matches a common .NET pattern.

**Severity:** Minor (Encouraged).

### F12 (Minor, AP-3 / AP-18) — `CapabilityVerdictSurfaced` event missing

**Where:** ADR 0062 §"Telemetry shape" emits `CapabilityProbed` (per probe), `CapabilityChanged` (per verdict transition), `CapabilityProbeFailure` (per error). These cover the substrate-side events but NOT the user-facing surface.

**Concrete gap:** when does the user actually *see* a capability verdict on screen? `CapabilityChanged` fires on transitions, but a transition from `Unavailable` → `Unavailable` (e.g., dimension changed; verdict didn't) doesn't fire — yet the UI re-renders and the user sees the same banner again. Conversely, a verdict transition that's never rendered (because the user is on a different page) fires `CapabilityChanged` but produces no user-facing experience.

**Recommended:** Add `CapabilityVerdictSurfaced` audit event emitted at UI render-time (the rendering layer calls `IAuditTrail.AppendAsync` when it actually renders a verdict to screen). Payload: `(capability_key, verdict_state, degradation_kind, surface_id)`. Used for cohort analytics: "of users who hit `DisableWithUpsell`, how many actually saw the upsell vs how many auto-dismissed the banner before render?" This is the closing-the-loop event for product-roadmap analytics.

**Severity:** Encouraged.

### F13 (Minor, AP-1) — "no surprise modals" attributed to paper §13.2 but paper §13.2 does not contain the phrase

**Where:** ADR 0062 §"User-communication policy" — *"NO toast / modal — paper §13.2 explicitly forbids surprise modals."*

**Reality (verified `grep -nE "modal|surprise" _shared/product/local-node-architecture-paper.md`):** paper §13.2 specifies AP/CP visibility tables (staleness thresholds + UX treatments) and three always-visible status indicators ("node health, link status, data freshness ... non-intrusive under normal conditions, informative under degraded ones"). Paper §13.2 does NOT contain the phrase "no surprise modals" — that's an ADR 0062 inference. The closest match is the §13.2 framing of "non-intrusive under normal conditions, informative under degraded ones" + paper §13.1 ("Complexity Hiding Standard") which says nothing about modals specifically.

**Impact:** Low. The principle is correct — surprise modals violate the §13.1 + §13.2 spirit — but the load-bearing citation is over-precise. ADR 0062 reads as if §13.2 *literally* forbids modals; the paper does not.

**Recommended:** Reword to "consistent with paper §13.1 Complexity Hiding + §13.2 visibility-treatment framing (non-intrusive under normal conditions); banners — not modals — are the protocol's surface for user-actionable changes" rather than "paper §13.2 explicitly forbids surprise modals." Removes the structural-citation overreach without abandoning the principle.

Also: Critical-severity persistent banners that "cannot be dismissed without acknowledgment" are functionally close to a modal in user impact (the banner blocks user attention until acknowledged). The line between "persistent banner" and "modal" is genuinely thin; the ADR should either acknowledge that line or pin the visual distinction (e.g., banners occupy a fixed top-of-screen region; do not block click-through to the application; can be acknowledged inline).

**Severity:** Minor.

### F14 (Encouraged, AP-3 / AP-21) — verification commands are aspirational

**Where:** ADR 0062 §"Implementation checklist" includes `Cited-symbol verification per the 3-direction spot-check rule (positive + negative + structural-citation)` as a checkbox, but the ADR itself does NOT enumerate which symbols, which ADRs, or which structural citations the Stage 06 implementer is supposed to verify.

**Recommended:** Add §"A0 cited-symbol audit" (mirroring ADR 0028-A8.11 / A5.9 / A7.13 patterns) listing every `Sunfish.*` symbol cited in the ADR + every cited ADR + every cited paper section, classified as `Existing` / `Introduced by ADR 0062` / `Removed by ADR 0062`, with verification status. This is the discipline that catches F1 + F2 + F3 + F8 in this review; ADR 0062 should bake the discipline in at authoring time, not defer it to Stage 06.

**Severity:** Encouraged.

### F15 (verification-pass) — paper §13.2 AP/CP visibility table existence

Verified `_shared/product/local-node-architecture-paper.md:409-425` — section "13.2 AP/CP Visibility" exists with the staleness-threshold + UX-treatment table (Resource availability / Financial balances / Scheduled appointments / Team membership) + the three always-visible status indicators. ADR 0062's reference to "paper §13.2 visibility tables" is positively-existence-correct. (F13 is a separate finding about specific phrasing within §13.2, not the section's existence.)

### F16 (verification-pass) — ADR 0036 5-sync-state ARIA roles

Verified `docs/adrs/0036-syncstate-multimodal-encoding-contract.md:41-51` — the 5-state encoding table (`healthy` / `stale` / `offline` / `conflict` / `quarantine`) with ARIA roles (`status` / `alert`) and `aria-live` (`polite` / `assertive`). ADR 0062's reference to "ADR 0036 specifies 5 sync states with ARIA roles" is positively-existence-correct AND structurally correct.

Caveat (not a finding): ADR 0036's title is "**Multimodal Encoding Contract**," not "sync-state machine." 0062's framing (§"Probe mechanics" — *"SyncState | Live (per ADR 0036; already a live observable)"*) implies a state-machine-with-transitions surface. ADR 0036 specifies the visual encoding; whatever produces the SyncState transitions is upstream of 0036. Worth a one-line note in F-something but not load-bearing enough for a separate finding.

### F17 (verification-pass) — post-A8 `FormFactorProfile` shape matches what 0062's `MissionEnvelope` cites

Verified `docs/adrs/0028-crdt-engine-selection.md:471` (post-A8 surface) — the `FormFactorProfile` tuple ships with `formFactor`, `inputModalities`, `displayClass`, `networkPosture`, `storageBudgetMb`, `powerProfile`, `sensorSurface`, `instanceClass` per A5.1; A8.6 added the bidirectional round-trip verification gate (catch-all dictionary requirement); A8.4 added Phone↔Watch + CarPlay/Android-Auto rows. ADR 0062's `MissionEnvelope.FormFactor: FormFactorProfile` citation is correct as-of post-A8 surface (signed-off on `origin/main`).

The `instanceClass` enum reduction per A7.6 (`{ SelfHost, ManagedBridge }` — "Embedded" removed) is reflected in A5.1; ADR 0062's `commercialTier: "anchor-self-host"` JSON example is consistent.

### F18 (verification-pass) — post-A7 `VersionVector` plugin-shape matches what 0062's `MissionEnvelope` cites

Verified `docs/adrs/0028-crdt-engine-selection.md` post-A7 surface — A7.3.2 augmented `plugins` from `Map<PluginId, SemVer>` to `Map<PluginId, PluginVersionVectorEntry>` where `PluginVersionVectorEntry = { version: SemVer, required: bool }`. ADR 0062 cites `VersionVector per ADR 0028-A6/A7` — the citation correctly identifies A6 (the type) + A7 (the post-council mechanical fix). 0062 doesn't restate the `PluginVersionVectorEntry` shape (it doesn't need to — the embedded `VersionVector` is opaque to MissionSpace), so there's no shape-drift to flag.

### F19 (verification-pass) — ADR 0049 audit substrate accepts dedup pattern at emission boundary

Verified `docs/adrs/0049-audit-trail-substrate.md:135` ("The contract is intentionally narrow — append, query, retention. Future compliance features [...] extend through additional interfaces in the same package, not through new packages.") + ADR 0028-A6.5.1 ("dedup is enforced at the *emission* boundary, not the *substrate* boundary"). ADR 0062 §"Telemetry shape / Audit dedup per ADR 0028-A6.5.1 pattern" correctly identifies the emission-boundary as the dedup point. Citation correct.

### F20 (verification-pass) — `AuditEventType` constants — no naming collision for the 6 new constants

Verified `packages/kernel-audit/AuditEventType.cs` (full file read). The 6 new constants ADR 0062 introduces (`CapabilityProbed`, `CapabilityChanged`, `CapabilityProbeFailure`, `CapabilityForceEnabled`, `CapabilityForceRevoked`, `EnvelopeChangeBroadcast`) do not collide with any existing constant in the file:

- Existing capability-namespace constants: `CapabilityDelegated`, `CapabilityRevoked`, `LeasingPipelineCapabilityRevoked` (ADR 0046 + ADR 0057). Different verbs (`Delegated`, `Revoked` vs `Probed`, `Changed`, `Probe*Failure`, `ForceEnabled`, `ForceRevoked`).
- Note: `CapabilityRevoked` (existing — Phase 2 commercial scope) and `CapabilityForceRevoked` (ADR 0062) share the verb "Revoked" — semantically distinct (capability authorization revocation vs feature force-enable revocation). Recommended naming clarification: `CapabilityForceRevoked` → `FeatureForceEnableRevoked` per the F2 vocabulary clean-up (or equivalent). NOT a naming collision; minor cognitive overlap. Tag as Encouraged.
- Existing dedup-friendly constants: `WorkOrderEntryNoticeRecorded`, `LeaseDocumentVersionAppended` etc. demonstrate the existing AuditEventType pattern (PascalCase string-id; dedup at emission boundary).

No collision; all 6 new constants are namespacable cleanly. (See F2 + F8 + F11 amendments which may rename them as a side effect.)

### F21 (verification-pass) — `foundation-localfirst` sync-state surface exists for ADR 0062's Phase 2 migration claim

Verified `git ls-tree origin/main packages/foundation-localfirst/` — `SyncEngine.cs`, `SyncConflict.cs`, `OfflineStore.cs` all exist; `Sunfish.Foundation.LocalFirst.csproj` is the canonical package. ADR 0062 §"Compatibility plan / Affected packages / Modified: `packages/foundation-localfirst/`" correctly identifies the package. Phase 2 of the migration ("migrate ADR 0036 sync-state surface to consume `IMissionEnvelopeProvider` for sync-state observation") is operationally well-defined (the package exists; the surface is identifiable).

---

## 3. Recommended amendments

### A1 (REQUIRED, mechanical) — Drop the ADR 0041 citation; restate the actual antecedents (resolves F1)

In §"Context" (paragraph 1), §"Decision drivers" (bullet 5), and §"Compatibility plan / Migration order / Phase 4":

- Strike *"ADR 0041 specifies the rich-vs-MVP UI degradation primitive."*
- Replace with *"the runtime graceful-degradation taxonomy is what ADR 0062 itself introduces; the closest in-repo predecessors are paper §13.2 (visibility tables) + ADR 0036 (sync-state encoding contract) — both surface-treatment ADRs, not degradation primitives."*
- Remove Phase 4 from the migration order (renumber Phase 5+ as Phase 4+).
- Update §"References" — remove ADR 0041 (or move it to "tangentially relevant" with a one-line note explaining it does NOT specify a degradation primitive).

**Authority:** XO (mechanical — citation correction).

### A2 (REQUIRED, mechanical) — Rename `ICapability` / `ICapabilityGate` → `IFeature` / `IFeatureGate` to avoid `Foundation.Capabilities` collision (resolves F2)

Three coordinated renames:

```csharp
// Before:
public interface ICapability {}    // marker
public interface ICapabilityGate<TCapability> where TCapability : ICapability { ... }
public sealed record CapabilityVerdict(...);
public enum CapabilityState { Available, DegradedAvailable, Unavailable }

// After:
public interface IFeature {}
public interface IFeatureGate<TFeature> where TFeature : IFeature { ... }
public sealed record FeatureVerdict(...);
public enum FeatureAvailabilityState { Available, DegradedAvailable, Unavailable }
```

The `MissionEnvelope` + `IMissionEnvelopeProvider` + `EnvelopeChange` + `DimensionChangeKind` + `DegradationKind` + `EnvelopeChangeSeverity` types stay (they're not "capability"-named). Force-enable surface renames as `IFeatureForceEnableSurface`. The 6 new `AuditEventType` constants rename:

- `CapabilityProbed` → `FeatureProbed`
- `CapabilityChanged` → `FeatureAvailabilityChanged`
- `CapabilityProbeFailure` → `FeatureProbeFailed`
- `CapabilityForceEnabled` → `FeatureForceEnabled`
- `CapabilityForceRevoked` → `FeatureForceRevoked`
- `EnvelopeChangeBroadcast` → `MissionEnvelopeChangeBroadcast`

Side-benefit: matches ADR 0009's existing `FeatureKey` / `FeatureSpec` / `IFeatureEvaluator` vocabulary; ADR 0009 should be added to §"References" as the runtime-feature-availability antecedent (replacing the dropped ADR 0041 citation).

**Authority:** XO (mechanical — rename only; no semantic change).

### A3 (REQUIRED, mechanical) — Replace `Instant` with `DateTimeOffset` everywhere (resolves F3)

In §"Decision / Initial contract surface":

```csharp
// Before:
public sealed record MissionEnvelope(
    [...]
    Instant CapturedAt,
    [...]
);
public interface IBespokeSignal { Instant CapturedAt { get; } ... }
public sealed record CapabilityForceEnableRequest(string CapabilityKey, string Justification, Instant? ExpiresAt);

// After:
public sealed record MissionEnvelope(
    [...]
    DateTimeOffset CapturedAt,
    [...]
);
public interface IBespokeSignal { DateTimeOffset CapturedAt { get; } ... }
public sealed record FeatureForceEnableRequest(string FeatureKey, string Justification, DateTimeOffset? ExpiresAt);
```

JSON shape stays identical (ISO-8601 string; round-trippable via either type).

**Authority:** XO (mechanical — type swap matching substrate convention).

### A4 (REQUIRED) — Add §"Coordinator concurrency semantics" (resolves F4)

New sub-section after §"Cache vs live-probe":

> **Single-flight on cache-miss.** When N concurrent callers request `GetCurrentAsync` and the relevant dimension's cache is stale, the coordinator launches **1** probe; all N callers await the same probe completion. Implementation: per-dimension `Lazy<Task<TDimension>>` reset on cache-invalidation event.
>
> **Per-cost-class wall-clock timeout.** Each probe has a maximum wall-clock budget: Low: 1s; Medium: 2s; High: 5s; Live: N/A (Live is not on the timeout path). On timeout, the coordinator returns the last-known-cached value (or `Unreachable` sentinel if no prior value), emits `FeatureProbeFailed` audit, and treats the dimension as `ProbeStatus.Failed` (per F10 amendment) until a successful re-probe.
>
> **Observer fanout policy.** `IDisposable Subscribe(IMissionEnvelopeObserver)` registers an observer; the coordinator fans out `OnEnvelopeChangedAsync` calls concurrently (fire-and-forget; no back-pressure to the change source). Each observer's queue is bounded at 100 pending change events; observers exceeding the bound drop oldest-first with a `MissionEnvelopeObserverOverflow` audit event. Dimension changes within a 100ms coalescing window are merged into a single `EnvelopeChange` (the `ChangedDimensions` list is the union; `Previous` is the envelope at the start of the window; `Current` is the envelope at coalescing-flush time).

**Authority:** XO (introduces concurrency semantics; matches the ADR 0061 council's `ITransportSelector` resolution shape).

### A5 (REQUIRED) — Tighten DirectX-FL prior-art rationale; engage with Vulkan + RFC 5939 (resolves F5)

Rewrite §"Decision drivers" bullets 2 + 3:

> **Industry prior art is multi-source.** DirectX Feature Levels (FL_9_1 / FL_10_0 / FL_11_0 / FL_12_0) inspire the *enumerated-not-arithmetic* + *runtime-queryable* + *developer-owns-graceful-degradation* properties — but Mission Space explicitly does NOT inherit DirectX's *single-axis* or *OS-guarantees-joint-stability* properties (Mission Space is multi-axis; dimensions flip independently; no joint-stability guarantee). Vulkan's `VkPhysicalDeviceFeatures` (~50 boolean flags per device, each independently queryable, gates `vkCmd*` calls) is the closer multi-axis analog; Mission Space's 10 dimensions trade off finer-granularity (Vulkan) vs coarser-aggregation (DirectX) at a deliberate intermediate point. SDP RFC 5939's offer/answer pattern is engaged with in Option C below — Mission Space's coordinator-owns-state model is preferred over SDP's pure-publish/subscribe because diagnostics + telemetry-shape + operator-debugging all benefit from a state record (not just a change-event stream).

In §"Considered options / Option C" (pure-bus rejection) add:

> **Comparison to RFC 5939 (offer/answer):** RFC 5939 provides offer/answer semantics with both a state-record (the offer/answer pair) AND change events (re-INVITE re-negotiations). At first glance this is a hybrid that solves Option C's state-record gap. **However** — RFC 5939's state record is per-session (an SDP body inside a SIP transaction); Mission Space's needs are per-process (one envelope governing N feature-gates). The per-session shape would force every gate to negotiate its own envelope-fetch; Mission Space's coordinator-owns-state model centralizes the negotiation. The cost of RFC 5939's per-session-state is exactly what Option C inherits + Mission Space rejects.

**Authority:** XO (rationale tightening; no decision change).

### A6 (REQUIRED) — Spec `CommercialTier` cache TTL realistically, OR add Bridge subscription-event-emitter halt-condition (resolves F6 + F8)

Two coupled changes:

(i) §"Probe mechanics / Probe-cost classes" — split the `High` cost class into two:

```
- High: network round-trip; 1s–5s wall-clock; cache TTL **30 seconds** with stale-while-revalidate (default for billing-cycle-sensitive dimensions where users expect sub-minute reflection of changes — e.g., subscription state)
- DeepHigh: network round-trip; 1s–5s wall-clock; cache TTL 1 hour with stale-while-revalidate (for genuinely-rare-changing remote signals — e.g., feature-flag rollout where eventual consistency is acceptable)
```

`CommercialTier` (now `EditionCapabilities` per A2 + A8 below) goes in `High` (30-second TTL). 30 seconds × N active clients × 24h/day = bounded Bridge load per active session; cost is acceptable for the MVP scale.

(ii) §"Re-evaluation triggers" — replace bullet 5 ("Subscription start/end events from Bridge per ADR 0031") with:

> **Edition / commercial-tier changes.** ADR 0031 has not yet specified a Bridge → Anchor subscription-event-emitter contract. ADR 0062 Stage 06 build cannot ship a `EditionCapabilities` probe with sub-minute responsiveness UNTIL ADR 0031 is amended to add such a contract OR the 30-second cache TTL above is accepted as the operational ceiling. **Halt-condition added to §"Cohort discipline":** Phase 1 of the migration may NOT begin until either (a) the 30-second TTL is operationally acceptable per the Phase 1 acceptance review, or (b) ADR 0031 has been amended to specify the subscription-event-emitter contract.

**Authority:** XO (TTL adjustment + halt-condition addition; coordinates with F8 vocabulary fix).

### A7 (REQUIRED) — Add §"Probe dependencies" (resolves F7)

After §"Probe mechanics / Probe-cost classes," add:

> **Probe dependencies.** Some probes depend on other probes' results:
>
> | Dimension | Depends on | Behavior on dependency-unavailable |
> |---|---|---|
> | `Regulatory` (jurisdiction-from-IP-geo subset) | `Network` (online state) | Coordinator falls back to user-set jurisdiction; emits `FeatureProbeFailed` for the IP-geo subset; `Regulatory` returns with `ProbeStatus.PartiallyDegraded` |
> | `EditionCapabilities` (commercial tier) | `Network` (online state) | Coordinator returns last-known-good value with `ProbeStatus.Stale` if cache age < 24h; otherwise `ProbeStatus.Unreachable` and `EditionCapabilities` defaults to `anchor-self-host` |
> | `VersionVector` | `Network` + `Trust` | Coordinator returns last-known-good value with `ProbeStatus.Stale`; gates consulting `VersionVector` produce `DegradationKind.HardFail` if `ProbeStatus.Unreachable` |
> | `User` (biometric-auth-method subset) | `Hardware` (biometric sensor surface) | Coordinator returns user-without-biometric-method; emits `FeatureProbeFailed` for the biometric subset |
> | All other dimensions | None | N/A |
>
> The coordinator runs probes in **topologically-sorted dependency order** at startup + on full re-probe (`ProbeAsync`); probes within a dependency-level run in parallel; failures cascade per the table above. The `ProbeStatus` enum is `Healthy / Stale / Failed / PartiallyDegraded / Unreachable`; each `<TDimension>` record carries its own `ProbeStatus`.

**Authority:** XO (introduces probe-dependency semantics; coordinates with F10 + F4).

### A8 (REQUIRED) — Rename `CommercialTier` → `EditionCapabilities`; rename `Trust` → `TrustAnchorCapabilities`; drop unsupported ADR 0031 citation (resolves F8)

In §"Decision / Initial contract surface":

```csharp
// Before:
CommercialTier        CommercialTier, // per ADR 0009/0031
TrustCapabilities     Trust,          // per ADR 0009 trust tiers

// After:
EditionCapabilities   Edition,        // per ADR 0009 (Edition / IEditionResolver)
TrustAnchorCapabilities Trust,        // local-trust-anchor inspection (no in-repo predecessor; new in 0062)
```

Update DimensionChangeKind enum: `CommercialTier` → `Edition`; rename `IDimensionProbe<CommercialCapabilities>` → `IDimensionProbe<EditionCapabilities>`. Remove ADR 0031 from §"References" (or replace with paper §17.2 Bridge-relay-substrate citation if a Bridge anchor is needed). Update the JSON example: `"commercialTier": "anchor-self-host"` → `"edition": "anchor-self-host"` (or keep the string value; just rename the field).

If the council decides "Trust" needs an in-repo predecessor: cite ADR 0046 (recovery substrate) which defines trustee/trust-anchor semantics — but that's a stretch. Cleaner: explicitly note that `TrustAnchorCapabilities` is introduced by ADR 0062 with no in-repo predecessor.

**Authority:** XO (mechanical — rename + citation correction).

### A9 (REQUIRED) — Add `ForceEnablePolicy` taxonomy per dimension (resolves F9)

In §"Per-feature force-enable surface," after the description of force-enable behavior, add:

> **Force-enable policy per dimension.** The force-enable surface is gated by a `ForceEnablePolicy` per dimension:
>
> | Dimension | ForceEnablePolicy | Force-enable verdict |
> |---|---|---|
> | `Hardware` | `NotOverridable` | `ForceEnableAsync` rejected; throws `ForceEnableNotPermittedException("Hardware-driven Unavailable cannot be force-enabled; the substrate cannot conjure capabilities the device lacks.")` |
> | `Runtime` | `NotOverridable` | Same shape as Hardware. |
> | `Regulatory` | `OverridableWithCaveat` | Force-enable produces `DegradedAvailable` + UX surface naming legal/regulatory consequence ("Force-enable acknowledges the operator assumes responsibility for jurisdictional non-compliance.") |
> | `EditionCapabilities` | `OverridableWithCaveat` | Force-enable produces `DegradedAvailable` + UX surface naming the contractual consequence ("Force-enable bypasses subscription gating; usage may incur Bridge-tier costs not covered by current subscription.") |
> | `User`, `Network`, `Trust`, `SyncState`, `VersionVector`, `FormFactor` | `Overridable` | Force-enable produces `DegradedAvailable` per the existing rule. |
>
> `ICapabilityForceEnableSurface.ForceEnableAsync<TFeature>` checks the relevant dimension's `ForceEnablePolicy` before applying; rejection emits `FeatureForceEnableRejected` audit event. (New AuditEventType constant: 7 total constants instead of 6.)

**Authority:** XO (substantive — dimension policy taxonomy).

### A10 (REQUIRED) — Add `ProbeStatus` + `EnvelopeChangeSeverity.ProbeUnreliable` (resolves F10)

Two coupled additions:

(i) Add `ProbeStatus` enum:

```csharp
public enum ProbeStatus
{
    Healthy,            // probe succeeded; result is fresh
    Stale,              // probe succeeded earlier; result is stale per cache TTL but the dimension's value is still trusted
    Failed,             // probe attempted; threw / timed out; last-known-good value returned per F10 amendment
    PartiallyDegraded,  // probe succeeded but a sub-component failed (e.g., Regulatory IP-geo subset failed; user-set subset fine)
    Unreachable         // probe not attempted (e.g., dependency unavailable per A7); last-known-good or sentinel returned
}
```

Each `<TDimension>` record carries `ProbeStatus Status { get; }` as an additional field (introduces a contract change to the 10 dimension records — Stage 06 implementation work).

(ii) Add `EnvelopeChangeSeverity.ProbeUnreliable`:

```csharp
public enum EnvelopeChangeSeverity
{
    Informational,
    Warning,
    Critical,
    ProbeUnreliable  // (NEW) coordinator could not produce a fresh probe; UI surfaces a "diagnostics check required" indicator per ADR 0036's quarantine state
}
```

UX surface: same as Critical (persistent banner, operator-targeted) but the recovery action is "Open diagnostics" rather than "Acknowledge."

**Authority:** XO (substantive — new probe-failure modeling).

### A11 (Encouraged) — Spec `UserMessage` localization mechanism (resolves F11)

In §"Decision / Per-feature gate contract," replace:

```csharp
string? UserMessage,  // localized; nullable if State == Available
```

with:

```csharp
LocalizedString? UserMessage,  // nullable if State == Available
[...]

public sealed record LocalizedString(
    string Key,                  // localization key; rendering layer resolves against active framework
    string DefaultValue          // fallback English string if key is missing
);
```

Same shape applies to `OperatorRecoveryAction`. The rendering layer (Anchor MAUI uses .resx; Bridge React uses i18next; iOS uses .strings) consumes `LocalizedString` and resolves per its framework convention.

**Authority:** XO (mechanical — friendlier-to-bootstrap localization shape).

### A12 (Encouraged) — Add `FeatureVerdictSurfaced` audit event (resolves F12)

Add seventh new `AuditEventType` constant:

- `FeatureVerdictSurfaced` — emitted at UI-render time (NOT at gate-evaluate time) when a verdict is surfaced to a user-visible UI element; payload `(feature_key, verdict_state, degradation_kind, surface_id)`. Used for product-roadmap analytics ("did the user actually see this verdict, or was it gated behind a route-change before render?").

§"Telemetry shape" gains a fourth bullet describing the event. Audit dedup: `FeatureVerdictSurfaced` capped at 1-per-(feature_key, surface_id)-per-30-second-window.

**Authority:** XO (substantive — closes the loop on UI-side telemetry).

### A13 (Encouraged) — Reword "no surprise modals" attribution (resolves F13)

In §"User-communication policy," replace:

> NO toast / modal — paper §13.2 explicitly forbids surprise modals.

with:

> NO toast / modal for unexpected substrate-detected changes. Consistent with paper §13.1 "Complexity Hiding Standard" + §13.2's framing of UX as "non-intrusive under normal conditions, informative under degraded ones." Banners — not modals — are the protocol's surface for user-actionable changes; the visual distinction: banners occupy a fixed top-of-screen region; do not block click-through to the application; can be acknowledged inline.

**Authority:** XO (mechanical — citation correction + acknowledging modal-vs-banner line).

### A14 (Encouraged) — Add §"Cited-symbol audit" (resolves F14)

Add §"A0 cited-symbol audit" mirroring ADR 0028-A8.11 / A5.9 / A7.13. Lists every `Sunfish.*` symbol cited in the ADR + every cited ADR + every cited paper section, classified `Existing` / `Introduced by ADR 0062` / `Removed by ADR 0062`, with verification status. This council review's §2 verification-passes (F15-F21) are the seed; the ADR should bake the discipline in at authoring time.

**Authority:** XO (mechanical — cohort-discipline pattern adoption).

---

## 4. Quality rubric grade

**B (low-B).** Earns C trivially: all 5 CORE present (Context, Decision drivers, Considered options, Decision, Consequences); multiple CONDITIONAL (Compatibility plan, Open questions, Revisit triggers, References, Cohort discipline, Implementation checklist). No AP-2 / AP-4 / AP-5 critical violations.

Earns B via: 4 honest options triangulated (A pure-coordinator, B status quo, C bus, D hybrid; D adopted as A specialization); industry prior art surveyed (DirectX FL, Vulkan, SDP, TLS, OpenGL, WebRTC, HTTP); 5-value `DegradationKind` taxonomy is exhaustive over the surfaced cases; Confidence/Cold-Start-Test self-assessment (cohort-discipline section); explicit cohort-batting-average framing ("12-of-12 substrate amendments needing council fixes" + structural-citation rate); auto-merge intentionally disabled per the discipline.

Falls short of A on:

- **F1 + F8 (structural-citation; 2 instances)** — repeats the A7-third-direction lesson for the 6th XO-authored substrate amendment (per cohort metric). Caught pre-merge here, but the pattern is now "default expected" rather than "occasional miss."
- **F2 (substrate-vocabulary collision)** — same Cold-Start-failure shape as ADR 0061's `NodeId` vs `PeerId`. The lesson should be internalized; introducing `Foundation.MissionSpace.ICapability` next to existing `Foundation.Capabilities.ICapabilityGraph` repeats the cohort drift.
- **F4 + F7 + F10 (substrate-protocol-completeness gaps)** — coordinator concurrency, probe dependencies, and probe-failure-UX are all unspec'd. Same shape as the ADR 0061 council's `ITransportSelector` failover-semantics gap. Each is in-ADR fixable; each is load-bearing.

The Critical pair (F1 + F2) + the load-bearing Major (F8) are tractable: F1 is a 5-minute citation strip; F2 is a coordinated rename across 6 types + 6 audit constants; F8 is a rename + citation correction. The remaining Major findings (F3 + F4 + F5 + F6 + F7 + F9 + F10) require ~1-2 hours of editing each. With A1-A10 landed (the REQUIRED amendments), this ADR clears A on a re-review.

---

## 5. Council perspective notes (compressed)

### 5.1 Distributed-systems / runtime-protocol reviewer (drove F4, F6, F7, F10)

The 10-dimension partition is mostly clean, but the dimensions are NOT jointly stable — `Network`, `CommercialTier`, `User`, `SyncState` flip on time-scales from milliseconds (SyncState) to seconds (Network) to minutes (User sign-in). The "single source of truth" pro doesn't hold under joint-instability without explicit coordination semantics. F4 names the concurrency gaps; F7 names the probe-dependency gaps; F10 names the probe-failure UX gap. F6 specifically: 1-hour cache for `CommercialTier` is wrong against the BDFL property business's Phase 2 commercial-tier UX expectation (sub-minute reflection of subscription state); the protocol's stale-while-revalidate window leaks "user paid; UI still says pay-to-upgrade" by design. The substrate-tier dependency-graph review (`Sunfish.Foundation.MissionSpace`) is named in §"Consequences / Negative" but not concretely scoped — every package consuming capability state will pull in MissionSpace; that's a substrate-tier coupling decision worth an ADR-cited dependency-graph-review checkpoint.

The coordinator-as-chokepoint question is real: under thread-pool starvation, a misbehaving High-cost probe blocks `GetCurrentAsync` indefinitely if the spec doesn't pin timeouts. F4 addresses; A4 amendment is mechanical.

### 5.2 Industry-prior-art reviewer (drove F5)

DirectX Feature Levels are the right *starting* analog — "enumerated, not arithmetic" is the strongest property to inherit — but the analogy under-extends. DirectX FL is single-axis and OS-guaranteed; Mission Space is multi-axis and runtime-volatile. Vulkan's `VkPhysicalDeviceFeatures` (~50 boolean flags) is the closer multi-axis analog; ADR 0062 names it once but doesn't engage. Apple `os.activity` + iOS Network framework `NWPathMonitor` for the Network dimension would be worth a paragraph (the iOS adapter author will find this gap); browser Permissions API + MediaCapabilities API for browser/PWA case (per ADR 0028-A8.8) — neither cited in 0062. SDP RFC 5939 is dismissed without analysis; the dismissal should engage with offer/answer and explicitly name why coordinator-owns-state beats per-session-state for diagnostics + telemetry (A5 amendment does this).

### 5.3 Cited-symbol / cohort-discipline reviewer (drove F1, F2, F3, F8 + verification passes F15–F21)

3-direction spot-check applied to all ADR and Sunfish.* symbol citations:

- **Positive-existence.** ADR 0036 (5-state ARIA roles): verified F16. Paper §13.2: verified F15. Post-A8 `FormFactorProfile`: verified F17. Post-A7 `VersionVector`: verified F18. ADR 0049 audit substrate: verified F19. AuditEventType non-collision: verified F20. foundation-localfirst surface: verified F21.
- **Negative-existence.** No `NodaTime.Instant` anywhere in `packages/`: verified F3. No "trust tier" or "commercial tier" phrase in ADR 0009: verified F8. No subscription-event-emitter contract in ADR 0031: verified F8.
- **Structural-citation correctness (A7-third-direction).** ADR 0041 cited as "rich-vs-MVP UI degradation primitive" — structurally false; ADR 0041 is a component-pair-coexistence policy: F1. ADR 0031 cited as a subscription-event-emitter — structurally false; ADR 0031 is a Bridge multi-tenant SaaS architecture ADR: F8 (second instance). Total structural-citation failures this review: **2**, both Critical/Major.

Cohort metric: **6-of-13 XO-authored substrate amendments now have at least one structural-citation failure caught by pre-merge council.** Roughly half the cohort. Pattern: keyword-grep is insufficient for "this ADR specifies primitive X" claims; reading the cited ADR's actual surface beats keyword-match.

### 5.4 UX / user-communication reviewer (drove F9, F11, F12, F13)

The "no surprise modals" rule is correct in spirit but over-precise in citation (F13). The 4-value `EnvelopeChangeSeverity` is mostly right, but persistent-banner-Critical that "cannot be dismissed without acknowledgment" is functionally close to a modal — pin the visual distinction. Force-enable-on-substrate-incapable-device is the most surprising failure mode (F9): admin force-enables GPU on no-GPU device, gate says `DegradedAvailable`, feature crashes, audit silent on the crash. The `ForceEnablePolicy` per-dimension taxonomy is the right fix. Localization is hand-waved (F11); the `LocalizedString` value type is the friendliest bootstrap. The telemetry triple (`Probed` / `Changed` / `ProbeFailure`) misses the closing-the-loop event — does the user actually see the verdict? `FeatureVerdictSurfaced` (F12) addresses. Probe-failure UX (F10) is its own thing — `EnvelopeChangeSeverity.ProbeUnreliable` + `ProbeStatus.Failed` is the cleanest model.

---

## 6. Cohort discipline scorecard

| ADR amendment | Pre-merge council? | Substrate amendment? | Post-acceptance amendments needed | Structural-citation failures |
|---|---|---|---|---|
| 0046-A2 | ✗ skipped | yes | 5 | 1 (F1 council A2) |
| 0051 | ✓ ran | yes | 4 | 0 |
| 0052 | ✓ ran | yes | 3 | 0 |
| 0053 | ✓ ran | yes | 2 | 1 |
| 0054 | ✓ ran | yes | 3 | 0 |
| 0058 | ✓ ran | yes | 3 | 0 |
| 0059 | ✓ ran | yes | 2 | 0 |
| 0046-A4 | ✓ ran | yes | 3-4 mechanical | 0 |
| 0061 | ✓ ran | yes | 4 | 1 (`NodeId`/`PeerId`) |
| 0028-A6 → A7 | ✓ ran | yes | 6 mechanical + 4 minor | 1 (`required: true / ModuleManifest`) |
| 0028-A5 → A8 | ✓ ran | yes | 6 mechanical + 4 minor | 2 (per-tenant key surface; `IFieldDecryptor` namespace) |
| 0048-A1 | ✓ ran | yes | 4 mechanical | 0 |
| 0028-A1 | ✓ ran | yes | 5 mechanical | 0 |
| **0062 (this review)** | ✓ ran | yes | **10 required + 4 encouraged** | **2 (F1 ADR 0041; F8 ADR 0031)** |

**Cumulative metrics post-0062:**

| Metric | Pre-0062 | Post-0062 |
|---|---|---|
| Substrate-amendment council batting average | 12-of-12 | **13-of-13** |
| Council false-claim rate (across all 3 directions) | 2-of-10 (per `feedback_decision_discipline`) | 2-of-11 (no new false claims this review) |
| Structural-citation-failure rate (XO-authored) | 5-of-12 | **6-of-13** (~46% of substrate amendments have at least one structural-citation failure) |
| Pre-merge council Major-finding catch rate | 100% on substrate amendments where council ran | 100% (this review caught F1 + F2 + F8 pre-merge) |

**Pattern reaffirmed:** pre-merge council on substrate ADRs is structurally canonical; structural-citation failures are now the most common XO-authored failure mode (6-of-13, exceeding cited-symbol drift in Major-or-worse severity). The discipline upgrade: read the cited ADR's actual surface for every "ADR X specifies primitive Y" claim before authoring; keyword-grep + ADR-title-skim is insufficient.

---

## 7. Closing recommendation

**Required mechanical fixes (XO authority; can land as a single mechanical follow-up amendment to PR #406's branch):**

- **A1** — drop ADR 0041 citation; restate antecedents as paper §13.2 + ADR 0036.
- **A2** — rename `ICapability` / `ICapabilityGate` / `CapabilityVerdict` / etc. → `IFeature` / `IFeatureGate` / `FeatureVerdict`; rename 6 `AuditEventType` constants accordingly.
- **A3** — replace `Instant` with `DateTimeOffset` everywhere.
- **A8** — rename `CommercialTier` → `EditionCapabilities` (Edition); rename `Trust` → `TrustAnchorCapabilities`; drop unsupported ADR 0031 citation.

**Required substantive fixes (XO authority; mechanical-to-author once design choices are pinned in this review):**

- **A4** — coordinator concurrency semantics (single-flight, timeouts, observer fanout).
- **A5** — DirectX-FL prior-art rationale tightening + Vulkan + RFC 5939 engagement.
- **A6** — `EditionCapabilities` cache TTL realistic + halt-condition for sub-minute responsiveness pending ADR 0031 amendment.
- **A7** — probe dependencies table.
- **A9** — `ForceEnablePolicy` per dimension.
- **A10** — `ProbeStatus` + `EnvelopeChangeSeverity.ProbeUnreliable`.

**Encouraged (XO authority; nice-to-have but not blocking):**

- **A11** — `LocalizedString` value type for `UserMessage`.
- **A12** — `FeatureVerdictSurfaced` audit event.
- **A13** — modal-vs-banner reattribution.
- **A14** — §"Cited-symbol audit" baked in.

**Sibling-amendment dependencies:** ADR 0031 needs amendment to specify Bridge → Anchor subscription-event-emitter contract before ADR 0062 Phase 1 build can ship sub-minute `EditionCapabilities` responsiveness (per A6 halt-condition). Stage 06 implementer should not begin migration Phase 1 work until A1-A10 land and (a) the 30-second TTL is operationally accepted OR (b) ADR 0031 amendment is Accepted on `origin/main`. ADR 0009 should gain `EditionCapabilities` / `TrustAnchorCapabilities` citation cross-references after A8 lands (mutual reference; no spec changes to ADR 0009).

**No findings escalate to CO.** All 14 amendments are mechanical / substrate-protocol-completeness / vocabulary-cleanup / encouraged. XO has authority per Decision Discipline Rule 3. Recommended path: XO applies A1-A10 as a single mechanical follow-up amendment on PR #406's branch; re-runs cited-symbol audit per A14; auto-merge re-enabled after the follow-up amendment passes a second-pass spot-check.

**Standing rung-6 spot-check** within 24h of ADR 0062 (post-amendment) merging, per the cohort-discipline §"Standing rung-6 spot-check" commitment.
