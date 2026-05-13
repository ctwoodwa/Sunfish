import Foundation

/// Canonical-JSON-encoded payload for an `EventType.Asset` envelope.
/// Transmitted inside `EventEnvelope.payload`. The accompanying photo
/// blob is referenced via `EventEnvelope.blobRef`.
public struct AssetCapturePayload: Codable, Sendable, Equatable, Hashable {

    /// The equipment this photo is associated with.
    public let equipmentId: String

    /// Photo role. v1 ships "primary" only; supplementary deferred to W#23.2 deepening.
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
    }
}
