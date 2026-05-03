# Hand-off — Foundation.Versioning substrate Phase 1 (ADR 0028-A6+A7 contract surface)

**From:** XO (research session)
**To:** sunfish-PM session (COB)
**Created:** 2026-05-01
**Status:** `ready-to-build`
**Spec source:** [ADR 0028 amendments A6 + A7](../../docs/adrs/0028-crdt-engine-selection.md) (post-A7 council-fixed surface; landed via PR #395)
**Approval:** ADR 0028-A6+A7 Accepted on origin/main; council review at `icm/07_review/output/adr-audits/0028-A6-council-review-2026-04-30.md` (PR #396, merged); A7 absorbed all 10 council recommendations
**Estimated cost:** ~12–16h sunfish-PM (foundation-tier package scaffold + ~10 type signatures + handshake protocol + 2 service interfaces + audit factory + ~30–40 tests + DI + apps/docs page)
**Pipeline:** `sunfish-feature-change`
**Audit before build:** `ls /Users/christopherwood/Projects/Sunfish/packages/ | grep -E "^foundation-versioning"` to confirm no collision (audit not yet run; COB confirms before Phase 1 commit)

---

## Context

Phase 1 lands the Foundation.Versioning substrate's core types + handshake protocol + audit emission per the post-A7 ADR 0028-A6 surface. Subsequent phases ship:

- **W#23 / W#28 / federation consumers** (separate workstreams) — wire the handshake into actual peer-discovery code paths
- **A1.x companion (iOS envelope capture-context tagging)** — separate intake at `icm/00_intake/output/2026-04-30_ios-envelope-capture-context-tagging-intake.md` (PR #397); will land as its own W# when CO promotes
- **A5+A8 cross-form-factor migration substrate** — separate W# when authored (currently authored as `Sunfish.Foundation.Migration`; potentially a separate W#35)

This hand-off scope is **substrate types + handshake protocol + reference implementation + audit emission**. Substrate-only; no consumers wired in this hand-off. Concrete enough to unblock:

- W#23 iOS Field-Capture App's federation-handshake path (currently `ready-to-build` per ledger row 23; consumes this substrate at Phase 4 of that workstream)
- W#28 Public Listings cross-version peer compatibility (currently `built` for substrate; consumes this substrate when capability-tier extends to multi-version anchor pairing)
- ADR 0028-A6.4 + A7.4 audit emission (`VersionVectorIncompatibilityRejected` + `LegacyDeviceReconnected`) — substrate that future federation-handshake code paths emit

---

## Files to create

### Package scaffold

```
packages/foundation-versioning/
├── Sunfish.Foundation.Versioning.csproj
├── README.md
├── DependencyInjection/
│   └── ServiceCollectionExtensions.cs        (AddInMemoryVersioning)
├── Models/
│   ├── VersionVector.cs                       (record; the post-A7 tuple shape)
│   ├── PluginVersionVectorEntry.cs            (record; per-A7.3 augmented map shape)
│   ├── VersionVectorVerdict.cs                (record; per-A7.1 two-phase commit message)
│   ├── PluginId.cs                            (readonly record struct; opaque ID)
│   ├── AdapterId.cs                           (readonly record struct; opaque ID)
│   ├── ChannelKind.cs                         (enum: Stable, Beta, Nightly)
│   ├── InstanceClassKind.cs                   (enum: SelfHost, ManagedBridge per A7.6)
│   ├── FailedRule.cs                          (enum per A6.4 + A7.x; canonical names below)
│   └── VerdictKind.cs                         (enum: Compatible, Incompatible)
├── Services/
│   ├── IVersionVectorExchange.cs              (handshake interface; per A6.3 + A7.1)
│   ├── IVersionVectorIncompatibility.cs       (rejection-handler interface; per A6.4)
│   ├── InMemoryVersionVectorExchange.cs       (reference impl; thread-safe; in-process)
│   └── InMemoryVersionVectorIncompatibility.cs (reference impl)
├── Compatibility/
│   ├── ICompatibilityRelation.cs              (rule-evaluator interface; per A6.2 + A7.3)
│   └── DefaultCompatibilityRelation.cs        (reference impl; 6 rules per A6.2 + A7.3 augmentation)
├── Audit/
│   └── VersionVectorAuditPayloads.cs          (factory; mirrors LeaseAuditPayloadFactory pattern)
├── Encoding/
│   └── VersionVectorCanonicalEncoding.cs      (camelCase round-trip per A7.8; via CanonicalJson.Serialize)
└── tests/
    └── Sunfish.Foundation.Versioning.Tests.csproj
        ├── VersionVectorTests.cs              (encoding round-trip; canonical-JSON shape; camelCase per A7.8)
        ├── PluginVersionVectorEntryTests.cs   (post-A7.3 plugin map shape)
        ├── CompatibilityRelationTests.cs      (6 rules per A6.2; one test per rule + edge cases)
        ├── HandshakeProtocolTests.cs          (post-A7.1 two-phase verdict commit; both peers must agree)
        ├── ReceiveOnlyModeTests.cs            (per A6.5 one-sided receive-only for legacy reconnect)
        ├── AuditEmissionTests.cs              (2 AuditEventType constants emit on the right triggers)
        ├── AuditDedupTests.cs                 (per A7.4: 1-hour for incompat, 24-hour for legacy reconnect)
        └── VersionVectorEncodingTests.cs      (verify camelCase canonical shape; verify post-A7.6 enum values)
```

### Type definitions (post-A7 surface; implement exactly)

```csharp
namespace Sunfish.Foundation.Versioning;

// Identity types
public readonly record struct PluginId(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct AdapterId(string Value)
{
    public override string ToString() => Value;     // e.g., "blazor", "react", "maui-blazor"
}

// Enum types (post-A7.6 reduced + canonical naming per A7.8)
public enum ChannelKind { Stable, Beta, Nightly }
public enum InstanceClassKind { SelfHost, ManagedBridge }    // Embedded stripped per A7.6
public enum VerdictKind { Compatible, Incompatible }

public enum FailedRule
{
    KernelSemverWindow,           // A6.2 rule 2 (post-A7.2 honest framing)
    SchemaEpochMismatch,          // A6.2 rule 1
    RequiredPluginIntersection,   // A6.2 rule 3 (post-A7.3 augmented)
    AdapterSetIncompatible,       // A6.2 rule 4
    ChannelOrdering,              // A6.2 rule 5 (post-A9 reword)
    InstanceClassIncompatible     // A6.2 rule 6 (cross-instance OK by default)
}

// Wire-format types
public sealed record VersionVector(
    string                                      Kernel,        // SemVer string; e.g., "1.3.0"
    IReadOnlyDictionary<PluginId, PluginVersionVectorEntry> Plugins,  // post-A7.3 augmented
    IReadOnlyDictionary<AdapterId, string>      Adapters,      // map of AdapterId → SemVer string
    uint                                        SchemaEpoch,
    ChannelKind                                 Channel,
    InstanceClassKind                           InstanceClass
);

public sealed record PluginVersionVectorEntry(
    string  Version,    // SemVer string
    bool    Required    // per A7.3.2; required-flag carried on the wire so rule-3 evaluation symmetrizes
);

public sealed record VersionVectorVerdict(    // post-A7.1 two-phase commit message
    VerdictKind     Verdict,
    FailedRule?     FailedRule,         // set iff Verdict == Incompatible
    string?         FailedRuleDetail    // set iff Verdict == Incompatible; localizable later
);

// Service contracts
public interface IVersionVectorExchange
{
    /// <summary>
    /// Performs the post-A7.1 two-phase verdict commit handshake against a peer.
    /// Returns the local node's verdict; the protocol caller is responsible for receiving the peer's verdict
    /// and BOTH peers must agree Compatible for federation to proceed (per A7.1.3c).
    /// </summary>
    ValueTask<VersionVectorVerdict> EvaluateAsync(
        VersionVector localVector,
        VersionVector peerVector,
        CancellationToken ct = default
    );
}

public interface IVersionVectorIncompatibility
{
    /// <summary>
    /// Records a rejection for audit + UX surface emission.
    /// Implementation MUST honor A7.4 dedup: 1-per-(remote_node_id, failed_rule, failed_rule_detail) per 1-hour rolling window.
    /// </summary>
    ValueTask RecordRejectionAsync(
        string                  remoteNodeId,
        VersionVectorVerdict    verdict,
        CancellationToken       ct = default
    );

    /// <summary>
    /// Records a legacy device reconnect (one-sided receive-only mode per A6.5).
    /// Implementation MUST honor A7.4 dedup: 1-per-(remote_node_id, kernel_minor_lag) per 24-hour rolling window.
    /// </summary>
    ValueTask RecordLegacyReconnectAsync(
        string              remoteNodeId,
        string              remoteKernel,
        int                 kernelMinorLag,
        CancellationToken   ct = default
    );
}

// Compatibility rule engine
public interface ICompatibilityRelation
{
    VersionVectorVerdict Evaluate(VersionVector v1, VersionVector v2);
}

public sealed class DefaultCompatibilityRelation : ICompatibilityRelation
{
    private const uint MaxKernelMinorLag = 2;    // A6.2 rule 2 default; per A7.2 made tunable in future
    public VersionVectorVerdict Evaluate(VersionVector v1, VersionVector v2);
}
```

### Audit constants

`AuditEventType` MUST gain 2 new constants in `packages/kernel-audit/AuditEventType.cs`:

```csharp
public static readonly AuditEventType VersionVectorIncompatibilityRejected = new("VersionVectorIncompatibilityRejected");
public static readonly AuditEventType LegacyDeviceReconnected            = new("LegacyDeviceReconnected");
```

`VersionVectorAuditPayloads` factory mirrors `LeaseAuditPayloadFactory` shape (keys alphabetized; canonical-JSON-serialized; per ADR 0049 emission contract):

```csharp
namespace Sunfish.Foundation.Versioning.Audit;

public static class VersionVectorAuditPayloads
{
    public static AuditPayload IncompatibilityRejected(string remoteNodeId, FailedRule rule, string detail);
    public static AuditPayload LegacyReconnected(string remoteNodeId, string remoteKernel, int kernelMinorLag);
}
```

---

## Phase breakdown (~5 PRs, ~12–16h total)

### Phase 1 — Substrate scaffold + core types (~2–3h, 1 PR)

- Package created at `packages/foundation-versioning/` with `Microsoft.NET.Sdk.Razor`-style csproj (foundation-tier; depends on `foundation` only)
- All types in `Models/` per the spec block above
- `VersionVector` round-trip via `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` test (verify camelCase shape + plugin map shape per A7.3 + post-A7.6 instanceClass enum has 2 values)
- README.md per the standard package-README pattern
- ~6–8 unit tests on Models alone

**Acceptance:**
- [ ] All Models compile + serialize cleanly
- [ ] Canonical-JSON shape matches A7.8 example (camelCase keys; PluginVersionVectorEntry shape; instanceClass enum values)
- [ ] 0-warnings build
- [ ] PR description names which post-A7 sub-amendments are wired (A7.1, A7.3, A7.6, A7.8 in this Phase)

### Phase 2 — Compatibility rule engine + DefaultCompatibilityRelation (~3–4h, 1 PR)

- `ICompatibilityRelation` + `DefaultCompatibilityRelation` per the spec
- 6 compatibility-rule tests (one per A6.2 rule)
- 4 rejection-error tests (one per `FailedRule` enum value)
- Schema-epoch hard-rejection test
- Adapter set difference DOES NOT block federation (A6.2 rule 4 specifically)
- Channel ordering rule 5 per A7.9 reword (more-permissive → less-permissive direction)
- Required-plugin intersection per A7.3 augmentation (both sides consult plugin-map's required flag union)

**Acceptance:**
- [ ] All 6 compatibility rules covered by unit tests
- [ ] Test coverage for asymmetric-evaluation pathology resolution per A7.1: when peers disagree on channel rule (Stable peer sees Nightly peer; Nightly peer sees Stable peer) the verdict-commit MUST resolve to Incompatible from BOTH sides

### Phase 3 — Handshake protocol + IVersionVectorExchange (~2–3h, 1 PR)

- `IVersionVectorExchange` + `InMemoryVersionVectorExchange` per the spec
- Handshake-protocol unit tests covering post-A7.1 two-phase verdict commit
- Both-peers-must-agree test (one-sided incompatible → both peers tear down cleanly)
- Receive-only mode test per A6.5 (one-sided receive-only for kernel_minor_lag > 2)

**Acceptance:**
- [ ] Two-phase commit semantics verified: peer A's verdict + peer B's verdict; both agree → proceed; disagreement → both tear down
- [ ] Receive-only mode test passes for legacy device pattern

### Phase 4 — Audit emission + dedup wiring (~2–3h, 1 PR)

- 2 new `AuditEventType` constants in `packages/kernel-audit/AuditEventType.cs`
- `VersionVectorAuditPayloads` factory (alphabetized keys per ADR 0049 convention)
- `IVersionVectorIncompatibility` + `InMemoryVersionVectorIncompatibility` per the spec
- A7.4 dedup wired at the emission boundary:
  - `VersionVectorIncompatibilityRejected`: 1-per-(remote_node_id, failed_rule, failed_rule_detail) tuple per 1-hour rolling window
  - `LegacyDeviceReconnected`: 1-per-(remote_node_id, kernel_minor_lag) tuple per 24-hour rolling window
- Audit dedup tests for both emission types (rapid-reconnect storm; misconfigured-peer-retry storm)

**Acceptance:**
- [ ] Both audit constants compile + register in `AuditEventType` static class
- [ ] Dedup cache resets correctly (worst-case duplicate emission acceptable per A7.4)
- [ ] All 6 emission scenarios from A6.6 acceptance criteria covered by tests

### Phase 5 — DI extension + apps/docs + ledger flip (~1–2h, 1 PR)

- `AddInMemoryVersioning()` DI extension registering `IVersionVectorExchange`, `IVersionVectorIncompatibility`, `ICompatibilityRelation`
- `apps/docs/foundation-versioning/overview.md` walkthrough page (cite ADR 0028 + post-A7 surface explicitly)
- Active-workstreams.md row 34 flipped from `building` → `built` with PR list

**Acceptance:**
- [ ] `AddInMemoryVersioning()` registers all 3 interfaces
- [ ] apps/docs page renders cleanly + cites ADR 0028 + cohort lesson on pre-merge council
- [ ] All N tests passing (~30–40 across phases)

---

## Halt-conditions (cob-question if any of these surface)

1. **Cross-package wiring discovery.** If Phase 2's `DefaultCompatibilityRelation` requires types from other packages not yet on origin/main (specifically, `BusinessCaseBundleManifest.requiredModules: string[]` from ADR 0007 — needed only if the substrate evaluates rule 3 against bundle manifests rather than against the wire-format `PluginVersionVectorEntry.Required` flag): file a `cob-question-*` beacon. The substrate Phase 1 should NOT consult bundle manifests directly; the wire-format's `PluginVersionVectorEntry.Required` flag is canonical per A7.3.2. If the implementation seems to need bundle-manifest data, halt and ask.

2. **`InstanceClassKind` enum forward-compat.** Per A7.6 the enum reduces to `{ SelfHost, ManagedBridge }`. The verification test (encode `VersionVector` with hypothetical-future enum value via `CanonicalJson.Serialize`; deserialize on a default `JsonStringEnumConverter` consumer; observe behavior) needs to ship in Phase 1 tests. If the test reveals enum-value forward-compat is NOT tolerant in v0 CanonicalJson (older deserializers reject unknown enum values), file a `cob-question-*` beacon — A7.6 reverses pending verification.

3. **A1.x companion amendment surface.** ADR 0028's iOS A1 envelope augmentation (per A6.11 + A7.5) is a separate intake at PR #397; A1.x has NOT yet been authored as an ADR amendment. The Foundation.Versioning substrate Phase 1 does NOT need to handle A1's iOS envelope shape (that's W#23 territory + A1.x amendment territory). If iOS-envelope-shape work surfaces in this hand-off, halt — that's out of scope.

4. **`PluginId` / `AdapterId` namespace collision.** If `Sunfish.Foundation.PluginId` or `Sunfish.Foundation.AdapterId` exist elsewhere in `packages/`, file a `cob-question-*` beacon. The hand-off's audit-before-build step asks COB to verify; if collision found, scope to a different namespace (e.g., `Sunfish.Foundation.Versioning.PluginId`).

5. **Audit dedup cache behavior.** Per A7.4 the dedup is in-memory + per-process. If the dedup cache becomes a contention chokepoint (unlikely for v0), the implementation strategy may need a redesign — file a `cob-question-*` beacon.

6. **A7.5 iOS Phase A6.11 envelope semantics.** Foundation.Versioning Phase 1 does NOT implement A6.11's per-event capture-context tagging on the iOS envelope. That's W#23 + A1.x territory. If implementation seems to require capture-context fields on the wire format, halt — that's out of scope.

7. **A7.2 OQ-A6.4 explicit-set migration.** The arithmetic-window kernel-compat model (A6.2 rule 2) ships as v0; the libp2p-style explicit-version-set model is a future migration target per OQ-A6.4. Phase 1 ships the arithmetic model; do NOT attempt to ship the explicit-set model in Phase 1.

---

## Cited-symbol verification (per ADR 0028-A4.3 + A7.12 + ADR 0063-A1.15 cohort lesson)

**Existing on origin/main (verified before hand-off authored):**

- `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` (foundation-tier encoding contract) ✓
- `Sunfish.Kernel.Audit.AuditEventType` + `AuditPayload` + `IAuditTrail` (audit substrate per ADR 0049) ✓
- `Sunfish.Foundation.Crypto.IOperationSigner` (per ADR 0028-A2/A4) ✓
- ADR 0028 itself (CRDT engine selection) — Accepted; A1+A2+A3+A4+A5+A6+A7+A8 all on origin/main as of 2026-04-30
- ADR 0049 (audit substrate) — Accepted

**Introduced by this hand-off** (ship in Phase 1):

- `Sunfish.Foundation.Versioning.VersionVector`
- `Sunfish.Foundation.Versioning.PluginVersionVectorEntry`
- `Sunfish.Foundation.Versioning.VersionVectorVerdict`
- `Sunfish.Foundation.Versioning.PluginId` + `AdapterId`
- `Sunfish.Foundation.Versioning.ChannelKind` + `InstanceClassKind` + `VerdictKind`
- `Sunfish.Foundation.Versioning.FailedRule` (6-value enum)
- `Sunfish.Foundation.Versioning.IVersionVectorExchange` + `InMemoryVersionVectorExchange`
- `Sunfish.Foundation.Versioning.IVersionVectorIncompatibility` + `InMemoryVersionVectorIncompatibility`
- `Sunfish.Foundation.Versioning.ICompatibilityRelation` + `DefaultCompatibilityRelation`
- `Sunfish.Foundation.Versioning.Audit.VersionVectorAuditPayloads` factory class
- 2 new `AuditEventType` constants: `VersionVectorIncompatibilityRejected`, `LegacyDeviceReconnected`

**Cohort lesson reminder (per ADR 0063 council post-A1.15):** §A0 self-audit pattern is necessary but NOT sufficient. The 4 structural-citation failures in ADR 0063 all passed §A0 audit but failed council verification. This hand-off's Phase 1 cited-symbol claims are XO-asserted; COB should verify each Sunfish.* symbol exists structurally (read the actual cited file's schema; don't grep alone) before declaring AP-21 clean per Decision Discipline Rule 6.

---

## Cohort discipline

Per `feedback_decision_discipline.md` cohort batting average (15-of-15 substrate amendments needing council fixes — counting ADR 0064 in flight; structural-citation failure rate 10-of-14 (~71%) XO-authored; §A0 catch rate 0-of-4 on ADR 0063):

- This hand-off is **not** a substrate ADR amendment; it's a Stage 06 hand-off implementing post-A7-fixed surface. The cohort discipline applies to ADR amendments, not to this hand-off.
- Pre-merge council on this hand-off is NOT required (council reviews ADR-tier substrate decisions; this is implementation-tier).
- COB's standard pre-build checklist applies: verify ledger row says `ready-to-build` (this row will after hand-off lands); verify hand-off file describes what to build file-by-file (it does); verify no in-flight PRs overlap; verify but status + git log -all show no parallel-session work.

---

## Beacon protocol

If COB hits a halt-condition (per the 7 named above) or has a design question:

- File `cob-question-2026-05-XXTHH-MMZ-w34-{slug}.md` in `icm/_state/research-inbox/`
- Frontmatter: 3-line YAML (`type: cob-question`, `workstream-or-chapter: W#34`, `last-pr: <PR# or "none">`)
- Body: ≤2 lines context + ≤2 lines "what would unblock me"
- Halt the workstream + add a note in active-workstreams.md row 34 ("paused on cob-question-XXX")
- ScheduleWakeup 1800s

If COB completes Phase 1 + drops to rung-1 / rung-2 fallback:

- Drop `cob-idle-2026-05-XXTHH-MMZ-{slug}.md` to research-inbox
- Continue with rung-1 dependabot + rung-2 build-hygiene per CLAUDE.md fallback work order

---

## Cross-references

- Spec source: ADR 0028-A6+A7 (post-A7 surface; ADR 0028's "Amendments" section A6.* + A7.*)
- Council that drove A7: PR #396 (merged 2026-04-30); council-review file at `icm/07_review/output/adr-audits/0028-A6-council-review-2026-04-30.md`
- Sibling hand-off (forthcoming): A5+A8 Foundation.Migration substrate Phase 1 — separate W# when authored
- Companion intake (deferred): A1.x iOS envelope capture-context tagging (PR #397; coordinated A1 amendment to ADR 0028 not yet authored)
- W#33 Mission Space Matrix follow-on queue: `project_workstream_33_followon_authoring_queue.md` (memory)
