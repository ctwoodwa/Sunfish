using System.Text.Json.Serialization;

namespace Sunfish.Foundation.SickBay;

/// <summary>
/// Severity discriminator for a <see cref="FirstAidHint"/> per ADR 0082
/// §4. UI consumers branch on this to pick the icon + accessible-name
/// prefix per WCAG SC 1.4.1 (Use of Color).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FirstAidLevel
{
    /// <summary>Informational hint; non-disruptive.</summary>
    Info,

    /// <summary>Caution; user should pause and read before proceeding.</summary>
    Caution,

    /// <summary>Warning; user action may have unintended consequences.</summary>
    Warning,
}
