namespace Sunfish.Foundation.PolicyEvaluator;

/// <summary>
/// The compiled policy schema: a read-only map from type name to
/// <see cref="TypeDefinition"/>. Built via <see cref="Create"/> and a fluent
/// <see cref="PolicyModelBuilder"/>.
/// </summary>
/// <remarks>
/// The <see cref="Types"/> dictionary preserves the insertion order of
/// <see cref="PolicyModelBuilder.Type(string)"/> calls (the builder backs with a
/// <see cref="Dictionary{TKey,TValue}"/>, which has documented insertion-order enumeration
/// since .NET 6 and is stable for our purposes in net10.0).
/// </remarks>
public sealed class PolicyModel
{
    /// <summary>All declared types in the model, keyed by name.</summary>
    public IReadOnlyDictionary<string, TypeDefinition> Types { get; }

    /// <summary>Constructs a model directly from a types map. Typically you use <see cref="Create"/>.</summary>
    public PolicyModel(IReadOnlyDictionary<string, TypeDefinition> types) => Types = types;

    /// <summary>Starts a new fluent builder.</summary>
    public static PolicyModelBuilder Create() => new();
}
