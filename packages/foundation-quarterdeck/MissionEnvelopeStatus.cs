using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Quarterdeck;

/// <summary>
/// Mission-Envelope projection state surfaced on the Quarterdeck per
/// ADR 0080 §2.3 rule 6. Coarse-grained projection of the underlying
/// Mission-Space evaluation; the Quarterdeck never renders raw
/// Mission-Envelope detail.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MissionEnvelopeStatus
{
    /// <summary>
    /// No envelope has been evaluated yet, or the provider could not
    /// resolve one. Distinct from <see cref="Failed"/> — represents
    /// absence of evaluation, not negative outcome.
    /// </summary>
    Unknown,

    /// <summary>
    /// Most-recent evaluation succeeded; all gated capabilities apply.
    /// </summary>
    Passed,

    /// <summary>
    /// Most-recent evaluation flagged a non-blocking warning (e.g.,
    /// approaching capability threshold). Capabilities continue to
    /// resolve normally.
    /// </summary>
    Warning,

    /// <summary>
    /// Most-recent evaluation failed; gated capabilities are denied
    /// until the envelope is restored.
    /// </summary>
    Failed,
}
