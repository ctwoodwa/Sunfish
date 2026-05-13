# Engine Room — Foundation Contracts + Block Implementations

`Sunfish.Foundation.EngineRoom` + `Sunfish.Blocks.EngineRoom` deliver the
Engine Room observability surface per
[ADR 0079](../../../docs/adrs/0079-engine-room-observability.md).

## Foundation contracts (`Sunfish.Foundation.EngineRoom`)

### Data provider

```csharp
// Subscribe to health updates — emits immediately on subscribe, then on
// status change, then every HeartbeatInterval (default 30 s).
await foreach (var summary in dataProvider.SubscribeHealthAsync(tenantId, ct))
{
    var erHealth = summary.For(EngineRoomSubsystem.DamageControl);
}

// One-shot snapshot
var summary = await dataProvider.GetHealthSummaryAsync(tenantId);

// CRDT growth metrics — stream all documents, or filter by eligibility
await foreach (var m in dataProvider.GetCrdtGrowthMetricsAsync(tenantId, ct))
{
    if (m.CompactionEligible) { /* surface in Damage Control triage list */ }
}
```

### Authorization pre-flight (§4)

All Damage Control commands follow the §Trust ordering invariant:

```
1. Pre-op audit event (*Requested)
2. Actor → Principal resolution (IActorPrincipalResolver)
3. IPermissionResolver.ResolveAsync(ShipAction.QuarantineDocument, …)
4. Execute
5. Post-op audit event (*Completed / *Released)
```

`DefaultEngineRoomCommandService` enforces this sequence; callers MUST NOT
short-circuit or reorder.

### EOOW watch-pin semantics

For `QuarantineDocument` and `ReleaseQuarantine`, the command service queries
`IOodWatchService.GetActiveWatchAsync(tenantId, OodRole.EngineeringOfficerOfTheWatch)`
and embeds the active watch ID in the pre-op audit payload. A null watch ID
(no EOOW on watch) is logged at Warning and does NOT block the command —
Damage Control operations remain callable in emergency manual-override posture.

### OTel metric catalog

| Instrument | Type | Description |
|---|---|---|
| `sunfish.engine_room.peer_count` | Gauge | Connected sync daemon peers |
| `sunfish.engine_room.events_throughput` | Gauge | Gossip events / second |
| `sunfish.engine_room.gossip_cycles` | Counter | Total gossip cycles |
| `sunfish.engine_room.crdt_total_bytes` | Gauge | Estimated CRDT on-disk size |
| `sunfish.engine_room.crdt_compaction_eligible` | Gauge | Documents eligible for compaction |
| `sunfish.engine_room.subsystem_status` | Gauge | Per-subsystem status (0=Unknown 1=Operational 2=Warning 3=Critical) |

Meter name: `Sunfish.EngineRoom`. Activity source: `Sunfish.EngineRoom`.

### Data model

| Type | Description |
|---|---|
| `EngineRoomHealthSummary` | Aggregated subsystem health list + `For(EngineRoomSubsystem)` helper |
| `SubsystemHealth` | `Subsystem` + `Status` + optional `Message` |
| `SubsystemStatus` | `Operational`, `Warning`, `Critical`, `Unknown` |
| `EngineRoomSubsystem` | `MainPropulsion`, `Electrical`, `DamageControl`, `QaWorkshop` |
| `SyncDaemonHealth` | Daemon snapshot: `Status`, `PeerCount`, `EventsThroughput`, `GossipCycles`, `AsOf` |
| `CrdtGrowthMetrics` | Per-document: `DocumentId`, `TotalByteEstimate`, `TombstoneCount`, `CompactionEligible`, `MeasuredAt` |
| `QuarantineResult` | `DocumentId` + `QuarantinedAt` |
| `ReleaseResult` | `DocumentId` + `ReleasedAt` |
| `CompactionResult` | `DocumentId`, `BytesBefore`, `BytesAfter`, `CompletedAt` |
| `EngineRoomOptions` | `HeartbeatInterval` (default 30 s), `DegradationDedupCooldown` (default 30 s) |

## DI registration

```csharp
// Foundation tier (host)
services.AddSunfishEngineRoom();

// Block tier (host) — wires DefaultEngineRoomDataProvider +
// DefaultEngineRoomCommandService. Hosts MUST also register
// IDocumentQuarantineStore, IPermissionResolver, IActorPrincipalResolver,
// and IOodWatchService before resolving IEngineRoomCommandService.
services.AddSunfishEngineRoomDefaults();

// Register the host's quarantine store implementation
services.AddEngineRoomQuarantineStore<MyEfCoreQuarantineStore>();
```

## WCAG live-region + alertdialog patterns

### Live regions (pre-mounted per ARIA22)

Engine Room panels pre-mount both an assertive region (status degradation) and
a polite region (heartbeat updates + loading state) in the DOM **before** any
content is injected. Never conditionally render live regions — mount them empty
at component init; write to them via state fields.

```razor
<div role="alert" aria-live="assertive" aria-atomic="true" class="sf-sr-only">
    @_assertiveAnnouncement
</div>
<div role="status" aria-live="polite" aria-atomic="true" class="sf-sr-only">
    @_politeAnnouncement
</div>
```

### Damage Control alertdialog + deliberation-pause

`DamageControlPanel` uses `role="alertdialog"` for destructive confirmations:

```
1. Dialog opens → Cancel receives focus (NOT Confirm)
2. Confirm is disabled (aria-disabled is NOT set — native disabled handles AT)
3. After 2000 ms deliberation pause → Confirm enables
4. Inner polite live region (inside alertdialog subtree) announces
   "Confirm available." — outer region is suppressed by aria-modal
5. Esc or Cancel → dialog closes → focus returns to trigger element
```

The inner live region MUST be inside the `role="alertdialog"` subtree so
`aria-modal` does not suppress the "Confirm available." announcement for
AT users navigating inside the dialog.
