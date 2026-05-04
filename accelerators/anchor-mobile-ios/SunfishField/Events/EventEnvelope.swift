import Foundation

/// Wire-format event envelope for outbound sync. Per ADR 0028-A1 (envelope
/// shape) + ADR 0028-A9 (post-A9 augmentation: `capturedUnderKernel` +
/// `capturedUnderSchemaEpoch`).
///
/// **Serialization is via pragmatic canonical JSON** matching
/// `Sunfish.Foundation.Crypto.CanonicalJson` on the .NET side
/// (sorted keys + no whitespace + UTF-8; NOT full RFC 8785 / JCS).
/// W#23 Phase 3.5 ships the Swift mirror at
/// `SunfishField/Events/JsonCanonical.swift`; the merge-boundary date
/// fidelity gap is documented in that file's `FIXME(W#23-P4)` block.
///
/// Per ADR 0028-A7.8 the wire form uses camelCase property names — the
/// `CodingKeys` map below bridges Swift's PascalCase struct fields to
/// the camelCase wire form.
public struct EventEnvelope: Codable, Sendable, Equatable, Hashable {
    /// Monotonically-increasing per-install sequence number assigned at
    /// capture time. Uniquely identifies the event within the device.
    public let deviceLocalSeq: UInt64

    /// Wall-clock time of capture (ISO 8601 UTC).
    public let capturedAt: Date

    /// Per-install device id derived from the install Ed25519 public key
    /// (first 16 hex chars of SHA-256). Phase 0 ships `DeviceId.derive`.
    public let deviceId: String

    /// Capture domain per ADR 0028-A2.1.
    public let eventType: EventType

    /// Canonical-encoded event-specific payload bytes (RFC 8785 / JCS).
    /// Phase 3.5 ships the full RFC 8785 Swift canonicalizer.
    public let payload: Data

    /// Optional content address (lowercase hex SHA-256) for the binary
    /// attachment stored in the BlobStore (Phase 2). Substrate v1 carries
    /// a single attachment per envelope to match the V1Migration
    /// `event_queue.blob_ref` TEXT column shape; Phase 4+ extends to an
    /// N-ary join table when capture flows need multi-attachment events.
    public let blobRef: String?

    /// **A9 (post-A9):** kernel SemVer running on the iPad at capture
    /// time. Per ADR 0028-A9 + A6.11 the merge boundary uses this to
    /// enforce the kernel-minor-lag compatibility window per A6.5.
    public let capturedUnderKernel: String

    /// **A9 (post-A9):** schema epoch the iPad was on at capture time.
    /// Per ADR 0028-A9 + A7.5 the merge boundary uses this to detect
    /// pre-A9 envelopes vs. epoch-divergent envelopes.
    public let capturedUnderSchemaEpoch: UInt32

    public init(
        deviceLocalSeq: UInt64,
        capturedAt: Date,
        deviceId: String,
        eventType: EventType,
        payload: Data,
        blobRef: String? = nil,
        capturedUnderKernel: String,
        capturedUnderSchemaEpoch: UInt32
    ) {
        self.deviceLocalSeq = deviceLocalSeq
        self.capturedAt = capturedAt
        self.deviceId = deviceId
        self.eventType = eventType
        self.payload = payload
        self.blobRef = blobRef
        self.capturedUnderKernel = capturedUnderKernel
        self.capturedUnderSchemaEpoch = capturedUnderSchemaEpoch
    }

    enum CodingKeys: String, CodingKey {
        case deviceLocalSeq, capturedAt, deviceId, eventType, payload, blobRef
        case capturedUnderKernel, capturedUnderSchemaEpoch
    }
}
