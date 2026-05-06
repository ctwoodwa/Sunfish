# W#23 — Equipment Photo Capture (W#23.2) — Stage 06 hand-off

**Context:** First capture-flow follow-on for the W#23 iOS Field-Capture App substrate
(W#23.2 from the main hand-off table). Adds a SwiftUI camera screen on the iOS side and
a per-event-type dispatch handler on the Bridge side so that `EventType.Asset` envelopes
update `Equipment.PrimaryPhotoBlobRef`.

**Workstream:** #23 · first capture-flow deliverable (W#23.2, Assets / Equipment)
**Spec sources:** ADR 0028 + A1–A3, main hand-off §Capture-flow follow-ups,
`packages/blocks-property-equipment/`
**Pipeline variant:** `sunfish-feature-change`
**Estimated effort:** ~6–8h sunfish-PM / 3 phases / 2 PRs
**PR titles:**
- PR 1: `feat(anchor-mobile-ios): W#23.2 P1 — AssetCaptureView + AssetCapturePayload`
- PR 2: `feat(bridge,blocks-property-equipment,kernel-audit): W#23.2 P2+P3 — Asset event handler + tests`

---

## Prerequisites

| Prerequisite | Status |
|---|---|
| W#23 P0–P4.5 shipped (EventType, EventEnvelope, BlobStore, SyncEngine, Bridge field endpoints) | ✓ shipped PRs #478/#498/#511/#516/#517/#533 |
| W#23 P5 pairing-flow hand-off | ✓ merged PR #620 |
| W#23 P6 home screen + queue-status UX (main hand-off §Phase 6) | COB must build from main hand-off before this hand-off is actionable |
| `Equipment.PrimaryPhotoBlobRef: string?` in `blocks-property-equipment` | ✓ exists |
| `IEquipmentRepository.GetByIdAsync` + `UpsertAsync` | ✓ exists |

**Sequencing note:** This hand-off is pre-authored now but is NOT actionable until W#23 P6
(home screen) ships — the home screen `HomeView.swift` is where the capture-flow entry
point buttons live. COB MUST build P6 first (from the main hand-off §Phase 6, no separate
addendum needed), then pick up this hand-off for P6-capture / W#23.2.

---

## §A0 Cited-symbol audit (XO pre-flight)

| Symbol | File | Exists? |
|---|---|---|
| `EventType.Asset` | `accelerators/anchor-mobile-ios/SunfishField/Events/EventType.swift` | ✓ |
| `EventEnvelope.blobRef: String?` | `accelerators/anchor-mobile-ios/SunfishField/Events/EventEnvelope.swift` | ✓ |
| `BlobStore.put(_ data: Data) throws -> String` | `accelerators/anchor-mobile-ios/SunfishField/Persistence/BlobStore.swift` | ✓ |
| `EventQueueRecord` (GRDB model for enqueue) | `accelerators/anchor-mobile-ios/SunfishField/Persistence/EventQueueRecord.swift` | ✓ |
| `POST /api/v1/field/event` | `accelerators/bridge/Sunfish.Bridge/Field/FieldEndpoints.cs` | ✓ |
| `POST /api/v1/field/blob/{sha256}` | `accelerators/bridge/Sunfish.Bridge/Field/FieldEndpoints.cs` | ✓ |
| `FieldEventEnvelope.EventType: string` | `FieldEndpoints.cs` (internal record, line ~310) | ✓ |
| `Equipment.PrimaryPhotoBlobRef: string?` | `packages/blocks-property-equipment/Models/Equipment.cs` | ✓ |
| `EquipmentId(string Value)` | `packages/blocks-property-equipment/Models/EquipmentId.cs` | ✓ |
| `IEquipmentRepository.GetByIdAsync(TenantId, EquipmentId, CancellationToken)` | `packages/blocks-property-equipment/Services/IEquipmentRepository.cs` | ✓ |
| `IEquipmentRepository.UpsertAsync(Equipment, CancellationToken)` | `packages/blocks-property-equipment/Services/IEquipmentRepository.cs` | ✓ |
| `AuditEventType.FieldEventAccepted` (naming-pattern reference) | `packages/kernel-audit/` | ✓ |
| `Sunfish.Blocks.PropertyEquipment` NOT yet referenced in `Sunfish.Bridge.csproj` | `accelerators/bridge/Sunfish.Bridge/Sunfish.Bridge.csproj` | ✓ confirmed absent — must add |

---

## Phase 1 — iOS: AssetCapturePayload + AssetCaptureView (~3h)

### Payload type

**File to create:**
`accelerators/anchor-mobile-ios/SunfishField/Capture/Asset/AssetCapturePayload.swift`

```swift
import Foundation

/// Canonical-JSON-encoded payload for an `EventType.Asset` envelope.
/// Transmitted inside `EventEnvelope.payload` (base64-encoded Data).
/// The accompanying photo blob is referenced via `EventEnvelope.blobRef`.
public struct AssetCapturePayload: Codable, Sendable, Equatable, Hashable {

    /// The equipment this photo is associated with.
    public let equipmentId: String

    /// Photo role. v1 only ships "primary"; supplementary deferred.
    public let photoKind: PhotoKind

    /// Optional free-text notes recorded by the field agent.
    public let notes: String?

    public init(equipmentId: String, photoKind: PhotoKind = .primary, notes: String? = nil) {
        self.equipmentId = equipmentId
        self.photoKind = photoKind
        self.notes = notes
    }

    public enum PhotoKind: String, Codable, Sendable, CaseIterable {
        case primary
        // supplementary deferred to W#23.2 follow-up
    }
}
```

**Canonical-JSON encoding:** `AssetCapturePayload` is serialized via `JsonCanonical.encode`
(existing `SunfishField/Events/JsonCanonical.swift`) before being placed in
`EventEnvelope.payload`. This matches the `.NET` side's `CanonicalJson.Serialize` contract.

### Capture view

**File to create:**
`accelerators/anchor-mobile-ios/SunfishField/Capture/Asset/AssetCaptureView.swift`

Use `UIImagePickerController` (via `UIViewControllerRepresentable`) for v1 simplicity.
`DataScannerViewController` + Vision OCR (nameplate extraction) is deferred to a
follow-up W#23.2 deepening hand-off — v1 ships photo capture only.

**View contract:**
```swift
/// Parameters passed into the view.
struct AssetCaptureView: View {
    let equipment: EquipmentListItem  // lightweight display struct (see below)
    let queueService: any EventQueueServicing
    let blobStore: BlobStore
    let deviceId: String
    let capturedUnderKernel: String
    let capturedUnderSchemaEpoch: UInt32

    // ...
}
```

`EquipmentListItem` is a new value type in the same file (or sibling file) — a lightweight
display-only projection of the equipment record fetched from Bridge on the home screen
(see Note 1 below).

**Capture flow (inside AssetCaptureView):**
1. Present `UIImagePickerController` in `.camera` source mode.
2. On `imagePickerController(_:didFinishPickingMediaWithInfo:)` callback:
   a. Compress UIImage to JPEG: `uiImage.jpegData(compressionQuality: 0.85)` — must not
      be nil; if nil, show an error toast and discard.
   b. Store via `blobStore.put(jpegData)` — returns lowercase hex SHA-256 (`blobRef`).
   c. Encode payload: `JsonCanonical.encode(AssetCapturePayload(equipmentId: equipment.id))`.
   d. Enqueue via `queueService.enqueue(EventQueueRecord(...))`:
      - `eventType = "Asset"`
      - `payload = canonicalPayloadData`
      - `blobRef = blobRef`
      - `queueStatus = .pending`
   e. Dismiss picker; show "Photo queued" confirmation banner.
3. Sync is handled automatically by the existing `SyncEngine` background loop.

**Gate:**
- PASS iff a `.Asset` event row + a blob file appear in the local GRDB + BlobStore
  after the picker dismisses.

**Note 1 — EquipmentListItem:** The home screen (Phase 6) fetches equipment from Bridge
via a new `GET /api/v1/equipment?tenantId=...` endpoint (NOT in scope of this hand-off;
consider this a known halt — see Halt conditions §H1). For the v1 capture smoke test,
COB may hard-code a test `equipmentId` or stub the equipment list with one item.

### Test file

**File to create:**
`accelerators/anchor-mobile-ios/Tests/SunfishFieldCaptureTests/AssetCapturePayloadTests.swift`

3 test cases:
1. `testEncode_roundTrip` — encode + decode `AssetCapturePayload`; verify field equality.
2. `testCanonicalJson_keyOrder` — verify encoded JSON has keys in alphabetical order
   (`equipmentId`, `notes`, `photoKind`).
3. `testPhotoKind_rawValue` — `PhotoKind.primary.rawValue == "primary"`.

**Package.swift targets:** The new `SunfishFieldCaptureTests` target needs to be added to
`Package.swift`:

```swift
.testTarget(
    name: "SunfishFieldCaptureTests",
    dependencies: ["SunfishField"],
    path: "Tests/SunfishFieldCaptureTests"
)
```

**PR 1 gate:** `swift test` passes (add `SunfishFieldCaptureTests` to the count).

---

## Phase 2 — Bridge: Asset event handler + project reference (~2h)

### Project reference

**File to modify:**
`accelerators/bridge/Sunfish.Bridge/Sunfish.Bridge.csproj`

Add inside the existing `<ItemGroup>` with other `<ProjectReference>` entries:
```xml
<ProjectReference Include="../../../packages/blocks-property-equipment/Sunfish.Blocks.PropertyEquipment.csproj" />
```

### Asset capture payload DTO

**File to create:**
`accelerators/bridge/Sunfish.Bridge/Field/AssetCapturePayload.cs`

```csharp
using System.Text.Json.Serialization;

namespace Sunfish.Bridge.Field;

/// <summary>
/// Canonical-JSON-decoded payload for an <c>EventType.Asset</c> event envelope.
/// Mirrors <c>AssetCapturePayload</c> on the iOS side.
/// </summary>
internal sealed record AssetCapturePayload(
    [property: JsonPropertyName("equipmentId")] string EquipmentId,
    [property: JsonPropertyName("photoKind")]   string PhotoKind,
    [property: JsonPropertyName("notes")]       string? Notes);
```

### Asset event handler

**File to create:**
`accelerators/bridge/Sunfish.Bridge/Field/AssetEventHandler.cs`

```csharp
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Blocks.PropertyEquipment.Models;
using Sunfish.Blocks.PropertyEquipment.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Kernel.Audit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;

namespace Sunfish.Bridge.Field;

/// <summary>
/// Handles <c>EventType == "Asset"</c> field-event envelopes. Resolves the
/// equipment referenced in the payload and stamps
/// <see cref="Equipment.PrimaryPhotoBlobRef"/> with the envelope's blob address.
/// </summary>
internal static class AssetEventHandler
{
    internal static async Task<IResult> HandleAsync(
        FieldEventEnvelope envelope,
        IEquipmentRepository equipmentRepository,
        IAuditTrail auditTrail,
        IOperationSigner signer,
        CancellationToken ct)
    {
        // Parse the canonical-JSON payload.
        AssetCapturePayload? capturePayload;
        try
        {
            capturePayload = JsonSerializer.Deserialize<AssetCapturePayload>(
                envelope.Payload.GetRawText(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = false });
        }
        catch (JsonException ex)
        {
            await EmitRejectAsync(auditTrail, signer, envelope.TenantId,
                "asset-payload-schema-failed", ex.Message, ct).ConfigureAwait(false);
            return Results.BadRequest(new { error = "asset-payload-schema-failed", detail = ex.Message });
        }

        if (capturePayload is null || string.IsNullOrWhiteSpace(capturePayload.EquipmentId))
        {
            await EmitRejectAsync(auditTrail, signer, envelope.TenantId,
                "asset-missing-equipment-id", "equipmentId is required in Asset payload", ct)
                .ConfigureAwait(false);
            return Results.BadRequest(new { error = "asset-missing-equipment-id" });
        }

        if (string.IsNullOrWhiteSpace(envelope.BlobRef))
        {
            await EmitRejectAsync(auditTrail, signer, envelope.TenantId,
                "asset-missing-blob-ref", "blobRef is required for EventType.Asset", ct)
                .ConfigureAwait(false);
            return Results.UnprocessableEntity(new { error = "asset-missing-blob-ref" });
        }

        var equipmentId = new EquipmentId(capturePayload.EquipmentId);
        var existing = await equipmentRepository
            .GetByIdAsync(envelope.TenantId, equipmentId, ct)
            .ConfigureAwait(false);

        if (existing is null)
        {
            await EmitRejectAsync(auditTrail, signer, envelope.TenantId,
                "asset-equipment-not-found", $"equipmentId {capturePayload.EquipmentId} not found",
                ct).ConfigureAwait(false);
            return Results.NotFound(new { error = "asset-equipment-not-found" });
        }

        var updated = existing with { PrimaryPhotoBlobRef = envelope.BlobRef };
        await equipmentRepository.UpsertAsync(updated, ct).ConfigureAwait(false);

        await EmitAcceptAsync(auditTrail, signer, envelope.TenantId,
            envelope.EventId, capturePayload.EquipmentId, envelope.BlobRef, ct)
            .ConfigureAwait(false);

        return Results.Ok(new
        {
            eventId = envelope.EventId,
            equipmentId = capturePayload.EquipmentId,
            primaryPhotoBlobRef = envelope.BlobRef,
        });
    }

    private static Task EmitAcceptAsync(
        IAuditTrail auditTrail, IOperationSigner signer, TenantId tenantId,
        Guid eventId, string equipmentId, string blobRef, CancellationToken ct)
    {
        var payload = new AuditPayload(new System.Collections.Generic.Dictionary<string, object?>
        {
            ["event_id"]      = eventId.ToString("D"),
            ["equipment_id"]  = equipmentId,
            ["blob_ref"]      = blobRef,
        });
        return FieldEndpoints.EmitAuditPublicAsync(
            auditTrail, signer, AuditEventType.FieldAssetPhotoAccepted, tenantId, payload, ct);
    }

    private static Task EmitRejectAsync(
        IAuditTrail auditTrail, IOperationSigner signer, TenantId tenantId,
        string reason, string detail, CancellationToken ct)
    {
        var payload = new AuditPayload(new System.Collections.Generic.Dictionary<string, object?>
        {
            ["detail"] = detail,
            ["reason"] = reason,
        });
        return FieldEndpoints.EmitAuditPublicAsync(
            auditTrail, signer, AuditEventType.FieldAssetPhotoRejected, tenantId, payload, ct);
    }
}
```

**Important:** `FieldEndpoints.EmitAuditAsync` is currently `private static`. To allow
`AssetEventHandler` to call it, the method must be changed to `internal static` and renamed
`EmitAuditPublicAsync` (or left as `EmitAuditAsync` and made `internal`). COB chooses;
the simplest option is `internal static async Task EmitAuditAsync(...)` in
`FieldEndpoints.cs`.

### FieldEndpoints.cs changes

**File to modify:** `accelerators/bridge/Sunfish.Bridge/Field/FieldEndpoints.cs`

Two changes:

**Change 1 — wire IEquipmentRepository from DI:**

The `HandleFieldEventPostAsync` signature currently is:
```csharp
internal static async Task<IResult> HandleFieldEventPostAsync(
    HttpRequest request,
    IAuditTrail auditTrail,
    IOperationSigner signer,
    CancellationToken ct)
```

Add `IEquipmentRepository equipmentRepository` parameter (ASP.NET Core minimal-API DI
injects it from the service container):
```csharp
internal static async Task<IResult> HandleFieldEventPostAsync(
    HttpRequest request,
    IAuditTrail auditTrail,
    IOperationSigner signer,
    IEquipmentRepository equipmentRepository,
    CancellationToken ct)
```

**Change 2 — per-event-type dispatch after idempotency check:**

After the idempotency-cache acceptance block (the final `FieldEventAccepted` emit + `return
Results.Ok(...)` block), the current code always emits `FieldEventAccepted` for ALL event
types. Replace that terminal block with a dispatch switch:

```csharp
// Per-event-type dispatch (v1: only Asset handled; all others fall through to
// the generic FieldEventAccepted emit).
if (envelope.EventType == "Asset")
{
    return await AssetEventHandler.HandleAsync(
        envelope, equipmentRepository, auditTrail, signer, ct).ConfigureAwait(false);
}

// Generic path for unhandled event types (Receipt, Inspection, Signature, Mileage,
// WorkOrderResponse) — accepted and stored; no per-type side-effects yet.
await EmitAuditAsync(auditTrail, signer,
    AuditEventType.FieldEventAccepted,
    envelope.TenantId,
    BuildAcceptPayload(envelope.EventId, envelope.EventType, envelope.DeviceId),
    ct).ConfigureAwait(false);

return Results.Ok(new
{
    eventId = envelope.EventId,
    accepted_at = DateTimeOffset.UtcNow,
});
```

**Change 3 — add `BlobRef` to `FieldEventEnvelope`:**

The current `FieldEventEnvelope` internal record (bottom of file) does not have `BlobRef`.
Add it:
```csharp
internal sealed record FieldEventEnvelope(
    Guid EventId,
    TenantId TenantId,
    string ActorId,
    string EventType,
    JsonElement Payload,
    DateTimeOffset CapturedAt,
    string CapturedUnderKernel,
    uint CapturedUnderSchemaEpoch,
    string DeviceId,
    string? BlobRef);   // ← new; maps to "blobRef" in camelCase JSON
```

Verify that `[JsonPropertyName("blobRef")]` or a `JsonNamingPolicy.CamelCase` setting is
in place on the deserialization call at line ~114 of `FieldEndpoints.cs` (it already sets
`PropertyNamingPolicy = JsonNamingPolicy.CamelCase` — no extra attribute needed).

### Audit EventType constants

**File to modify:** `packages/kernel-audit/AuditEventType.cs`

Add in the Field section (after `FieldDeviceRevoked`):
```csharp
// W#23.2 — Equipment Asset Photo
public static readonly AuditEventType FieldAssetPhotoAccepted = new("field.asset-photo.accepted");
public static readonly AuditEventType FieldAssetPhotoRejected = new("field.asset-photo.rejected");
```

---

## Phase 3 — Tests + ledger note (~1.5h)

### Bridge test

**File to create:**
`accelerators/bridge/Sunfish.Bridge.Tests/Field/AssetEventHandlerTests.cs`

Verify the Bridge.Tests project exists and add the test class.

3 test cases (xUnit):

1. `HandleAsync_AcceptsValidPayload_UpdatesEquipmentAndReturnsOk` — mock
   `IEquipmentRepository` returning a known equipment; call `AssetEventHandler.HandleAsync`
   with a valid `FieldEventEnvelope` (EventType "Asset", blobRef set, payload = encoded
   `AssetCapturePayload`); assert `UpsertAsync` called with `PrimaryPhotoBlobRef` = the
   envelope's blobRef; assert `Results.Ok(...)`.

2. `HandleAsync_EquipmentNotFound_Returns404` — mock repository returning `null`; assert
   `Results.NotFound(...)` + `FieldAssetPhotoRejected` emitted.

3. `HandleAsync_MissingBlobRef_Returns422` — envelope with `BlobRef = null`; assert
   `Results.UnprocessableEntity(...)`.

**Test helper:** use `InMemoryEquipmentRepository` (already exists in
`packages/blocks-property-equipment/Services/InMemoryEquipmentRepository.cs`) rather than
mocking `IEquipmentRepository`.

### Ledger note

No workstream state change needed — W#23 is still `ready-to-build` for this hand-off.
When COB ships PR 1 + PR 2, update the W#23 workstream source file
`icm/_state/workstreams/W23-ios-field-capture-app-substrate-v1.md`:
- Bump `status_cell` to mention W#23.2 equipment photo shipped
- Add PR links to `reference_cell`

---

## Halt conditions

| # | Condition | Action |
|---|---|---|
| H1 | Home screen (P6 from main hand-off) not yet shipped — no `HomeView.swift` entry point for the capture flow | HALT; build P6 first |
| H2 | `InMemoryEquipmentRepository` is NOT registered in Bridge DI (Bridge uses EFCore repo for production) | Confirm IEquipmentRepository is registered in Bridge startup; if not, add `services.AddSingleton<IEquipmentRepository, InMemoryEquipmentRepository>()` as a dev-only registration behind a feature flag OR accept that the handler returns 500 until the EFCore adapter ships |
| H3 | `FieldEndpoints.EmitAuditAsync` is private and cannot be accessed from `AssetEventHandler` | Change visibility to `internal static` as described in Phase 2; if this introduces another visibility issue, escalate to XO |
| H4 | `FieldEventEnvelope` BlobRef field not populated by Bridge on deserialization (field absent from iOS envelope JSON) | Confirm iOS EventEnvelope.blobRef serializes correctly (it is non-optional in the struct; check JsonCanonical handles nil/absent correctly) |

---

## Acceptance criteria

- [ ] `swift test` passes with new `SunfishFieldCaptureTests` target
- [ ] `AssetCapturePayload` round-trips through `JsonCanonical.encode` + standard `JSONDecoder` decode
- [ ] `AssetCaptureView.swift` compiles in Xcode without warnings (camera permissions note:
  `NSCameraUsageDescription` key must be present in `Info.plist`; if missing, add it)
- [ ] Bridge project builds with `Sunfish.Blocks.PropertyEquipment` reference
- [ ] `AssetEventHandler.HandleAsync` with a valid envelope + pre-seeded `IEquipmentRepository`:
  - returns `Results.Ok(...)` with `primaryPhotoBlobRef` set
  - `IEquipmentRepository.UpsertAsync` called with updated equipment
  - `FieldAssetPhotoAccepted` emitted
- [ ] All 3 `AssetEventHandlerTests` pass
- [ ] `FieldAssetPhotoAccepted` + `FieldAssetPhotoRejected` `AuditEventType` constants present in `kernel-audit`
- [ ] Security council review BEFORE auto-merge (new event-dispatch path + equipment mutation)

---

## Total decomposition

| Phase | Subject | Hours |
|---|---|---|
| 1 | iOS AssetCapturePayload + AssetCaptureView + capture tests | ~3h |
| 2 | Bridge AssetCapturePayload DTO + AssetEventHandler + FieldEndpoints dispatch + audit constants | ~2h |
| 3 | Bridge tests + ledger note | ~1.5h |
| **Total** | | **~6.5h** |

---

## Decision-class

Session-class per `feedback_decision_discipline` Rule 1 (NOT CO-class — capture-flow
phase per main hand-off table; `Equipment.PrimaryPhotoBlobRef` was pre-designed for this;
no new external APIs). Security council mandatory before auto-merge (new event-dispatch
path mutates equipment records).

---

## References

- **Main hand-off:** `icm/_state/handoffs/property-ios-field-app-stage06-handoff.md` §Capture-flow follow-up hand-offs
- **P5 pairing hand-off:** `icm/_state/handoffs/property-ios-field-app-stage06-p5-pairing-handoff.md`
- **Equipment substrate:** `packages/blocks-property-equipment/Models/Equipment.cs`
- **Bridge field endpoints:** `accelerators/bridge/Sunfish.Bridge/Field/FieldEndpoints.cs`
- **ADR 0028 + A1–A3:** CRDT engine + iOS field app substrate
