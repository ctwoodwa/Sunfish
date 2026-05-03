namespace Sunfish.Foundation.PolicyEvaluator;

/// <summary>
/// Fluent entry point for defining a <see cref="PolicyModel"/>. Accumulates typed definitions and
/// emits an immutable <see cref="PolicyModel"/> on <see cref="Build"/>.
/// </summary>
public sealed class PolicyModelBuilder
{
    // Insertion-order preserving per .NET Dictionary documentation (net10.0).
    private readonly Dictionary<string, TypeDefinition> _types = new(StringComparer.Ordinal);

    /// <summary>Adds a type with no relations (e.g. for forward references).</summary>
    /// <exception cref="ArgumentException">If a type with the same name is already declared.</exception>
    public PolicyModelBuilder Type(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (_types.ContainsKey(name))
            throw new ArgumentException($"Duplicate type '{name}'", nameof(name));
        var tb = new TypeBuilder(name);
        _types[name] = tb.Build();
        return this;
    }

    /// <summary>Adds a type configured via a nested <see cref="TypeBuilder"/> callback.</summary>
    /// <remarks>If <paramref name="configure"/> is <c>null</c>, the type is added with no relations.</remarks>
    /// <exception cref="ArgumentException">If a type with the same name is already declared.</exception>
    public PolicyModelBuilder Type(string name, Action<TypeBuilder>? configure)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (_types.ContainsKey(name))
            throw new ArgumentException($"Duplicate type '{name}'", nameof(name));
        var tb = new TypeBuilder(name);
        configure?.Invoke(tb);
        _types[name] = tb.Build();
        return this;
    }

    /// <summary>Finalises the model. The returned <see cref="PolicyModel"/> is immutable.</summary>
    public PolicyModel Build() => new(_types);
}
