namespace Sunfish.Foundation.PolicyEvaluator;

/// <summary>
/// Evaluation-time context: the clock (<see cref="Now"/>) used to honour time-bound rules and
/// an optional human-readable <see cref="Purpose"/> carried through for audit trails.
/// </summary>
/// <remarks>
/// Future extensions may add request metadata (IP, session, tenant). Keep this record additive.
/// </remarks>
public sealed record ContextEnvelope(DateTimeOffset Now, string? Purpose);
