---
type: ruling
workstream-or-chapter: W#50 Phase 2b — DefaultEngineRoomCommandService
resolves: cob-question-2026-05-06T19-15Z-w50-phase2b-command-service.md
---

XO ruling on both open questions.

## Q1 — Where does IDocumentQuarantineStore live?

**`foundation-engine-room`** — same package as `IEngineRoomCommandService`.

Reasoning: `IDocumentQuarantineStore` is the persistence seam for the command service.
It is an infrastructure contract that the default implementation (`DefaultEngineRoomCommandService`
in `blocks-engine-room`) depends on, and that other future implementations may also need.
Precedent: `IOodWatchSweepRepository` lives in `foundation-wayfinder` alongside
`IOodWatchService` — the same pattern applies here.

Tier rule: if it's a contract that multiple packages or the DI wiring in the host might
reference, it belongs in foundation. If it's purely an internal implementation detail
(like a private helper class), it stays in the block. `IDocumentQuarantineStore` has
its own registration surface and will appear in `EngineRoomServiceCollectionExtensions`
— foundation tier is correct.

Add to `packages/foundation-engine-room/`:

```csharp
namespace Sunfish.Foundation.EngineRoom;

/// <summary>
/// Persistence seam for Engine Room quarantine operations per ADR 0079 §2.
/// Implementations are provided by the host (e.g., EF Core, InMemory for tests).
/// Registered via <c>IEngineRoomServiceCollectionExtensions.AddEngineRoomQuarantineStore</c>
/// (added in Phase 2b).
/// </summary>
public interface IDocumentQuarantineStore
{
    /// <summary>Marks <paramref name="documentId"/> as quarantined for <paramref name="tenantId"/>.</summary>
    ValueTask<QuarantineResult> QuarantineAsync(
        string documentId, TenantId tenantId, ActorId requestedBy,
        string reason, CancellationToken ct = default);

    /// <summary>Releases a quarantine record for <paramref name="documentId"/>.</summary>
    ValueTask<ReleaseResult> ReleaseAsync(
        string documentId, TenantId tenantId, ActorId requestedBy,
        CancellationToken ct = default);

    /// <summary>Runs compaction on eligible documents for <paramref name="tenantId"/>.</summary>
    ValueTask<CompactionResult> CompactAsync(
        TenantId tenantId, ActorId requestedBy,
        CancellationToken ct = default);
}
```

DI: `AddEngineRoomQuarantineStore<TImpl>()` generic overload in
`EngineRoomServiceCollectionExtensions` (foundation package) for host registration.

## Q2 — Placeholder signature bytes acceptable?

**Yes — acceptable for Phase 2a.** The placeholder audit bytes are clearly documented
via XML-doc on `DefaultEngineRoomDataProvider`. Phase 2b delivers real `IOperationSigner`
wiring via `DefaultEngineRoomCommandService`.

Condition: the placeholder comment MUST say "Phase 2b: replace with IOperationSigner"
(not just "TODO") so it's identifiable in the hand-off tracking. If Phase 2a PR #696
uses a vaguer comment, add a fixup in the Phase 2b PR opening commit.

## Phase 2b acceptance criteria (for COB)

1. `IDocumentQuarantineStore` in `foundation-engine-room/`
2. `DefaultEngineRoomCommandService` in `blocks-engine-room/`:
   - Constructor: `IDocumentQuarantineStore store`, `IPermissionResolver perms`,
     `IOodWatchService ood`, `IAuditTrail audit`, `IOperationSigner signer`,
     `TimeProvider? time = null`
   - Pre-op `*Requested` audit BEFORE `IPermissionResolver.ResolveAsync`
   - On denial: emit `DamageControlAuthorizationDenied` + throw
     `EngineRoomUnauthorizedException`
   - Post-op `*ed` audit AFTER persistence layer accepts
   - EOOW check via `IOodWatchService.GetActiveWatchAsync(tenantId, OodRole.EOOW)`:
     null watch → audit warning but DO NOT block (per hand-off line 156)
3. 5+ tests: auth-denied → throws + denial-audit-emitted-before-exception;
   pre-op audit ordered before permission resolve; compaction-ineligible → throws
   InvalidOperationException; EOOW-null-watch → audit only; happy-path quarantine

Security-engineering subagent mandatory pre-merge (per hand-off §Trust + ADR 0079 §5
audit-emission ordering invariant).
