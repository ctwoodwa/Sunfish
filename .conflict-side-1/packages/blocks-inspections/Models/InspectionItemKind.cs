namespace Sunfish.Blocks.Inspections.Models;

/// <summary>
/// Describes the response format expected for a checklist item.
/// </summary>
public enum InspectionItemKind
{
    /// <summary>Binary yes/no answer. Stored as <c>"yes"</c> or <c>"no"</c>.</summary>
    YesNo,

    /// <summary>Pass/fail answer. Stored as <c>"pass"</c> or <c>"fail"</c>.</summary>
    PassFail,

    /// <summary>Numeric rating from 1 to 5. Stored as <c>"1"</c>–<c>"5"</c>.</summary>
    Rating1to5,

    /// <summary>Open-ended text response. Stored as the raw string value entered by the inspector.</summary>
    FreeText,

    /// <summary>
    /// Photo attachment placeholder (reserved for a future mobile capture pass).
    /// In this pass the <c>ResponseValue</c> stores a placeholder string such as
    /// <c>"[photo-deferred]"</c>. Photo upload and blob-store integration are not implemented.
    /// </summary>
    Photo,
}
