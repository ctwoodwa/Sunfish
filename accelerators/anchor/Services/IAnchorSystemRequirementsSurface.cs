using Microsoft.AspNetCore.Components;
using Sunfish.Foundation.MissionSpace;

namespace Sunfish.Anchor.Services;

/// <summary>
/// Anchor-MAUI-specific extension of <see cref="ISystemRequirementsSurface"/> that
/// the <see cref="AnchorMauiSystemRequirementsRenderer"/> casts to at dispatch time.
/// Defined as a separate interface so the renderer can be compiled and tested without
/// the MAUI workload (the concrete <see cref="AnchorMauiSystemRequirementsSurface"/>
/// is MAUI-dependent; tests use a fake that implements this interface directly).
/// </summary>
public interface IAnchorSystemRequirementsSurface : ISystemRequirementsSurface
{
    /// <summary>
    /// Bundle identifier used by the renderer to navigate to
    /// <c>/system-requirements/{BundleId}</c> when
    /// <see cref="SystemRequirementsRenderMode.PreInstallFullPage"/> is requested.
    /// Set by the caller before invoking <see cref="ISystemRequirementsRenderer.RenderAsync"/>.
    /// </summary>
    string? BundleId { get; set; }

    /// <summary>
    /// Populated by the renderer when
    /// <see cref="SystemRequirementsRenderMode.PostInstallInlineExplanation"/> is requested.
    /// Consumers render this fragment inside their own layout.
    /// </summary>
    RenderFragment? InlineFragment { get; set; }

    /// <summary>
    /// Populated by the renderer when
    /// <see cref="SystemRequirementsRenderMode.PostInstallRegressionBanner"/> is requested.
    /// Shell-level consumers render this fragment (e.g., in <c>MainLayout</c>) to display
    /// a regression banner; driven by the renderer's manual-trigger path.
    /// Automatic regression detection uses <see cref="SystemRequirementsRegressionObserver"/>
    /// and its <see cref="SystemRequirementsRegressionObserver.Regressions"/> channel.
    /// </summary>
    RenderFragment? RegressionBannerFragment { get; set; }

    /// <summary>Navigation manager used for <see cref="SystemRequirementsRenderMode.PreInstallFullPage"/> dispatch.</summary>
    NavigationManager NavigationManager { get; }
}
