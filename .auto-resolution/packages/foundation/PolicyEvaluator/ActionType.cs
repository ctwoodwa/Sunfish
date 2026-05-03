namespace Sunfish.Foundation.PolicyEvaluator;

/// <summary>
/// The verb of a permission check at the policy-evaluator layer (e.g. <c>read</c>, <c>write</c>,
/// <c>delete</c>, or a domain-specific action like <c>sign_inspection</c>).
/// </summary>
/// <remarks>
/// <para>
/// This type is intentionally kept distinct from <see cref="Sunfish.Foundation.Capabilities.CapabilityAction"/>
/// to preserve the layered architecture: the PolicyEvaluator sits above the capability graph and
/// speaks in terms of policy-model relations, while the capability graph speaks in terms of signed
/// delegations. Two callers resolve the same underlying authorization check via different paths,
/// and keeping the verbs in separate types makes the layering explicit.
/// </para>
/// </remarks>
public readonly record struct ActionType(string Name)
{
    /// <inheritdoc />
    public override string ToString() => Name;
}
