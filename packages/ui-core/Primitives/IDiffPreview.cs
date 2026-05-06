using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Sunfish.UICore.Primitives;

/// <summary>
/// Diff-preview primitive per ADR 0077 §4 + ADR 0065 §7 (Stripe-pattern
/// diff-preview before destructive / authority-elevated operations).
/// Renderers display the diff as a side-by-side or unified view; the
/// <see cref="DiffPreviewView"/> selects the rendering style.
/// </summary>
public interface IDiffPreview
{
    /// <summary>Per-field change entries; empty list = no-op preview.</summary>
    IReadOnlyList<DiffEntry> Entries { get; }

    /// <summary>
    /// Localized human-readable summary (e.g., <c>"2 changes"</c>);
    /// rendered as the dialog heading + announced via
    /// <see cref="ILiveAnnouncer"/>.
    /// </summary>
    string Summary { get; }
}

/// <summary>
/// Single field-level change entry per ADR 0077 §4. Old + new values
/// are <see cref="object"/> so the renderer can format per-type.
/// </summary>
/// <param name="Field">Localized display name of the changed field.</param>
/// <param name="OldValue">Prior value; null when adding the field.</param>
/// <param name="NewValue">New value; null when removing the field.</param>
public sealed record DiffEntry(
    string Field,
    object? OldValue,
    object? NewValue);

/// <summary>
/// Discriminator for diff-preview presentation per ADR 0077 §4.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DiffPreviewView
{
    /// <summary>One-line per change; suitable for inline review pane.</summary>
    Compact,

    /// <summary>Full side-by-side / unified view; suitable for confirmation dialog.</summary>
    Expanded,
}
