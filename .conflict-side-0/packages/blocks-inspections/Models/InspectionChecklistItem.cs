namespace Sunfish.Blocks.Inspections.Models;

/// <summary>
/// A single item on an inspection checklist, defining the prompt and the kind of response expected.
/// </summary>
/// <param name="Id">Unique identifier for this checklist item.</param>
/// <param name="Prompt">Human-readable prompt shown to the inspector (e.g., "Is the smoke detector operational?").</param>
/// <param name="Kind">The type of response this item expects (see <see cref="InspectionItemKind"/>).</param>
/// <param name="Required">When <see langword="true"/>, the inspector must provide a response before the inspection can be completed.</param>
public sealed record InspectionChecklistItem(
    InspectionChecklistItemId Id,
    string Prompt,
    InspectionItemKind Kind,
    bool Required);
