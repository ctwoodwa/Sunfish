namespace Sunfish.Foundation.PolicyEvaluator;

/// <summary>
/// The policy model's description of a single resource type: its name, plus the named relations
/// defined on it, each pointing at a <see cref="RelationRewrite"/> expression.
/// </summary>
/// <remarks>
/// Relations are typically named after user-facing roles (e.g. <c>owner</c>, <c>editor</c>, <c>viewer</c>)
/// or permissions (e.g. <c>can_read</c>, <c>can_write</c>). The OpenFGA convention is to derive the
/// latter from the former via <see cref="RelationRewrite.ComputedUserset"/> or <see cref="RelationRewrite.Union"/>.
/// </remarks>
public sealed record TypeDefinition(string Name, IReadOnlyDictionary<string, RelationRewrite> Relations);
