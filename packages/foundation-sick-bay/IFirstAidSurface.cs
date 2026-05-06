using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Foundation.SickBay;

/// <summary>
/// Contextual help surface per ADR 0082 §4. UI consumers query this
/// keyed by surface identifier (kebab-case) to fetch the
/// <see cref="FirstAidHint"/> entries to render alongside the surface.
/// </summary>
public interface IFirstAidSurface
{
    /// <summary>
    /// Returns contextual hints for <paramref name="surfaceKey"/>.
    /// Unknown keys return an empty list (NOT throw) per §4 graceful-
    /// degradation contract.
    /// </summary>
    /// <param name="surfaceKey">Kebab-case surface identifier (e.g., <c>"sick-bay.medevac"</c>).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<FirstAidHint>> GetContextualHintsAsync(
        string surfaceKey,
        CancellationToken ct = default);
}
