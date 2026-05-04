using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// A single issue surfaced by a validator in the chain. Per ADR 0065 §3.
/// </summary>
/// <param name="Severity">Severity classification; see <see cref="StandingOrderValidationSeverity"/>.</param>
/// <param name="Path">Dotted path within the Standing Order's scope (matches <see cref="StandingOrderTriple.Path"/> when the issue is triple-localized).</param>
/// <param name="Message">Human-readable description of the issue. Localization is the consumer's responsibility.</param>
/// <param name="RemediationHint">Optional operator-facing hint pointing at how to fix the issue. Null when the issue is informational only.</param>
public sealed record StandingOrderValidationIssue(
    [property: JsonPropertyName("severity")] StandingOrderValidationSeverity Severity,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("remediationHint")] string? RemediationHint);
