using System.Text.Json;

using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Integration.KernelSyncRoundtrip.Harness;

/// <summary>
/// Test-only encoder for <see cref="KernelEvent"/> on the wire. The real
/// Sunfish sync path rides CRDT ops through <c>DELTA_STREAM</c>; these
/// integration tests put KernelEvents into the <c>crdt_ops</c> byte field
/// directly because we exercise the transport + framing, not the CRDT
/// encoding path.
/// </summary>
internal static class EventEncoder
{
    public static byte[] Encode(KernelEvent evt)
    {
        var envelope = new Envelope
        {
            Id = evt.Id.Value,
            EntityId = evt.EntityId.ToString(),
            Kind = evt.Kind,
            OccurredAtTicks = evt.OccurredAt.UtcTicks,
            OccurredAtOffsetMinutes = (int)evt.OccurredAt.Offset.TotalMinutes,
            Payload = evt.Payload.ToDictionary(
                kv => kv.Key,
                kv => kv.Value?.ToString()),
        };
        return JsonSerializer.SerializeToUtf8Bytes(envelope);
    }

    public static KernelEvent Decode(byte[] bytes)
    {
        var env = JsonSerializer.Deserialize<Envelope>(bytes)
            ?? throw new InvalidOperationException("Decoded envelope was null.");
        return new KernelEvent(
            Id: new EventId(env.Id),
            EntityId: EntityId.Parse(env.EntityId!),
            Kind: env.Kind!,
            OccurredAt: new DateTimeOffset(
                env.OccurredAtTicks,
                TimeSpan.FromMinutes(env.OccurredAtOffsetMinutes)),
            Payload: (env.Payload ?? new Dictionary<string, string?>())
                .ToDictionary(kv => kv.Key, kv => (object?)kv.Value));
    }

    private sealed class Envelope
    {
        public Guid Id { get; set; }
        public string? EntityId { get; set; }
        public string? Kind { get; set; }
        public long OccurredAtTicks { get; set; }
        public int OccurredAtOffsetMinutes { get; set; }
        public Dictionary<string, string?>? Payload { get; set; }
    }
}
