namespace Sunfish.Blocks.Inspections.Models;

/// <summary>
/// An inspector's recorded answer to a single checklist item.
/// </summary>
/// <remarks>
/// <para>
/// Response values are stringified regardless of <see cref="InspectionItemKind"/>:
/// <list type="bullet">
///   <item><description><see cref="InspectionItemKind.YesNo"/> → <c>"yes"</c> or <c>"no"</c></description></item>
///   <item><description><see cref="InspectionItemKind.PassFail"/> → <c>"pass"</c> or <c>"fail"</c></description></item>
///   <item><description><see cref="InspectionItemKind.Rating1to5"/> → <c>"1"</c>, <c>"2"</c>, <c>"3"</c>, <c>"4"</c>, or <c>"5"</c></description></item>
///   <item><description><see cref="InspectionItemKind.FreeText"/> → arbitrary inspector-supplied text</description></item>
///   <item><description><see cref="InspectionItemKind.Photo"/> → placeholder string (photo capture deferred)</description></item>
/// </list>
/// </para>
/// </remarks>
/// <param name="ItemId">The checklist item this response answers.</param>
/// <param name="ResponseValue">Stringified response value (see remarks).</param>
/// <param name="Notes">Optional free-text notes the inspector wishes to attach.</param>
public sealed record InspectionResponse(
    InspectionChecklistItemId ItemId,
    string ResponseValue,
    string? Notes);
