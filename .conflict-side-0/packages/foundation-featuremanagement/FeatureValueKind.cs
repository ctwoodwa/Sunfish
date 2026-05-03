using System.Text.Json.Serialization;

namespace Sunfish.Foundation.FeatureManagement;

/// <summary>Declared value-type of a feature, used for catalog validation.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FeatureValueKind
{
    /// <summary>Boolean flag (true/false).</summary>
    Boolean = 0,

    /// <summary>String value (enum-like choice, free-form text).</summary>
    String = 1,

    /// <summary>Signed 32-bit integer.</summary>
    Integer = 2,

    /// <summary>Decimal value.</summary>
    Decimal = 3,

    /// <summary>Arbitrary JSON document (serialized in the raw value).</summary>
    Json = 4,
}
