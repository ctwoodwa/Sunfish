# Hand-off — W#23.4 iOS Signatures capture flow

**From:** XO (research session)
**To:** sunfish-PM (COB)
**Created:** 2026-05-15
**Parent workstream:** W#23 (iOS Field-Capture App substrate v1)
**Pipeline variant:** `sunfish-feature-change`
**Estimate:** ~8–12h / 3 PRs

> **GATE:** Do not start until W#23 Phase 6 (home screen) is shipped and merged to main.
> W#23.2 (Equipment Photo) does NOT gate this — signatures are independent of the equipment
> cache. W#23.3 (Inspections) and W#23.6 (Work Orders) may build in parallel.

---

## Context

W#23.4 implements the Signatures capture flow on top of the W#23 substrate. It surfaces the
`ISignatureCapture` contract through a three-step SwiftUI flow: review document → affirm
consent → draw PencilKit signature → submit signed event.

The cryptographic layer uses the **Ed25519 key already provisioned by W#23 Phase 0** (the
`InstallIdentity` per-device keypair stored in the iOS Keychain). No new keys, no new
entitlements — this is the key the device already has.

**Domain foundation (all on main):**
- `Sunfish.Kernel.Signatures` — W#21, fully built ✓
  - `ISignatureCapture.CaptureAsync(SignatureCaptureRequest, CancellationToken)`
  - `SignatureCaptureRequest` — see full shape below
  - `SignatureEnvelope(Algorithm, Signature, Headers)` — algorithm-agility container
  - `ContentHash(byte[] Bytes)` — SHA-256 wrapper; constructed directly from hash bytes
  - `CaptureQuality` — stroke fidelity + clock source + document-review flags
  - `PenStrokeBlobRef(BlobUri, ByteCount)` — optional high-res stroke reference
  - `ConsentRecordId(Guid Value)` — stable consent identifier
  - `PenStrokeFidelity` enum: `None | LowResolution | HighResolution`
  - `ClockSource` enum: `DeviceClock | NtpVerified | TrustedTimestamp | ServerSide`
  - `Geolocation` — WGS-84 lat/lng + optional accuracy + source string

**iOS frameworks used:**
- `PDFKit` — `PDFView` (document display)
- `PencilKit` — `PKCanvasView` (signature canvas, via `UIViewRepresentable`)
- `Crypto` (Apple CryptoKit, already imported by `InstallIdentity.swift`)
- `CoreLocation` — optional one-shot location fetch (no new permission prompt if denied)
- No new entitlements required

**EventType:** `.Signature` already exists in `SunfishField/Events/EventType.swift` (confirmed).

---

## Signing algorithm

`InstallIdentity.signingKey()` returns a `Curve25519.Signing.PrivateKey` (Ed25519).
Use `"ed25519"` as the `SignatureEnvelope.Algorithm` string per the type's doc comment.
The Ed25519 signature over the SHA-256 document hash is 64 bytes (compact representation).

On the Bridge side, `ISignatureCapture` receives the pre-computed signature envelope —
it stores and verifies the signature; the iOS device is the authoritative signer.

---

## Phase 1 — Document viewer + consent gate + PencilKit canvas (~3–4h)

### New directory

`accelerators/anchor-mobile-ios/SunfishField/Signatures/`

### Files

`SignatureListView.swift`
- Fetches via Bridge: `GET /api/v1/field/signatures?status=pending`
- Each row: document title, requested date
- Pull-to-refresh + offline stale indicator (GRDB `signature_requests` table — added Phase 3)
- Tapping a row → `DocumentReviewView`

`DocumentReviewView.swift`
- Wraps `PDFKit.PDFView` in a `UIViewRepresentable`
- PDF bytes loaded from `documentBase64` field in the DTO (pre-encoded for offline support)
- "Sign" button disabled until the user has navigated to the last page
  (`NotificationCenter.default.addObserver(forName: .PDFViewPageChanged…`)
- On "Sign" → `ConsentCheckView`
- Back button discards (no event emitted)

`ConsentCheckView.swift`
- Shows `consentText` from DTO (or a fallback: "I agree to use electronic signatures for
  this document and understand it has the same legal effect as a handwritten signature.")
- "I Agree" → `SignatureCanvasView`
- "Decline" → dismisses entire flow; no event emitted

`SignatureCanvasView.swift`
- `PKCanvasView` wrapped in `UIViewRepresentable`; `drawingPolicy: .anyInput`
- `PKToolPicker` shown when canvas becomes first responder (Pencil or finger supported)
- "Clear" button resets canvas
- "Submit" button (active when `canvas.drawing.strokes.isEmpty == false`) →
  calls `SignatureCaptureService.capture(...)` then dismisses to `SignatureListView`

### Bridge route additions (new file)

`accelerators/bridge/Sunfish.Bridge/Field/SignatureFieldEndpoints.cs`

`GET /api/v1/field/signatures`
- Query param: `?status=pending` (optional)
- Returns `SignatureRequestFieldDto[]`:

```csharp
public record SignatureRequestFieldDto(
    string Id,
    string DocumentTitle,
    string DocumentBase64,  // Base64-encoded PDF bytes (for offline-first)
    string ConsentRecordId,
    string? ConsentText,
    string[] TaxonomyScope,
    DateTimeOffset? RequestedAt
);
```

> The document is embedded as Base64 rather than a presigned URL so that the field app
> can display it offline after the initial sync.

### Tests (Phase 1)

- `GET /api/v1/field/signatures` returns only pending requests for the authenticated tenant
- Response DTO includes `documentBase64` that round-trips through Base64 decode

**PR title:** `feat(anchor-mobile-ios,bridge): W#23.4 Phase 1 — signature list + document viewer + consent gate + PencilKit canvas + Bridge listing`

---

## Phase 2 — CryptoKit signing + event payload construction (~3–4h)

### New file

`SignatureCaptureService.swift` (in `Signatures/`)

**Input:** `documentData: Data`, `documentId: String`, `consentRecordId: String`,
`scope: [String]`, `canvas: PKDrawing`, `identity: InstallIdentity`, `blobStore: BlobStore`

**Steps:**

1. **SHA-256 document hash:**
   ```swift
   import Crypto
   let digest = SHA256.hash(data: documentData)
   let documentHashBase64 = Data(digest).base64EncodedString()
   ```

2. **Ed25519 signature:**
   ```swift
   let key = try identity.signingKey()          // Curve25519.Signing.PrivateKey
   let sig = try key.signature(for: Data(digest))  // 64 bytes
   let signatureBytesBase64 = sig.base64EncodedString()
   let algorithm = "ed25519"
   let kid = identity.deviceId.description       // or uuidString if DeviceId is UUID-backed
   ```

3. **Pen stroke blob (optional, only when Apple Pencil detected):**
   ```swift
   var penStrokeBlobUri: String? = nil
   var penStrokeByteCount: Int? = nil
   if !canvas.strokes.isEmpty {
       let strokeData = try canvas.dataRepresentation()
       penStrokeBlobUri = try blobStore.put(strokeData)
       penStrokeByteCount = strokeData.count
   }
   ```
   Stroke fidelity = `"HighResolution"` if any stroke has a `UITouch.TouchType.pencil` source;
   otherwise `"LowResolution"`.

4. **Geolocation (optional):**
   One-shot `CLLocationManager` fetch using the `CLLocationManagerDelegate` pattern.
   If location permission is not yet granted: skip (do not prompt mid-flow).

5. **Enqueue event:**
   ```swift
   let payload = SignatureCapturedPayload(
       documentId: documentId,
       consentRecordId: consentRecordId,
       documentHashBase64: documentHashBase64,
       algorithm: "ed25519",
       signatureBytesBase64: signatureBytesBase64,
       signatureHeaders: ["kid": kid],
       taxonomyScope: scope,
       strokeFidelity: "HighResolution", // or "LowResolution"
       clockSource: "DeviceClock",
       deviceTouchAvailable: true,
       documentReviewedBeforeSign: true,  // set by DocumentReviewView before advancing
       penStrokeBlobUri: penStrokeBlobUri,
       penStrokeByteCount: penStrokeByteCount,
       latitude: location?.coordinate.latitude,
       longitude: location?.coordinate.longitude,
       locationAccuracyMeters: location?.horizontalAccuracy,
       locationSource: "gps"
   )
   try eventQueue.enqueue(EventEnvelope(eventType: .Signature, payload: payload))
   ```

### Swift `Codable` payload struct

```swift
struct SignatureCapturedPayload: Codable {
    let documentId: String
    let consentRecordId: String
    let documentHashBase64: String
    let algorithm: String
    let signatureBytesBase64: String
    let signatureHeaders: [String: String]
    let taxonomyScope: [String]
    let strokeFidelity: String
    let clockSource: String
    let deviceTouchAvailable: Bool
    let documentReviewedBeforeSign: Bool
    let penStrokeBlobUri: String?
    let penStrokeByteCount: Int?
    let latitude: Double?
    let longitude: Double?
    let locationAccuracyMeters: Double?
    let locationSource: String?
}
```

**PR title:** `feat(anchor-mobile-ios): W#23.4 Phase 2 — CryptoKit Ed25519 signing + SignatureCaptureService + event payload`

---

## Phase 3 — Bridge routing + GRDB cache + docs (~2–3h)

### Bridge field-event dispatcher addition

In the existing Bridge field-event dispatcher (wherever `POST /api/v1/field/event` routes
events to domain services):

```csharp
case "Signature":
    await _signatureCapture.CaptureAsync(new SignatureCaptureRequest
    {
        Tenant    = new TenantId(actorContext.TenantId),
        Signer    = new ActorId(actorContext.ActorId),
        Consent   = new ConsentRecordId(Guid.Parse(payload.consentRecordId)),
        DocumentHash = new ContentHash(Convert.FromBase64String(payload.documentHashBase64)),
        Scope = payload.taxonomyScope
                    .Select(s => new TaxonomyClassification(s))
                    .ToList<TaxonomyClassification>(),
        Envelope = new SignatureEnvelope(
            payload.algorithm,
            Convert.FromBase64String(payload.signatureBytesBase64),
            (payload.signatureHeaders as IDictionary<string, string>)
                ?? new Dictionary<string, string>()),
        Quality = new CaptureQuality
        {
            StrokeFidelity         = Enum.Parse<PenStrokeFidelity>(payload.strokeFidelity),
            ClockSource            = Enum.Parse<ClockSource>(payload.clockSource),
            DeviceTouchAvailable   = payload.deviceTouchAvailable,
            DocumentReviewedBeforeSign = payload.documentReviewedBeforeSign,
        },
        PenStroke = payload.penStrokeBlobUri is null ? null
            : new PenStrokeBlobRef(payload.penStrokeBlobUri, payload.penStrokeByteCount ?? 0L),
        Location = payload.latitude is null ? null : new Geolocation
        {
            Latitude       = payload.latitude.Value,
            Longitude      = payload.longitude.Value,
            AccuracyMeters = payload.locationAccuracyMeters,
            Source         = payload.locationSource ?? "gps",
        },
        Attestation = null, // Apple App Attest deferred to future W#23 phase
    }, ct);
    break;
```

**Halt condition:** `TaxonomyClassification` constructor does not accept a plain `string` →
read `packages/foundation/Taxonomy/Models/TaxonomyClassification.cs` and adapt. Do NOT
modify the domain type.

### GRDB offline cache schema

```sql
CREATE TABLE signature_requests (
    id TEXT PRIMARY KEY,
    document_title TEXT NOT NULL,
    document_base64 TEXT NOT NULL,
    consent_record_id TEXT NOT NULL,
    consent_text TEXT,
    taxonomy_scope TEXT NOT NULL, -- JSON-encoded string array
    requested_at TEXT,
    status TEXT NOT NULL DEFAULT 'pending', -- 'pending' | 'captured'
    cached_at TEXT NOT NULL
) WITHOUT ROWID;
```

Populated on successful `GET /api/v1/field/signatures`. Stale indicator if `cached_at > 24h`.
`SignatureListView` reads from cache when offline; shows stale badge when past threshold.
On successful event submission: update local row `status = 'captured'` so the item leaves
the pending list without requiring a network round-trip.

### Docs

- `apps/docs/kernel/signatures/ios-field-signature-capture.md` — capture flow description,
  event payload schema, Ed25519 signing approach, consent gate semantics, pen-stroke fidelity
  guide, Bridge routing, offline behavior

### Ledger note

Add W#23.4 note to W#23 workstream source file's Notes section (do not flip W#23 to built).

**PR title:** `feat(anchor-mobile-ios,bridge): W#23.4 Phase 3 — Bridge routing + GRDB signature cache + docs`

---

## Acceptance criteria

- [ ] Signature list screen shows pending signature requests for tenant
- [ ] Tapping a request opens PDFKit viewer; "Sign" button disabled until last page reached
- [ ] "I Agree" consent gate required before PencilKit canvas is shown
- [ ] "Decline" dismisses without emitting an event
- [ ] PencilKit canvas: Clear + Submit; Submit disabled on empty canvas
- [ ] Submit emits `.Signature` event with Ed25519 signature over SHA-256 document hash
- [ ] `algorithm` field = `"ed25519"` in event payload
- [ ] `documentReviewedBeforeSign = true` always set when submitted through the normal flow
- [ ] High-res pen stroke stored in `BlobStore` and referenced in event payload
- [ ] `strokeFidelity = "HighResolution"` when Apple Pencil used; `"LowResolution"` otherwise
- [ ] Offline: pending requests shown from GRDB cache with stale indicator
- [ ] After submission: item removed from pending list (local cache `status = 'captured'`)
- [ ] Bridge routing: `Signature` event calls `ISignatureCapture.CaptureAsync` with full request
- [ ] 2 Bridge tests: list endpoint + signature event routing with correct `CaptureQuality`
- [ ] docs page live at `apps/docs/kernel/signatures/ios-field-signature-capture.md`

---

## Halt conditions

1. `EventType.Signature` case missing from Swift enum → add it (confirmed present; check again
   at build time if W#23 Phase 3 was modified since hand-off was authored).
2. `InstallIdentity.signingKey()` does not compile or has changed signature →
   read `Sources/Identity/InstallIdentity.swift`; confirmed as `throws -> Curve25519.Signing.PrivateKey`.
3. `PKDrawing.dataRepresentation()` not available on target iOS version → use
   `try NSKeyedArchiver.archivedData(withRootObject: canvas.drawing, requiringSecureCoding: true)`.
4. `TaxonomyClassification` constructor does not accept a plain string → read the type
   and adapt payload; do NOT invent or modify the domain model.
5. `ConsentRecordId` does not accept `Guid` → read `kernel-signatures/Models/Identifiers.cs`;
   confirmed as `readonly record struct ConsentRecordId(Guid Value)`.
6. W#23 Phase 6 not yet shipped → do not start; check `gh pr list` for P6 PR first.
