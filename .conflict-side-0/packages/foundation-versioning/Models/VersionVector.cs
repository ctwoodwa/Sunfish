using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Versioning;

/// <summary>
/// Wire-format version vector exchanged at the Sunfish federation handshake
/// (ADR 0028-A6 + A7). The receiving peer feeds this into the local
/// compatibility-relation evaluator alongside its local vector to produce
/// a <see cref="VersionVectorVerdict"/>.
/// </summary>
/// <remarks>
/// <para>
/// Canonical-JSON shape per A7.8 — camelCase keys (<c>kernel</c>, <c>plugins</c>,
/// <c>adapters</c>, <c>schemaEpoch</c>, <c>channel</c>, <c>instanceClass</c>);
/// enum values serialized as their literal name (<c>Stable</c>, <c>Beta</c>,
/// <c>Nightly</c>; <c>SelfHost</c>, <c>ManagedBridge</c>). Round-trip via
/// <see cref="Sunfish.Foundation.Crypto.CanonicalJson"/>.
/// </para>
/// <para>
/// The <see cref="Plugins"/> map's value type is <see cref="PluginVersionVectorEntry"/>
/// (per A7.3) — carries both the SemVer string and the required-flag so rule
/// 3 (required-plugin intersection) evaluates symmetrically without consulting
/// bundle manifests at handshake time.
/// </para>
/// </remarks>
/// <param name="Kernel">Kernel SemVer string (e.g., <c>"1.3.0"</c>).</param>
/// <param name="Plugins">Per-plugin entries (see <see cref="PluginVersionVectorEntry"/>).</param>
/// <param name="Adapters">UI-adapter SemVer map (e.g., <c>{ "blazor": "0.9.0" }</c>).</param>
/// <param name="SchemaEpoch">Schema-epoch counter (A6.2 rule 1; mismatch is a hard rejection).</param>
/// <param name="Channel">Release channel (A6.2 rule 5).</param>
/// <param name="InstanceClass">Sunfish-instance class (A6.2 rule 6 / A7.6 reduced enum).</param>
public sealed record VersionVector(
    [property: JsonPropertyName("kernel")] string Kernel,
    [property: JsonPropertyName("plugins")] IReadOnlyDictionary<PluginId, PluginVersionVectorEntry> Plugins,
    [property: JsonPropertyName("adapters")] IReadOnlyDictionary<AdapterId, string> Adapters,
    [property: JsonPropertyName("schemaEpoch")] uint SchemaEpoch,
    [property: JsonPropertyName("channel"), JsonConverter(typeof(JsonStringEnumConverter))] ChannelKind Channel,
    [property: JsonPropertyName("instanceClass"), JsonConverter(typeof(JsonStringEnumConverter))] InstanceClassKind InstanceClass);
