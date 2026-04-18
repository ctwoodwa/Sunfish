namespace Sunfish.Foundation.Assets.Common;

/// <summary>
/// Opaque schema identifier. Phase A treats it as a string; the schema registry
/// (spec §3.4) will back it with a real type registry in a later phase.
/// </summary>
public readonly record struct SchemaId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string for ergonomic literal usage.</summary>
    public static implicit operator SchemaId(string value) => new(value);

    /// <summary>Implicit conversion to string for log/DB serialization.</summary>
    public static implicit operator string(SchemaId id) => id.Value;
}
