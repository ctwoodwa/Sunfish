using System.Text.Json.Serialization;

namespace Sunfish.UICore.Primitives;

/// <summary>
/// Form-control discriminator per ADR 0077 §4 First-Aid form-control
/// contract. Renderers map each kind to the platform-canonical input:
/// HTML <c>&lt;input type="..."&gt;</c> for Blazor/React; native
/// platform inputs for MAUI.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FormControlKind
{
    /// <summary>Free-form text input.</summary>
    Text,

    /// <summary>Numeric input — applies numeric validation.</summary>
    Number,

    /// <summary>Closed-set selection (drop-down / list).</summary>
    Select,

    /// <summary>Boolean toggle (checkbox).</summary>
    Checkbox,

    /// <summary>Radio button (mutex within a group).</summary>
    Radio,

    /// <summary>Multi-line text area.</summary>
    TextArea,

    /// <summary>Date input (platform-canonical date picker).</summary>
    Date,

    /// <summary>Read-only output (value displayed; not user-editable).</summary>
    ReadOnly,
}
