using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Migration;

/// <summary>
/// The active surface visible on a host with the given
/// <see cref="FormFactorProfile"/> for a workspace declaring
/// <see cref="WorkspaceDeclaredCapabilities"/>. Per ADR 0028-A5.1
/// the visible set is the intersection of the form factor's derivable
/// capabilities with the workspace's declared requirements; capabilities
/// the host CAN'T support fall into <see cref="ExcludedCapabilities"/>
/// and the records keyed on them get sequestered (P3) rather than
/// deleted, per the Invariant DLF (data-loss-vs-feature-loss) contract.
/// </summary>
public sealed record DerivedSurface
{
    [JsonPropertyName("formFactor")]
    [JsonConverter(typeof(JsonStringEnumConverter<FormFactorKind>))]
    public required FormFactorKind FormFactor { get; init; }

    /// <summary>The full capability set the workspace declared (input).</summary>
    [JsonPropertyName("workspaceDeclaredCapabilities")]
    public required HashSet<string> WorkspaceDeclaredCapabilities { get; init; }

    /// <summary>Capabilities present in BOTH the workspace declaration AND the form factor's derivable surface.</summary>
    [JsonPropertyName("includedCapabilities")]
    public required HashSet<string> IncludedCapabilities { get; init; }

    /// <summary>Capabilities the workspace declared but the form factor cannot support — drives sequestration in P3.</summary>
    [JsonPropertyName("excludedCapabilities")]
    public required HashSet<string> ExcludedCapabilities { get; init; }
}
