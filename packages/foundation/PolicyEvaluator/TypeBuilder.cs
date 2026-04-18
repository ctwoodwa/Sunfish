namespace Sunfish.Foundation.PolicyEvaluator;

/// <summary>
/// Fluent builder for a single <see cref="TypeDefinition"/>. Collects named relations and emits
/// a finalised <see cref="TypeDefinition"/> on <see cref="Build"/>.
/// </summary>
public sealed class TypeBuilder
{
    private readonly string _name;
    private readonly Dictionary<string, RelationRewrite> _relations = new(StringComparer.Ordinal);

    internal TypeBuilder(string name) { _name = name; }

    /// <summary>Adds a relation with a pre-built <see cref="RelationRewrite"/>.</summary>
    /// <exception cref="ArgumentException">If a relation with the same name already exists.</exception>
    public TypeBuilder Relation(string name, RelationRewrite rewrite)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(rewrite);
        if (_relations.ContainsKey(name))
            throw new ArgumentException($"Duplicate relation '{name}' on type '{_name}'", nameof(name));
        _relations[name] = rewrite;
        return this;
    }

    /// <summary>Adds a relation configured via a nested <see cref="RelationBuilder"/> callback.</summary>
    /// <exception cref="ArgumentException">If a relation with the same name already exists.</exception>
    public TypeBuilder Relation(string name, Action<RelationBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(name);
        var rb = new RelationBuilder();
        configure?.Invoke(rb);
        return Relation(name, rb.Build());
    }

    internal TypeDefinition Build() => new(_name, _relations);
}
