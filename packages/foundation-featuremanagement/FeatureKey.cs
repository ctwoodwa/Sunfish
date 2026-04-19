namespace Sunfish.Foundation.FeatureManagement;

/// <summary>
/// Case-sensitive identifier for a feature. Compared by ordinal string equality.
/// </summary>
public readonly record struct FeatureKey(string Value)
{
    /// <summary>Creates a key, rejecting null, empty, or whitespace-only values.</summary>
    public static FeatureKey Of(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Feature key must be non-empty.", nameof(value));
        }

        return new FeatureKey(value);
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>Implicit conversion to string for logging and dictionary keys.</summary>
    public static implicit operator string(FeatureKey key) => key.Value;
}
