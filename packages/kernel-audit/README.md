# Sunfish.Kernel.Audit

Append-only domain-typed audit trail — kernel subsystem for security- and compliance-relevant events. Ships per [ADR 0049](../../docs/adrs/0049-audit-trail-substrate.md).

## What this package owns

Audit records of multi-party-attested events: recovery initiations, trustee attestations, capability delegations, payment authorizations, IRS exports — anything where "who did what, when, signed by whom" needs to be queryable for compliance, security review, or regulatory disclosure.

This package owns:

- `IAuditTrail` — the append + query contract.
- `IAuditEventStream` — in-process typed stream for projections, retention reporters, audit-log UIs.
- `AuditRecord` — the tenant-scoped, signed record type. Format `v0` (see [Trust impact](#trust-impact)).
- `AuditEventType` — string-based discriminators, extensible per block.
- `EventLogBackedAuditTrail` — default impl, persists to the kernel `IEventLog`.

This package does **not** own:

- Durability — that's `Kernel.EventBus`'s `IEventLog`. `Kernel.Audit` layers over it.
- Sync transport — audit records propagate via the same `IEventLog`-fed sync substrate as application data, per ADR 0046's *"same sync protocol as business data"* requirement.
- Compliance query / retention logic — `IComplianceQuery` is deferred per ADR 0049 §"Open questions". When IRS export (Phase 2) or another real compliance use case lands, that contract joins this package.

## Architectural pattern

`Kernel.Audit` is **structurally parallel to `Kernel.Ledger`** — same package shape, same `IEventLog` integration. Anyone who already understands the ledger subsystem's relationship to the kernel event log understands this one.

| Layer | Ledger | Audit |
|---|---|---|
| Domain contract | `IPostingEngine` | `IAuditTrail` |
| Typed event stream | `ILedgerEventStream` | `IAuditEventStream` |
| Domain record | `Transaction` (with `Posting`s) | `AuditRecord` |
| Durability hook | `IEventLog` | `IEventLog` |

The `Kernel.Ledger` README (`packages/kernel-ledger/README.md`) is the direct precedent — read it first if the layering is unfamiliar.

## Paper / ADR cross-reference

- [ADR 0049](../../docs/adrs/0049-audit-trail-substrate.md) — establishes this package as a distinct kernel-tier subsystem rather than a subsystem of `Kernel.Ledger` or `Kernel.EventBus`. Names the architectural parallel to `Kernel.Ledger`.
- [ADR 0046](../../docs/adrs/0046-key-loss-recovery-scheme-phase-1.md) §"sub-pattern #48f" — the first consumer. The Phase 1 G6 host integration task ("persist `RecoveryEvent`s to per-tenant audit log") consumes this package's `IAuditTrail`.
- [ADR 0028](../../docs/adrs/0028-crdt-engine-selection.md) — the substrate-impl insulation precedent ADR 0049 cites. `IAuditTrail` over `IEventLog` is the kernel-tier instance of the same pattern.
- [ADR 0004](../../docs/adrs/0004-post-quantum-signature-migration.md) — the algorithm-agility refactor that gates audit-format `v1`. Until then, audit records are `v0`. See [Trust impact](#trust-impact).

## Trust impact

`AuditRecord.AttestingSignatures` uses `Sunfish.Foundation.Crypto.Signature`, currently algorithm-locked to Ed25519 (64-byte fixed) per ADR 0004. Audit records are exactly the long-retention data class that needs algorithm-agility before format commitment — a 7-year-retained IRS audit record or recovery attestation written today against fixed Ed25519 will need migration when post-quantum signatures ship per ADR 0004's dual-sign window.

The persisted format is therefore marked **`v0`**. `AuditRecord.FormatVersion` is set to `0`, and the `EventLogBackedAuditTrail` rejects any record with a different `FormatVersion`. When ADR 0004's signature refactor lands, `v1` will introduce an algorithm-tagged signature envelope and a migration path between the two formats.

Until that migration is in place, **callers SHOULD treat audit records as data they may need to re-sign before v1 release**. This is a tradeoff documented in ADR 0049 §"Trust impact" — the alternative (holding scaffolding until ADR 0004's refactor lands) would block the Phase 1 G6 host integration that this package exists to unblock.

## Multi-party signature verification

The kernel `IAuditTrail.AppendAsync` only verifies the payload's own `SignedOperation` envelope. The `AttestingSignatures` list is carried through but **not algorithmically verified at the kernel boundary in v0** — the contract does not bind those signatures to specific principals or to a canonical bytes form. Verification of multi-party attestation is the caller's responsibility:

- The `RecoveryCoordinator` in `Sunfish.Kernel.Security.Recovery` already verifies trustee attestations via `TrusteeAttestation.Verify` before constructing a `KeyRecoveryCompleted` audit record. Records appended via that path are pre-validated.
- Future blocks that produce multi-attested audit records SHOULD do the same — verify before append.

ADR 0049 §"Open questions" tracks promotion of this to a kernel-tier check once the contract names principals + canonical-bytes form.

## Usage

```csharp
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Audit;
using Sunfish.Kernel.Audit.DependencyInjection;
using Sunfish.Kernel.Events.DependencyInjection;

var services = new ServiceCollection()
    .AddSunfishEventLog()                  // Kernel.EventBus
    .AddSingleton<IOperationSigner, Ed25519Signer>()
    .AddSingleton<IOperationVerifier, Ed25519Verifier>()
    .AddSunfishKernelAudit()               // this package
    .BuildServiceProvider();

var trail = services.GetRequiredService<IAuditTrail>();
var signer = services.GetRequiredService<IOperationSigner>();

// Sign a payload and append an audit record.
var payload = new SignedOperation<AuditPayload>(
    Payload: new AuditPayload(new Dictionary<string, object?>
    {
        ["recoveryId"] = recoveryId,
        ["newOwnerKey"] = newOwnerKeyBase64,
    }),
    IssuerId: actingPrincipal,
    IssuedAt: DateTimeOffset.UtcNow,
    Nonce: Guid.NewGuid(),
    Signature: signature);

await trail.AppendAsync(new AuditRecord(
    AuditId: Guid.NewGuid(),
    TenantId: tenantId,
    EventType: AuditEventType.KeyRecoveryCompleted,
    OccurredAt: DateTimeOffset.UtcNow,
    Payload: payload,
    AttestingSignatures: trusteeQuorumSignatures));

// Query by time range + event type.
await foreach (var rec in trail.QueryAsync(new AuditQuery(
    TenantId: tenantId,
    EventType: AuditEventType.KeyRecoveryCompleted,
    OccurredAfter: DateTimeOffset.UtcNow.AddDays(-90))))
{
    Console.WriteLine($"{rec.OccurredAt:o} {rec.EventType} {rec.AuditId}");
}
```

## Storage

`EventLogBackedAuditTrail` persists each record to the kernel `IEventLog` as a `KernelEvent` with `Kind = "audit." + EventType`. The `KernelEvent.Payload` carries the typed `AuditRecord` reference; the `EntityId` encodes the tenant for per-tenant log filtering.

This is intentionally simple — a future revision may add a paged direct-from-`IEventLog` read for tenants with very large audit histories. For Phase 1 G6 (recovery audit) and Phase 2 commercial-scope sizing, in-process replay is sufficient.

## What you get from this contract

- **Append-only by definition.** No update, no delete. GDPR Article 17 erasure is the future `IComplianceQuery` surface's responsibility.
- **Tenant-scoped reads.** `AuditQuery.TenantId` is mandatory. Cross-tenant audit is not in v0 (ADR 0049 §"Open questions" tracks whether `IMayHaveTenant` ever applies here).
- **Substrate-impl insulation.** Storage can swap to a compliance-specific WORM store without rippling into application code — same pattern as `ICrdtDocument` over Loro/YDotNet (ADR 0028) and `IPostingEngine` over `IEventLog`.
