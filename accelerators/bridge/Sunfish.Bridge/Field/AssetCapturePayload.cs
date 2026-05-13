using System.Text.Json.Serialization;

namespace Sunfish.Bridge.Field;

/// <summary>
/// Canonical-JSON-decoded payload for an <c>EventType.Asset</c> event envelope.
/// Mirrors <c>AssetCapturePayload</c> on the iOS side (W#23.2 P1).
/// </summary>
internal sealed record AssetCapturePayload(
    [property: JsonPropertyName("equipmentId")] string EquipmentId,
    [property: JsonPropertyName("photoKind")]   string PhotoKind,
    [property: JsonPropertyName("notes")]       string? Notes);
