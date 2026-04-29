# Workstream #27 — Leases EXTEND — Stage 06 hand-off

**Workstream:** #27 (Leases — EXTEND `blocks-leases`)
**Spec:** [ADR 0054](../../docs/adrs/0054-electronic-signature-capture-and-document-binding.md) (Accepted) + [`property-leases-intake-2026-04-28.md`](../../icm/00_intake/output/property-leases-intake-2026-04-28.md) + cluster reconciliation review §27 (NEW → EXTEND)
**Pipeline variant:** `sunfish-feature-change` (extends `blocks-leases`; existing block self-describes as "thin first pass; full workflow surface deferred")
**Estimated effort:** 10–14 hours focused sunfish-PM time
**Decomposition:** 7 phases shipping as ~5 PRs
**Prerequisites:** W#21 Signatures (in flight; Phase 4 ships `ISignatureCapture`); W#19 Phase 0 stubs (Money + ThreadId — already shipped per #281)

---

## Scope summary

Existing `packages/blocks-leases/` ships ~80% of cluster scope (`Lease` + `LeasePhase` enum + `Document` + `Party` + `PartyKind` + `Unit`). The chapter docstring on `Lease.cs` self-describes the gap:

> Intentionally thin for the first pass; full workflow surface (signature, execution, renewal, termination) is deferred to follow-up work.

This hand-off ships that follow-up. Cluster contribution:

1. **`LeaseDocumentVersion` versioning** — append-only log of lease document revisions; each revision has a content-hash per ADR 0054
2. **`Lease.SignatureEventRef`** + per-lease signature collection (one signature per party + a primary attestation by the landlord)
3. **Renewal/termination state-machine transitions** (extends existing `LeasePhase` enum + adds `TransitionTable<LeasePhase>` per W#19's pattern)
4. **`LeaseHolderRole`** value enum leveraging existing `PartyKind.Tenant` per UPF Rule 5 (don't duplicate; use existing `Party` + `PartyKind`)
5. **Audit emission** — 8 new `AuditEventType` constants per ADR 0049

**NOT in scope:** lease-template authoring (admin-defined template substrate; defer to ADR 0055 dynamic-forms when shipped); lease-document storage (Document blob is opaque; ADR 0028 already covers); rent-collection automation (lives in `blocks-rent-collection`); lease-renewal notification UX (Owner Cockpit W#29).

---

## Phases

### Phase 1 — `LeasePhase` transition table + state-machine guards (~1.5h)

Existing `LeasePhase` enum has: `Draft, AwaitingSignature, Executed, Active, Renewed, Terminated` (with `Cancelled` likely). Add `TransitionTable<LeasePhase>` mirroring W#19's pattern (now public per ADR 0053 A5):

```text
Draft → AwaitingSignature | Cancelled
AwaitingSignature → Executed | Cancelled | Draft (revisions)
Executed → Active (on commencement date)
Active → Renewed | Terminated
Renewed → Active (re-enters Active state with new term)
Terminated (terminal)
Cancelled (terminal)
```

Add `ILeasesService.TransitionPhaseAsync(LeaseId, LeasePhase newPhase, ActorId actor, CancellationToken)` enforcing transitions via `TransitionTable`.

Per ADR 0053 A5, `TransitionTable<TState>` is now `public sealed` in `blocks-maintenance`. Reference it directly from `blocks-leases`.

**Gate:** transition table refuses invalid transitions; 8 unit tests covering each arrow + each rejection.

**PR title:** `feat(blocks-leases): LeasePhase transition table + state-machine guards`

### Phase 2 — `LeaseDocumentVersion` versioning (~2–3h)

Append-only document-revision log per ADR 0054 content-hash binding:

```csharp
public sealed record LeaseDocumentVersion
{
    public required LeaseDocumentVersionId Id { get; init; }
    public required LeaseId Lease { get; init; }
    public required int VersionNumber { get; init; }                   // monotonically increasing per Lease
    public required ContentHash DocumentHash { get; init; }            // ADR 0054 SHA-256 over canonical bytes
    public required string DocumentBlobRef { get; init; }              // tenant-key encrypted blob storage
    public required ActorId AuthoredBy { get; init; }
    public required DateTimeOffset AuthoredAt { get; init; }
    public required string ChangeSummary { get; init; }                // free-text revision note
}

public readonly record struct LeaseDocumentVersionId(Guid Value);
```

Add `ILeaseDocumentVersionLog` interface + `InMemoryLeaseDocumentVersionLog` implementation. Append-only — no editing existing versions; new version = new entry.

Update `Lease` record to include `IReadOnlyList<LeaseDocumentVersionId> DocumentVersions { get; init; }` field.

**Gate:** version-append works; cannot edit existing version; ContentHash is computed deterministically.

**PR title:** `feat(blocks-leases): LeaseDocumentVersion append-only versioning (ADR 0054)`

### Phase 3 — Signature collection per lease (~2–3h)

Each party signs the lease; landlord signs an attestation. Add to `Lease`:

```csharp
public sealed record Lease
{
    // ... existing fields ...
    public IReadOnlyList<LeasePartySignature> PartySignatures { get; init; } = [];
    public SignatureEventId? LandlordAttestation { get; init; }       // ADR 0054
}

public sealed record LeasePartySignature
{
    public required LeasePartySignatureId Id { get; init; }
    public required LeaseId Lease { get; init; }
    public required PartyId Party { get; init; }
    public required SignatureEventId SignatureEvent { get; init; }    // ADR 0054 signature
    public required LeaseDocumentVersionId DocumentVersion { get; init; } // which version they signed
    public required DateTimeOffset SignedAt { get; init; }
}
```

`Lease.AwaitingSignature → Executed` transition guard: ALL parties must have a `LeasePartySignature` entry pointing at the latest `LeaseDocumentVersion` AND `LandlordAttestation` must be set.

Add `ILeasesService.RecordPartySignatureAsync(LeaseId, PartyId, SignatureEventId, CancellationToken)` and `ILeasesService.SetLandlordAttestationAsync(LeaseId, SignatureEventId, CancellationToken)`.

**Gate:** signature collection enforces all-parties-signed before allowing transition to Executed; landlord attestation distinct from party signatures.

**PR title:** `feat(blocks-leases): per-party signatures + landlord attestation (ADR 0054)`

### Phase 4 — `LeaseHolderRole` (UPF Rule 5: leverage existing) (~0.5h)

UPF Rule 5: don't duplicate existing concepts. `Party.Kind = PartyKind.Tenant` already covers "this party is a tenant"; `LeaseHolderRole` adds RBAC-style distinction within the tenant set:

```csharp
public enum LeaseHolderRole
{
    PrimaryLeaseholder,    // typically receives rent reminders, manages account
    CoLeaseholder,         // shares responsibility; receives copies of all communications
    Occupant,              // listed on lease but not a financial party (e.g., minor child)
    Guarantor,             // financial backstop; not occupying
}
```

`LeasePartySignature` only required for `PrimaryLeaseholder` + `CoLeaseholder`; `Occupant` doesn't sign; `Guarantor` signs a separate attestation document (deferred to follow-up hand-off).

Add `LeasePartyRole` join entity:

```csharp
public sealed record LeasePartyRole
{
    public required LeasePartyRoleId Id { get; init; }
    public required LeaseId Lease { get; init; }
    public required PartyId Party { get; init; }
    public required LeaseHolderRole Role { get; init; }
}
```

Update `Lease` to include `IReadOnlyList<LeasePartyRoleId> PartyRoles`.

**Gate:** role enum + join entity ship; PrimaryLeaseholder + CoLeaseholder enforced as signing parties.

**PR title:** `feat(blocks-leases): LeaseHolderRole + LeasePartyRole join (UPF Rule 5)`

### Phase 5 — Audit emission (~1–2h)

Add 8 new `AuditEventType` constants under `===== ADR 0028 / ADR 0054 — Leases =====` divider:

```csharp
public static readonly AuditEventType LeaseDrafted = new("LeaseDrafted");
public static readonly AuditEventType LeaseDocumentVersionAppended = new("LeaseDocumentVersionAppended");
public static readonly AuditEventType LeasePartySignatureRecorded = new("LeasePartySignatureRecorded");
public static readonly AuditEventType LeaseLandlordAttestationSet = new("LeaseLandlordAttestationSet");
public static readonly AuditEventType LeaseExecuted = new("LeaseExecuted");        // all signatures captured
public static readonly AuditEventType LeaseActivated = new("LeaseActivated");      // commencement date reached
public static readonly AuditEventType LeaseRenewed = new("LeaseRenewed");
public static readonly AuditEventType LeaseTerminated = new("LeaseTerminated");
```

`LeaseAuditPayloadFactory` per established pattern. Wire `InMemoryLeasesService` to call `IAuditTrail.AppendAsync(...)` after each lifecycle event.

**Gate:** 8 event types ship; factory works; audit trail covers all phase transitions + signature events + version appends.

**PR title:** `feat(blocks-leases): audit emission — 8 AuditEventType + factory (ADR 0049)`

### Phase 6 — Cross-package wiring + apps/docs (~1.5h)

Wiring points:
- **`kernel-signatures.ISignatureCapture`** (W#21 Phase 1+) — for both `LeasePartySignature` and `LandlordAttestation`. If W#21 not yet shipped at Phase 6 time, halt-condition.
- **`Foundation.Taxonomy.ITaxonomyResolver`** (W#31 ✓) — `Sunfish.Signature.Scopes/lease-execution` + `lease-renewal` already seeded
- **W#22 Leasing Pipeline** (`Application` → `LeaseOffer` → `Lease`): boundary contract — `LeaseOffer.AcceptAsync(...)` calls `ILeasesService.CreateFromOfferAsync(...)` to seed a `Lease.Draft`

apps/docs:
- `apps/docs/blocks/leases/overview.md` — full lifecycle (Draft → AwaitingSignature → Executed → Active → Renewed/Terminated)
- `apps/docs/blocks/leases/signature-flow.md` — multi-party signature collection + ADR 0054 integration
- `apps/docs/blocks/leases/document-versioning.md` — append-only revision log + content-hash binding

**Gate:** cross-package wiring works; apps/docs builds.

**PR title:** `feat(blocks-leases): cross-package wiring + apps/docs`

### Phase 7 — Ledger flip (~0.5h)

Update `icm/_state/active-workstreams.md` row #27 → `built`. Append last-updated entry.

**PR title:** `chore(icm): flip W#27 ledger row → built`

---

## Total decomposition

| Phase | Subject | Hours |
|---|---|---|
| 1 | LeasePhase transition table + guards | 1.5 |
| 2 | LeaseDocumentVersion versioning | 2–3 |
| 3 | Per-party signatures + landlord attestation | 2–3 |
| 4 | LeaseHolderRole + LeasePartyRole | 0.5 |
| 5 | Audit emission (8 AuditEventType) | 1–2 |
| 6 | Cross-package wiring + apps/docs | 1.5 |
| 7 | Ledger flip | 0.5 |
| **Total** | | **9–11h** |

---

## Halt conditions

- **W#21 Phase 1+ (`kernel-signatures.ISignatureCapture`) not yet shipped** at Phase 6 → write `cob-question-*`; XO may stub the interface ahead of full W#21 Stage 06
- **`ContentHash` from kernel-signatures** signature differs from W#21's design → halt; cross-package contract drift
- **Existing `LeasePhase` enum has different values** than the current spec assumes (e.g., missing `Renewed` or `Cancelled`) → audit + adjust transition table to match reality
- **`Sunfish.Signature.Scopes/lease-execution` taxonomy node missing** → halt; W#31 Phase X charter needs the node
- **`Lease.AwaitingSignature → Executed` transition guard** false-positive (signature recorded but version mismatch) — write `cob-question-*` if the guard logic surfaces edge cases unhandled by the spec

---

## Acceptance criteria

- [ ] `TransitionTable<LeasePhase>` + 8+ unit tests covering valid + invalid transitions
- [ ] `LeaseDocumentVersion` append-only log; cannot mutate existing versions
- [ ] `LeasePartySignature` + `Lease.LandlordAttestation` integrate `kernel-signatures.SignatureEventId`
- [ ] `LeaseHolderRole` + `LeasePartyRole` ship; `PrimaryLeaseholder` + `CoLeaseholder` enforced as signing parties
- [ ] 8 new `AuditEventType` constants in kernel-audit
- [ ] `LeaseAuditPayloadFactory` ships
- [ ] `Lease.AwaitingSignature → Executed` transition guard requires all signatures + landlord attestation
- [ ] `apps/docs/blocks/leases/` overview + signature-flow + document-versioning pages exist
- [ ] All tests pass; build clean; no breaking changes to existing `Lease` callers
- [ ] Ledger row #27 → `built`

---

## References

- [ADR 0054](../../docs/adrs/0054-electronic-signature-capture-and-document-binding.md) — `SignatureEvent` + `ContentHash` consumed
- [ADR 0028](../../docs/adrs/0028-per-record-class-consistency.md) — `Lease` is CP-class
- [ADR 0049](../../docs/adrs/0049-audit-trail-substrate.md) — audit emission
- [ADR 0056](../../docs/adrs/0056-foundation-taxonomy-substrate.md) — Sunfish.Signature.Scopes for signature scope
- [Cluster reconciliation review §27](../07_review/output/property-ops-cluster-vs-existing-reconciliation-2026-04-28.md) — disposition NEW → EXTEND
- [W#21 Signatures hand-off](./property-signatures-stage06-handoff.md) — kernel-signatures Phase 1
- [W#19 Work Orders hand-off](./property-work-orders-stage06-handoff.md) — TransitionTable<TState> public-promotion pattern reused
- [W#22 Leasing Pipeline hand-off](./property-leasing-pipeline-stage06-handoff.md) — `Application` → `LeaseOffer` → `Lease.Draft` boundary
