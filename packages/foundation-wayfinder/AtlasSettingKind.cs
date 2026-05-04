using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// Type discriminator for an <see cref="AtlasSettingSnapshot"/>'s value, used
/// by the Atlas form view to pick a renderer (string editor, number stepper,
/// boolean toggle, JSON tree, secret reveal-on-tap, etc.). Per ADR 0065 §5.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AtlasSettingKind
{
    /// <summary>String value.</summary>
    String,

    /// <summary>Numeric value (integer or floating-point).</summary>
    Number,

    /// <summary>Boolean toggle.</summary>
    Boolean,

    /// <summary>One of a fixed set of choices; the schema enumerates the allowed values.</summary>
    Enum,

    /// <summary>Structured JSON value (object or array); the schema describes the shape. The Atlas form view renders both with the JSON tree renderer.</summary>
    JsonObject,

    /// <summary>Secret value (token, key, password). UX must reveal-on-tap and never log.</summary>
    Secret,
}
