namespace Sunfish.Foundation.Assets.Common;

/// <summary>Opaque identifier for an acting principal (user / service / system).</summary>
public readonly record struct ActorId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion from string.</summary>
    public static implicit operator ActorId(string value) => new(value);

    /// <summary>Implicit conversion to string.</summary>
    public static implicit operator string(ActorId id) => id.Value;

    /// <summary>The system-internal actor used when no ambient context is available.</summary>
    public static ActorId System { get; } = new("system");
}
