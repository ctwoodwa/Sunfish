using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Sunfish.Foundation.MissionSpace;

namespace Sunfish.Anchor.Services;

/// <summary>
/// Anchor MAUI concrete <see cref="ISystemRequirementsRenderer"/> per ADR 0063-A1.1
/// + W#47 Phase 2+3.
/// <list type="bullet">
/// <item><description>
/// <see cref="SystemRequirementsRenderMode.PreInstallFullPage"/> — navigates
/// <see cref="IAnchorSystemRequirementsSurface.NavigationManager"/> to
/// <c>/system-requirements/{BundleId}</c>.
/// </description></item>
/// <item><description>
/// <see cref="SystemRequirementsRenderMode.PostInstallInlineExplanation"/> — sets
/// <see cref="IAnchorSystemRequirementsSurface.InlineFragment"/> to a summary
/// fragment; does NOT navigate.
/// </description></item>
/// <item><description>
/// <see cref="SystemRequirementsRenderMode.PostInstallRegressionBanner"/> — sets
/// <see cref="IAnchorSystemRequirementsSurface.RegressionBannerFragment"/> to a
/// regression summary fragment AND publishes failing Required dimensions to the
/// optional <see cref="SystemRequirementsRegressionObserver"/> (injected at
/// construction) for the shell-level banner's channel-driven path.
/// </description></item>
/// </list>
/// </summary>
public sealed class AnchorMauiSystemRequirementsRenderer : ISystemRequirementsRenderer
{
    private readonly SystemRequirementsRegressionObserver? _observer;

    /// <summary>Production constructor — no observer (observer registered separately in DI).</summary>
    public AnchorMauiSystemRequirementsRenderer() { }

    /// <summary>
    /// Constructor with observer — used in tests and production composition root
    /// (via <c>AddAnchorSystemRequirementsRenderer</c>) to wire the
    /// <see cref="SystemRequirementsRenderMode.PostInstallRegressionBanner"/> mode.
    /// </summary>
    public AnchorMauiSystemRequirementsRenderer(SystemRequirementsRegressionObserver observer)
        => _observer = observer;

    /// <inheritdoc/>
    public ValueTask RenderAsync(
        SystemRequirementsResult result,
        ISystemRequirementsSurface surface,
        SystemRequirementsRenderMode mode,
        CancellationToken ct = default)
    {
        if (surface is not IAnchorSystemRequirementsSurface anchorSurface)
            throw new InvalidOperationException(
                $"Surface must implement {nameof(IAnchorSystemRequirementsSurface)} for Anchor MAUI rendering.");

        switch (mode)
        {
            case SystemRequirementsRenderMode.PreInstallFullPage:
                anchorSurface.NavigationManager.NavigateTo(
                    $"/system-requirements/{anchorSurface.BundleId}");
                break;

            case SystemRequirementsRenderMode.PostInstallInlineExplanation:
                anchorSurface.InlineFragment = BuildInlineFragment(result);
                break;

            case SystemRequirementsRenderMode.PostInstallRegressionBanner:
                anchorSurface.RegressionBannerFragment = BuildRegressionFragment(result);
                PublishRegressions(result);
                break;
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Returns a <see cref="RenderFragment"/> that renders
    /// <c>SystemRequirementsInlinePanel</c> for the given <paramref name="dimension"/>.
    /// Consumers compose this into their own Razor layout for the
    /// <see cref="SystemRequirementsRenderMode.PostInstallInlineExplanation"/> flow.
    /// The component type is resolved at render time to avoid a compile-time
    /// dependency on the Razor-generated class (which is not available in the
    /// MAUI-free test project).
    /// </summary>
    public RenderFragment RenderInlineFragment(
        IAnchorSystemRequirementsSurface surface,
        DimensionChangeKind dimension)
    {
        var bundleId = surface.BundleId ?? string.Empty;
        return builder =>
        {
            var panelType = ResolveInlinePanelType();
            builder.OpenComponent(0, panelType);
            builder.AddAttribute(1, "BundleId", bundleId);
            builder.AddAttribute(2, "Dimension", dimension);
            builder.CloseComponent();
        };
    }

    private void PublishRegressions(SystemRequirementsResult result)
    {
        if (_observer is null) return;
        foreach (var dim in result.Dimensions)
        {
            if (dim.Policy == DimensionPolicyKind.Required && dim.Outcome == DimensionPassFail.Fail)
                _observer.PublishRegression(dim.Dimension);
        }
    }

    private static RenderFragment BuildInlineFragment(SystemRequirementsResult result) =>
        builder =>
        {
            // Sequence numbers are literal constants per ASP0006.
            // The outer wrapper uses 0-1; each dimension repeats at the same positions
            // inside the loop — Blazor diffs by key via the foreach-index loop variable.
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "sf-inline-req-summary");
            var idx = 0;
            foreach (var dim in result.Dimensions)
            {
                builder.OpenElement(2, "div");
                builder.SetKey(idx++);
                builder.AddAttribute(3, "class",
                    dim.Outcome == DimensionPassFail.Fail
                        ? "sf-inline-req-item sf-inline-req-item--fail"
                        : "sf-inline-req-item");
                builder.AddContent(4, $"{dim.Dimension}: {dim.Outcome}");
                builder.CloseElement();
            }
            builder.CloseElement();
        };

    private static RenderFragment BuildRegressionFragment(SystemRequirementsResult result) =>
        builder =>
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "sf-regression-summary");
            var idx = 0;
            foreach (var dim in result.Dimensions)
            {
                if (dim.Policy != DimensionPolicyKind.Required || dim.Outcome != DimensionPassFail.Fail)
                    continue;
                builder.OpenElement(2, "div");
                builder.SetKey(idx++);
                builder.AddAttribute(3, "class", "sf-regression-item");
                builder.AddContent(4, dim.Dimension.ToString());
                builder.CloseElement();
            }
            builder.CloseElement();
        };

    private static Type ResolveInlinePanelType()
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType("Sunfish.Anchor.Components.SystemRequirementsInlinePanel");
            if (t is not null) return t;
        }
        throw new InvalidOperationException(
            "SystemRequirementsInlinePanel not found in any loaded assembly. " +
            "Ensure the Anchor MAUI project is compiled and loaded before calling RenderInlineFragment.");
    }
}
