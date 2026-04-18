namespace Sunfish.Foundation.Assets.Common;

/// <summary>Opaque tenant identifier for multi-tenant data isolation.</summary>
public readonly record struct TenantId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator TenantId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(TenantId id) => id.Value;

    /// <summary>The default tenant used when no explicit tenant is provided.</summary>
    public static TenantId Default { get; } = new("default");
}
