using System.Collections.Generic;

namespace Sunfish.UICore.Wayfinder;

/// <summary>
/// Composed registry of <see cref="IHelmWidget"/> instances per ADR
/// 0066 §1.1. The Helm shell pulls the registered set + filters by
/// slot at render time.
/// </summary>
public interface IHelmWidgetRegistry
{
    /// <summary>
    /// Every registered widget, ordered by
    /// <see cref="HelmWidgetMetadata.Slot"/> first then
    /// <see cref="HelmWidgetMetadata.OrderHint"/>.
    /// </summary>
    IReadOnlyList<IHelmWidget> Widgets { get; }

    /// <summary>
    /// Widgets registered to <paramref name="slot"/>, sorted by
    /// <see cref="HelmWidgetMetadata.OrderHint"/> ascending. Ties
    /// resolve to registration order (the
    /// <see cref="DefaultHelmWidgetRegistry"/> uses a stable sort).
    /// </summary>
    IReadOnlyList<IHelmWidget> GetSlot(HelmSlot slot);
}
