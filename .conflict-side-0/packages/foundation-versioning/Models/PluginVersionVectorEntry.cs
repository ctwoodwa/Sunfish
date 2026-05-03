using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Versioning;

/// <summary>
/// Per-plugin entry in a <see cref="VersionVector.Plugins"/> map (ADR 0028-A7.3
/// augmentation). Carries both the SemVer string and the required-flag so
/// rule-3 (required-plugin intersection) can evaluate symmetrically without
/// consulting bundle manifests at handshake time.
/// </summary>
/// <param name="Version">SemVer string (e.g., <c>"1.3.0"</c>).</param>
/// <param name="Required">
/// <c>true</c> when the plugin is required for federation per the local
/// node's policy. Per A7.3.2, the wire-format carries this flag so both
/// peers can intersect required-sets symmetrically (canonical for rule 3).
/// </param>
public sealed record PluginVersionVectorEntry(
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("required")] bool Required);
