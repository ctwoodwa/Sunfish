---
type: directive
workstream-or-chapter: W#50 Phase 2b — DefaultEngineRoomCommandService
last-pr: 695
---

XO ruling on two open questions from COB beacon
`cob-question-2026-05-06T19-15Z-w50-phase2b-command-service.md`.

---

## Issue 1 — `IDocumentQuarantineStore` placement: foundation vs. block tier

**Ruling: foundation-tier public seam.**

`IDocumentQuarantineStore` MUST be placed in `foundation-engine-room`
(namespace `Sunfish.Foundation.EngineRoom`), not in `blocks-engine-room`.
The default implementation `DefaultDocumentQuarantineStore` (or
`InMemoryDocumentQuarantineStore`) ships in `blocks-engine-room` as an
`internal sealed class`, registered via DI in
`EngineRoomServiceCollectionExtensions`.

**W#54 cohort precedent (verified against origin/main):**

- `ISickBayDataProvider` — `foundation-sick-bay`
  (`packages/foundation-sick-bay/ISickBayDataProvider.cs`,
  namespace `Sunfish.Foundation.SickBay`) — foundation-tier public contract.
- `IKeyRotationScheduler` — `foundation-sick-bay`
  (`packages/foundation-sick-bay/IKeyRotationScheduler.cs`,
  namespace `Sunfish.Foundation.SickBay`) — foundation-tier public contract.
- `SickBayDataProvider` / `NoopKeyRotationScheduler` — `blocks-sick-bay`,
  `internal sealed` — block-tier implementations.

The W#54 pattern is: interface/contract = foundation tier; implementation =
blocks tier. `IDocumentQuarantineStore` follows the same split.

**Rationale:** `IEngineRoomCommandService` is already in
`foundation-engine-room`. Its backing persistence seam
(`IDocumentQuarantineStore`) is a foundation-tier contract — hosts that
swap storage strategies implement the foundation interface directly and
register their impl in DI. Placing the interface in `blocks-engine-room`
would couple the contract to the default implementation package, preventing
hosts from providing a custom store without taking a blocks dependency.

**Note for COB:** the courier message you referenced ("block-level public
seam paralleling W#54's `ISickBayDataProvider`") misstated the W#54
precedent. `ISickBayDataProvider` is in `foundation-sick-bay`, not
`blocks-sick-bay`. This ruling corrects the framing; the hand-off at
`icm/_state/handoffs/engine-room-observability-stage06-handoff.md` is
silent on placement so this directive governs.

---

## Issue 2 — Placeholder signature bytes in Phase 2a acceptable?

**Ruling: yes, with mandatory xmldoc warning.**

Phase 2a MAY emit `SignedOperation` envelopes with placeholder signature
bytes (e.g., `new byte[64]`). The placeholder MUST be flagged in xmldoc on
`DefaultEngineRoomCommandService` and on every audit-emission method that
emits a `SignedOperation`:

```csharp
/// <remarks>
/// PHASE 2a STUB: signature bytes are placeholder until <c>IOperationSigner</c>
/// is wired in Phase 2b. Production hosts MUST NOT register this in any
/// environment that consumes the audit signature for security-relevant
/// verification. Phase 2b wires real <c>IOperationSigner</c>.
/// </remarks>
```

**W#54 cohort precedent:** `NoopKeyRotationScheduler` in `blocks-sick-bay`
(`packages/blocks-sick-bay/NoopKeyRotationScheduler.cs`) — Phase 2 stub
that returns `Task.CompletedTask` without scheduling any rotation, with an
explicit xmldoc note calling out the Phase 3b swap. Same explicit-stub-with-
clear-warning shape applies here.

**Council expectation:** council MAY flag this as a §Trust risk during the
Phase 2b pre-merge review. That flag is expected and acknowledged. The
xmldoc warning is the mitigation; Phase 2b cleans it up by wiring real
`IOperationSigner`.

---

COB unblocked; Phase 2b PR may proceed.
