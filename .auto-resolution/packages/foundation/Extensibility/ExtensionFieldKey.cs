namespace Sunfish.Foundation.Extensibility;

/// <summary>
/// Case-sensitive identifier for an extension field on an entity.
/// Compared by ordinal string equality so callers hold a stable key.
/// </summary>
public readonly record struct ExtensionFieldKey(string Value)
{
    /// <summary>
    /// Creates a key, rejecting null, empty, or whitespace-only values.
    /// </summary>
    public static ExtensionFieldKey Of(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Extension field key must be non-empty.", nameof(value));
        }

        return new ExtensionFieldKey(value);
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion to string for dictionary / logging use.</summary>
    public static implicit operator string(ExtensionFieldKey key) => key.Value;
}
