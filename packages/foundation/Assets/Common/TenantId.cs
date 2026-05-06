using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Assets.Common;

/// <summary>Opaque tenant identifier for multi-tenant data isolation.</summary>
/// <remarks>
/// <para>
/// <b>Reserved-prefix guard (W#1 WS-A / ADR 0084 §1):</b> values starting with
/// <c>"__"</c> are reserved for Sunfish system sentinels (currently
/// <see cref="System"/>). External callers attempting to construct such a
/// <see cref="TenantId"/> via the public constructor receive an
/// <see cref="ArgumentException"/>; sentinels are constructed via the
/// private <c>CreateSentinel</c> factory which bypasses the guard via the
/// with-init pattern (per ADR 0084 §1 OQ-1).
/// </para>
/// <para>
/// <b>Sentinels:</b>
/// <list type="bullet">
/// <item><description><see cref="System"/> — Sunfish system actor (background
/// jobs, migrations, server-side processes). Use instead of
/// <see cref="Default"/> for system context.</description></item>
/// <item><description><see cref="Default"/> — pre-WS-A fallback. Marked
/// <see cref="ObsoleteAttribute"/>; will be removed in WS-B (ADR 0085).</description></item>
/// </list>
/// </para>
/// </remarks>
[JsonConverter(typeof(TenantIdJsonConverter))]
public readonly record struct TenantId
{
    /// <summary>The opaque string value. Never null when constructed via the public ctor.</summary>
    public string Value { get; init; }

    /// <summary>
    /// Construct a tenant id. Values starting with <c>"__"</c> are reserved
    /// for Sunfish system sentinels and rejected with
    /// <see cref="ArgumentException"/> per ADR 0084 §1.
    /// </summary>
    public TenantId(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.StartsWith("__", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"TenantId values may not start with '__' (received: '{value}'). "
                + "The '__' prefix is reserved for Sunfish system sentinels.",
                nameof(value));
        }
        Value = value;
    }

    /// <summary>
    /// True when this <see cref="TenantId"/> is a Sunfish system sentinel
    /// (value starts with the reserved <c>"__"</c> prefix). Default-constructed
    /// instances (where <see cref="Value"/> is null) are also treated as sentinels
    /// — fail-closed for the multi-tenant query path. Per ADR 0084 §1 + W#1 WS-A
    /// security follow-up MF-1.
    /// </summary>
    public bool IsSystemSentinel =>
        Value?.StartsWith("__", StringComparison.Ordinal) ?? true;

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator TenantId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(TenantId id) => id.Value;

    /// <summary>
    /// Sentinel representing the Sunfish system actor (background jobs,
    /// migrations, server-side processes). Use instead of
    /// <see cref="Default"/> for system context. Per ADR 0084 §1.
    /// </summary>
    public static readonly TenantId System = CreateSentinel("__system__");

    /// <summary>
    /// Pre-WS-A default tenant. Use <see cref="System"/> for system context
    /// or a real tenant id for tenant-scoped operations.
    /// </summary>
    [Obsolete("Use TenantId.System for system/background-job context, or a real "
              + "tenant id for tenant-scoped operations. TenantId.Default will be "
              + "removed in WS-B (ADR 0085). Per ADR 0084 §1.")]
    public static readonly TenantId Default = new("default");

    /// <summary>
    /// Private factory bypassing the <see cref="TenantId(string)"/>
    /// reserved-prefix guard. C# <c>readonly record struct</c> cannot be
    /// subclassed; the with-init pattern is the supported path to construct
    /// sentinels per ADR 0084 §1 OQ-1.
    /// </summary>
    private static TenantId CreateSentinel(string value) => new TenantId { Value = value };
}

internal sealed class TenantIdJsonConverter : JsonConverter<TenantId>
{
    public override TenantId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("TenantId must be a non-null string.");
        return new TenantId(str);
    }

    public override void Write(Utf8JsonWriter writer, TenantId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
