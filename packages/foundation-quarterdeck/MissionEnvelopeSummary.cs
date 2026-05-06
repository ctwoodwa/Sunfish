using System;

namespace Sunfish.Foundation.Quarterdeck;

/// <summary>
/// Coarse-grained Mission-Envelope projection surfaced on the
/// Quarterdeck per ADR 0080 §2.3 rule 6. The data provider reads from
/// <c>IMissionEnvelopeProvider.GetCurrentAsync</c> and maps the rich
/// underlying envelope to this thin DTO; the Quarterdeck never renders
/// raw envelope detail.
/// </summary>
/// <param name="Status">Most-recent evaluation outcome.</param>
/// <param name="VersionLabel">
/// Optional human-readable version of the envelope (e.g.,
/// <c>"v3.2-rc4"</c>). Null when no envelope has been evaluated.
/// </param>
/// <param name="LastEvaluatedAt">
/// Wall-clock timestamp of the most-recent evaluation. Null when no
/// envelope has been evaluated.
/// </param>
public sealed record MissionEnvelopeSummary(
    MissionEnvelopeStatus Status,
    string? VersionLabel,
    DateTimeOffset? LastEvaluatedAt);
