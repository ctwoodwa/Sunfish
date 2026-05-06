using System.Text.Json.Serialization;

namespace Sunfish.UICore.FirstAid;

/// <summary>
/// Discriminator for where on the surface First-Aid contextual help
/// renders per ADR 0077 §4. WCAG SC 3.2.6 (Consistent Help) requires
/// help to live in a consistent location across surfaces; this enum
/// constrains the option set.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HelpLocation
{
    /// <summary>Top of the surface (e.g., banner / header).</summary>
    TopOfSurface,

    /// <summary>Sidebar / right-rail panel.</summary>
    Sidebar,

    /// <summary>Floating help button (typically corner-anchored).</summary>
    HelpButton,

    /// <summary>Inline alongside the relevant control.</summary>
    Inline,
}
