using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Versioning;

/// <summary>
/// Result of a compatibility-relation evaluation (ADR 0028-A7.1 two-phase
/// commit message). Both peers in a handshake produce a verdict
/// independently; per A7.1.3c federation proceeds iff BOTH verdicts are
/// <see cref="VerdictKind.Compatible"/>.
/// </summary>
/// <param name="Verdict">Outcome of the local node's evaluation.</param>
/// <param name="FailedRule">Set iff <paramref name="Verdict"/> is <see cref="VerdictKind.Incompatible"/>; names the specific rule that rejected.</param>
/// <param name="FailedRuleDetail">Set iff <paramref name="Verdict"/> is <see cref="VerdictKind.Incompatible"/>; carries the operator-grade detail (e.g., <c>"local kernel 1.5.0 lags peer 1.2.0 by 3 minor versions; window is 2"</c>). Localizable in a future amendment.</param>
public sealed record VersionVectorVerdict(
    [property: JsonPropertyName("verdict"), JsonConverter(typeof(JsonStringEnumConverter))] VerdictKind Verdict,
    [property: JsonPropertyName("failedRule"), JsonConverter(typeof(JsonStringEnumConverter))] FailedRule? FailedRule,
    [property: JsonPropertyName("failedRuleDetail")] string? FailedRuleDetail);
