using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Foundation.MissionSpace;

/// <summary>
/// Per ADR 0063-A1.1 — per-adapter UX surface for rendering a
/// <see cref="SystemRequirementsResult"/> to the operator. Phase 1 substrate
/// ships the interface only; concrete renderers are per-adapter Stage 06
/// work products (W#42+ — Anchor MAUI Razor; W#43+ — Bridge React;
/// W#44+ — iOS SwiftUI).
/// </summary>
/// <remarks>
/// <para>
/// The substrate carries no rendering primitives — the per-adapter
/// implementation owns layout, copy, and platform conventions. The
/// substrate's responsibility is the data shape the renderer consumes
/// (<see cref="SystemRequirementsResult"/> + <see cref="SystemRequirementsRenderMode"/>)
/// and the platform-surface abstraction
/// (<see cref="ISystemRequirementsSurface"/>) the renderer mounts onto.
/// </para>
/// <para>
/// Render modes per A1.1:
/// <list type="bullet">
/// <item><description><see cref="SystemRequirementsRenderMode.PreInstallFullPage"/> — full-page UX before installer commits.</description></item>
/// <item><description><see cref="SystemRequirementsRenderMode.PostInstallInlineExplanation"/> — inline explanation panel post-install (e.g., on settings page).</description></item>
/// <item><description><see cref="SystemRequirementsRenderMode.PostInstallRegressionBanner"/> — banner that appears when a previously-passing dimension regresses.</description></item>
/// </list>
/// </para>
/// </remarks>
public interface ISystemRequirementsRenderer
{
    /// <summary>Renders <paramref name="result"/> on <paramref name="surface"/> using the supplied <paramref name="mode"/>.</summary>
    /// <param name="result">Resolver verdict + per-dimension evaluations.</param>
    /// <param name="surface">Per-adapter platform-surface abstraction.</param>
    /// <param name="mode">Render mode hint (see <see cref="SystemRequirementsRenderMode"/>).</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask RenderAsync(
        SystemRequirementsResult result,
        ISystemRequirementsSurface surface,
        SystemRequirementsRenderMode mode,
        CancellationToken ct = default);
}

/// <summary>
/// Per A1.1 — opaque platform-surface handle the per-adapter renderer
/// mounts onto. Phase 1 ships the marker interface; per-adapter
/// implementations (W#42+) define their own concrete surface types
/// (e.g., MAUI <c>ContentPage</c>, React DOM root, SwiftUI view tree).
/// </summary>
public interface ISystemRequirementsSurface
{
    /// <summary>The platform key (e.g., <c>"ios"</c>, <c>"android"</c>, <c>"windows-desktop"</c>) the surface targets.</summary>
    string Platform { get; }
}
