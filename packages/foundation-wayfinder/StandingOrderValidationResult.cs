using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// Aggregate result of running the <see cref="IStandingOrderValidator"/> chain
/// on a single <see cref="StandingOrder"/>. Per ADR 0065 §3.
/// </summary>
/// <param name="Accepted">True when no issue had <see cref="StandingOrderValidationSeverity.Block"/> severity (and no <see cref="StandingOrderValidationSeverity.Error"/> reduced to Block via the sub-admin rule).</param>
/// <param name="Issues">All issues surfaced by the chain, in declaration order. Empty when the order passes cleanly.</param>
public sealed record StandingOrderValidationResult(
    [property: JsonPropertyName("accepted")] bool Accepted,
    [property: JsonPropertyName("issues")] IReadOnlyList<StandingOrderValidationIssue> Issues);
