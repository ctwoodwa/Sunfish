using System;
using System.Text.Json;

namespace Sunfish.Bridge.Field;

/// <summary>Wire-format envelope as parsed off the request body.</summary>
/// <remarks>
/// Field shape mirrors <c>EventEnvelope</c> on the iOS side (W#23 P3
/// PR #516 post-A9 wire shape). Bridge accepts the envelope as opaque
/// JSON for substrate v1; signature verification + per-event-type
/// schema validation are follow-up work.
/// </remarks>
internal sealed record FieldEventEnvelope(
    Guid EventId,
    Sunfish.Foundation.Assets.Common.TenantId TenantId,
    string ActorId,
    string EventType,
    JsonElement Payload,
    DateTimeOffset CapturedAt,
    string CapturedUnderKernel,
    uint CapturedUnderSchemaEpoch,
    string DeviceId,
    string? BlobRef);   // W#23.2: content-addressed blob address for EventType.Asset
