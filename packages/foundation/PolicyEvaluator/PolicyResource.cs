namespace Sunfish.Foundation.PolicyEvaluator;

/// <summary>
/// A policy-layer resource: a typed, identifiable object in the policy model
/// (e.g. <c>property:42</c>, <c>inspection:2026-04-17</c>, <c>inspection_firm:acmeFirm</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this type exists:</b> The capability graph's
/// <see cref="Sunfish.Foundation.Capabilities.Resource"/> is a flat opaque string (<c>Id</c> only) —
/// intentionally minimal because the graph treats resources as black boxes. The policy evaluator,
/// however, needs to look up a resource's type in the policy model to resolve its relations.
/// Rather than widening the capability graph's contract, we introduce <c>PolicyResource</c>
/// at the policy-evaluator layer with an explicit <see cref="TypeName"/> and <see cref="Id"/>,
/// and bridge downward via <see cref="ToCapabilityResource"/>.
/// </para>
/// <para>
/// The conventional string form is <c>"{TypeName}:{Id}"</c> — matching how OpenFGA renders
/// objects — which is exactly what <see cref="ToCapabilityResource"/> produces.
/// </para>
/// </remarks>
public sealed record PolicyResource(string TypeName, string Id)
{
    /// <summary>
    /// Projects this policy resource onto a <see cref="Sunfish.Foundation.Capabilities.Resource"/>
    /// with the composite identifier <c>"{TypeName}:{Id}"</c>. Use when calling into the
    /// capability graph from within the policy-evaluator layer.
    /// </summary>
    public Sunfish.Foundation.Capabilities.Resource ToCapabilityResource()
        => new($"{TypeName}:{Id}");

    /// <inheritdoc />
    public override string ToString() => $"{TypeName}:{Id}";
}
