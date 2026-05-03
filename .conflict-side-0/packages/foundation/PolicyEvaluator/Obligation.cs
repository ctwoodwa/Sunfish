namespace Sunfish.Foundation.PolicyEvaluator;

/// <summary>
/// A side-condition attached to a <see cref="Decision"/>: the evaluator permits the request
/// <i>only if</i> the caller also satisfies the named obligation (e.g. <c>mfa</c>, <c>reason_logged</c>).
/// </summary>
/// <remarks>
/// Phase B Task 6 produces obligations only as the <see cref="Decision"/> surface; discharging
/// them is the caller's responsibility and is not implemented here.
/// </remarks>
public sealed record Obligation(string Name, IReadOnlyDictionary<string, string> Parameters);
