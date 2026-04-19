using System.Globalization;

namespace Sunfish.Foundation.FeatureManagement;

/// <summary>
/// Raw string value for a feature with typed accessors. The catalog's
/// <see cref="FeatureSpec.Kind"/> declares the intended type; accessors
/// throw when the raw value cannot be parsed as that type.
/// </summary>
public sealed record FeatureValue
{
    /// <summary>The raw string form.</summary>
    public required string Raw { get; init; }

    /// <summary>Parses as a boolean. Throws if the raw value is not a valid boolean literal.</summary>
    public bool AsBoolean()
    {
        if (bool.TryParse(Raw, out var value))
        {
            return value;
        }

        throw new InvalidOperationException($"Feature value '{Raw}' is not a boolean.");
    }

    /// <summary>Parses as a signed 32-bit integer in the invariant culture.</summary>
    public int AsInt32()
    {
        if (int.TryParse(Raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        throw new InvalidOperationException($"Feature value '{Raw}' is not a 32-bit integer.");
    }

    /// <summary>Parses as a decimal in the invariant culture.</summary>
    public decimal AsDecimal()
    {
        if (decimal.TryParse(Raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        throw new InvalidOperationException($"Feature value '{Raw}' is not a decimal.");
    }

    /// <summary>Returns the raw string value.</summary>
    public string AsString() => Raw;

    /// <summary>Factory: wraps a boolean as <c>"true"</c> / <c>"false"</c>.</summary>
    public static FeatureValue Of(bool value) => new() { Raw = value ? "true" : "false" };

    /// <summary>Factory: wraps an integer in the invariant culture.</summary>
    public static FeatureValue Of(int value) => new() { Raw = value.ToString(CultureInfo.InvariantCulture) };

    /// <summary>Factory: wraps a decimal in the invariant culture.</summary>
    public static FeatureValue Of(decimal value) => new() { Raw = value.ToString(CultureInfo.InvariantCulture) };

    /// <summary>Factory: wraps a string verbatim.</summary>
    public static FeatureValue Of(string value) => new() { Raw = value };
}
