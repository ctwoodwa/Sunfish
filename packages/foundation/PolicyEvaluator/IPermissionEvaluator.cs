namespace Sunfish.Foundation.PolicyEvaluator;

/// <summary>
/// The Sunfish policy decision point: given a <see cref="Subject"/>, an <see cref="ActionType"/>,
/// a <see cref="PolicyResource"/>, and a <see cref="ContextEnvelope"/>, render a <see cref="Decision"/>.
/// </summary>
/// <remarks>
/// <para>
/// Implementations are expected to be side-effect free with respect to the policy model and tuple
/// store: a call to <see cref="EvaluateAsync"/> must not mutate either. See
/// <see cref="ReBACPolicyEvaluator"/> for the default OpenFGA-style implementation.
/// </para>
/// <para>
/// See the Sunfish Platform specification §3.5 (policy evaluator framing) for the layering rationale.
/// </para>
/// </remarks>
public interface IPermissionEvaluator
{
    /// <summary>
    /// Evaluates whether <paramref name="subject"/> may perform <paramref name="action"/> on
    /// <paramref name="resource"/> at <paramref name="context"/>.<see cref="ContextEnvelope.Now"/>.
    /// </summary>
    /// <param name="subject">The actor whose permissions are being checked.</param>
    /// <param name="action">The policy-layer verb (e.g. <c>read</c>, <c>write</c>).</param>
    /// <param name="resource">The typed resource being acted on.</param>
    /// <param name="context">Clock and purpose for the evaluation.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<Decision> EvaluateAsync(
        Subject subject,
        ActionType action,
        PolicyResource resource,
        ContextEnvelope context,
        CancellationToken ct = default);
}
