using System.Collections.Generic;
using System.Linq;

namespace Sunfish.UICore.Wayfinder;

/// <summary>
/// Default in-memory <see cref="IHelmWidgetRegistry"/> per ADR 0066 §1.1.
/// Widgets are sorted at construction time by
/// <see cref="HelmWidgetMetadata.Slot"/> then
/// <see cref="HelmWidgetMetadata.OrderHint"/>; ties resolve to
/// registration order via LINQ's stable
/// <see cref="Enumerable.OrderBy{TSource, TKey}(System.Collections.Generic.IEnumerable{TSource}, System.Func{TSource, TKey})"/>.
/// </summary>
internal sealed class DefaultHelmWidgetRegistry : IHelmWidgetRegistry
{
    private readonly IReadOnlyList<IHelmWidget> _widgets;

    /// <summary>Creates a registry from the supplied widget set.</summary>
    public DefaultHelmWidgetRegistry(IEnumerable<IHelmWidget> widgets)
    {
        _widgets = widgets
            .OrderBy(w => w.Metadata.Slot)
            .ThenBy(w => w.Metadata.OrderHint)
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<IHelmWidget> Widgets => _widgets;

    /// <inheritdoc />
    public IReadOnlyList<IHelmWidget> GetSlot(HelmSlot slot) =>
        _widgets.Where(w => w.Metadata.Slot == slot).ToList();
}
