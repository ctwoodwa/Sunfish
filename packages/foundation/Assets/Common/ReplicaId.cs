using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Assets.Common;

/// <summary>
/// Opaque per-replica identifier — names the local Sunfish replica
/// that originated an event or owns a record-class lease per
/// <c>_shared/engineering/cross-cluster-event-bus-design.md</c> §1
/// (canonical envelope) + the kernel CP/AP per-record-class lease
/// design.
/// </summary>
/// <remarks>
/// <para>
/// Two devices belonging to the same multi-team install carry
/// distinct <see cref="ReplicaId"/>s. Replica identity is mint-once
/// at install time and persists across app restarts; downstream
/// consumers use it for last-writer-wins arbitration, lease holders,
/// and cross-replica event provenance.
/// </para>
/// <para>
/// <b>Reserved-prefix guard:</b> values starting with <c>"__"</c> are
/// reserved for Sunfish system sentinels (currently
/// <see cref="System"/>). The pattern mirrors
/// <see cref="TenantId"/>'s ADR 0084 §1 guard.
/// </para>
/// </remarks>
[JsonConverter(typeof(ReplicaIdJsonConverter))]
public readonly record struct ReplicaId
{
    /// <summary>The opaque string value. Never null when constructed via the public ctor.</summary>
    public string Value { get; init; }

    /// <summary>
    /// Construct a replica id. Values starting with <c>"__"</c> are
    /// reserved for Sunfish system sentinels and rejected with
    /// <see cref="ArgumentException"/>.
    /// </summary>
    public ReplicaId(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.StartsWith("__", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"ReplicaId values may not start with '__' (received: '{value}'). "
                + "The '__' prefix is reserved for Sunfish system sentinels.",
                nameof(value));
        }
        Value = value;
    }

    /// <summary>
    /// True when this <see cref="ReplicaId"/> is a Sunfish system
    /// sentinel (value starts with the reserved <c>"__"</c> prefix).
    /// Default-constructed instances (where <see cref="Value"/> is
    /// null) are also treated as sentinels — fail-closed for
    /// cross-replica arbitration.
    /// </summary>
    public bool IsSystemSentinel =>
        Value?.StartsWith("__", StringComparison.Ordinal) ?? true;

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator ReplicaId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(ReplicaId id) => id.Value;

    /// <summary>
    /// Sentinel representing the Sunfish system replica — background
    /// jobs, server-side processes, and tests that do not need a real
    /// per-device identity. Per the ADR 0084 §1 sentinel pattern.
    /// </summary>
    public static readonly ReplicaId System = CreateSentinel("__system__");

    /// <summary>
    /// Private factory bypassing the <see cref="ReplicaId(string)"/>
    /// reserved-prefix guard. <c>readonly record struct</c> cannot be
    /// subclassed; the with-init pattern is the supported path to
    /// construct sentinels.
    /// </summary>
    private static ReplicaId CreateSentinel(string value) => new ReplicaId { Value = value };
}

internal sealed class ReplicaIdJsonConverter : JsonConverter<ReplicaId>
{
    public override ReplicaId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("ReplicaId must be a non-null string.");
        return new ReplicaId(str);
    }

    public override void Write(Utf8JsonWriter writer, ReplicaId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
