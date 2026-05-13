using Microsoft.AspNetCore.Components;
using Microsoft.Maui.Devices;

namespace Sunfish.Anchor.Services;

/// <summary>
/// Anchor MAUI concrete <see cref="Sunfish.Foundation.MissionSpace.ISystemRequirementsSurface"/>
/// per ADR 0063-A1.1 + W#47 Phase 2+3.
/// Carries the <see cref="NavigationManager"/> reference for
/// <see cref="Sunfish.Foundation.MissionSpace.SystemRequirementsRenderMode.PreInstallFullPage"/>
/// dispatch, an <see cref="InlineFragment"/> slot for
/// <see cref="Sunfish.Foundation.MissionSpace.SystemRequirementsRenderMode.PostInstallInlineExplanation"/>,
/// and a <see cref="RegressionBannerFragment"/> slot for the manual-trigger
/// <see cref="Sunfish.Foundation.MissionSpace.SystemRequirementsRenderMode.PostInstallRegressionBanner"/> path.
/// </summary>
/// <remarks>
/// This class uses <see cref="DeviceInfo.Current"/> which is only available under
/// the MAUI workload.  Test code must not compile this file directly — use
/// <see cref="IAnchorSystemRequirementsSurface"/> and a test fake instead.
/// </remarks>
public sealed class AnchorMauiSystemRequirementsSurface : IAnchorSystemRequirementsSurface
{
    private readonly string? _platformOverride;

    /// <summary>Production constructor — derives <see cref="Platform"/> from <see cref="DeviceInfo.Current"/>.</summary>
    public AnchorMauiSystemRequirementsSurface(NavigationManager nav)
    {
        NavigationManager = nav;
    }

    /// <summary>
    /// Test-only constructor — accepts an explicit platform string so the class
    /// can be exercised without the MAUI workload (via reflection from test assemblies).
    /// </summary>
    internal AnchorMauiSystemRequirementsSurface(NavigationManager nav, string platformOverride)
    {
        NavigationManager = nav;
        _platformOverride = platformOverride;
    }

    /// <inheritdoc/>
    public string Platform =>
        SystemRequirementsViewHelpers.GetPlatformKey(
            _platformOverride ?? DeviceInfo.Current.Platform.ToString());

    /// <inheritdoc/>
    public string? BundleId { get; set; }

    /// <inheritdoc/>
    public RenderFragment? InlineFragment { get; set; }

    /// <inheritdoc/>
    public RenderFragment? RegressionBannerFragment { get; set; }

    /// <inheritdoc/>
    public NavigationManager NavigationManager { get; }
}
