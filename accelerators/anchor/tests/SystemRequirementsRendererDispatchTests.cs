using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Sunfish.Anchor.Services;
using Sunfish.Foundation.MissionSpace;
using Xunit;

namespace Sunfish.Anchor.Tests;

/// <summary>
/// W#47 Phase 2 — AnchorMauiSystemRequirementsRenderer dispatch tests.
/// Compiled directly (renderer + IAnchorSystemRequirementsSurface are MAUI-free).
/// AnchorMauiSystemRequirementsSurface (MAUI-dependent) is exercised via
/// reflection-load of the pre-built Anchor DLL (same pattern as SystemRequirementsTests).
/// </summary>
public sealed class SystemRequirementsRendererDispatchTests
{
    private static readonly SystemRequirementsResult PassResult = new()
    {
        Overall = OverallVerdict.Pass,
        Dimensions = Array.Empty<DimensionEvaluation>(),
        EvaluatedAt = DateTimeOffset.UtcNow,
    };

    // ── Test 1: PreInstallFullPage navigates to /system-requirements/{bundleId} ──

    [Fact]
    public async Task PreInstallFullPage_NavigatesToBundleRoute()
    {
        var nav = new FakeNavigationManager();
        var surface = new FakeAnchorSurface(nav) { BundleId = "test-bundle" };
        var renderer = new AnchorMauiSystemRequirementsRenderer();

        await renderer.RenderAsync(PassResult, surface, SystemRequirementsRenderMode.PreInstallFullPage);

        Assert.Equal("/system-requirements/test-bundle", nav.LastUri);
    }

    // ── Test 2: PostInstallInlineExplanation — no navigation; fragment set ───────

    [Fact]
    public async Task PostInstallInlineExplanation_SetsInlineFragment_DoesNotNavigate()
    {
        var nav = new FakeNavigationManager();
        var surface = new FakeAnchorSurface(nav);
        var renderer = new AnchorMauiSystemRequirementsRenderer();

        await renderer.RenderAsync(PassResult, surface, SystemRequirementsRenderMode.PostInstallInlineExplanation);

        Assert.Null(nav.LastUri);
        Assert.NotNull(surface.InlineFragment);
    }

    // ── Test 3: AnchorMauiSystemRequirementsSurface.Platform — via DLL reflection ─

    [Fact]
    public void Surface_Platform_MapsDevicePlatformKey()
    {
        var surfaceType = LoadSurfaceTypeOrFail();
        var nav = new FakeNavigationManager();

        // Use internal constructor (NavigationManager nav, string platformOverride)
        var ctor = surfaceType.GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            types: new[] { typeof(NavigationManager), typeof(string) },
            modifiers: null)
            ?? throw new InvalidOperationException(
                $"{surfaceType.Name} is missing internal (NavigationManager, string) constructor.");

        var surface = (ISystemRequirementsSurface)ctor.Invoke(new object[] { nav, "WinUI" });

        // "WinUI" → SystemRequirementsViewHelpers maps → "windows-desktop"
        Assert.Equal(SystemRequirementsViewHelpers.GetPlatformKey("WinUI"), surface.Platform);
    }

    // ── Test 4: All three render modes accepted by the renderer ──────────────────

    [Theory]
    [InlineData(SystemRequirementsRenderMode.PreInstallFullPage)]
    [InlineData(SystemRequirementsRenderMode.PostInstallInlineExplanation)]
    [InlineData(SystemRequirementsRenderMode.PostInstallRegressionBanner)]
    public async Task Renderer_AcceptsAllRenderModes_WithoutThrowing(SystemRequirementsRenderMode mode)
    {
        var nav = new FakeNavigationManager();
        var surface = new FakeAnchorSurface(nav);
        var renderer = new AnchorMauiSystemRequirementsRenderer();

        await renderer.RenderAsync(PassResult, surface, mode);
        // No exception → pass.
    }

    // ── Reflection helper (mirrors SystemRequirementsTests loader) ───────────────

    private static Type LoadSurfaceTypeOrFail()
    {
        var alreadyLoaded = Array.Find(
            AppDomain.CurrentDomain.GetAssemblies(),
            a => a.GetName().Name == "Sunfish.Anchor");
        if (alreadyLoaded is { } loaded)
        {
            return loaded.GetType("Sunfish.Anchor.Services.AnchorMauiSystemRequirementsSurface")
                ?? throw new InvalidOperationException(
                    "Sunfish.Anchor loaded but AnchorMauiSystemRequirementsSurface type missing.");
        }

        var testDir = AppContext.BaseDirectory;
        var current = new DirectoryInfo(testDir);
        DirectoryInfo? anchorBin = null;
        while (current is not null && anchorBin is null)
        {
            var candidate = Path.Combine(current.FullName, "accelerators", "anchor", "bin");
            if (Directory.Exists(candidate)) { anchorBin = new DirectoryInfo(candidate); break; }
            current = current.Parent;
        }

        if (anchorBin is null)
            throw new InvalidOperationException(
                $"Could not locate Anchor bin dir walking up from '{testDir}'.");

        var dlls = anchorBin
            .EnumerateFiles("Sunfish.Anchor.dll", SearchOption.AllDirectories)
            .ToList();
        if (dlls.Count == 0)
            throw new InvalidOperationException($"Sunfish.Anchor.dll not found under {anchorBin.FullName}.");

        var asm = Assembly.LoadFrom(dlls[0].FullName);
        return asm.GetType("Sunfish.Anchor.Services.AnchorMauiSystemRequirementsSurface")
            ?? throw new InvalidOperationException(
                $"AnchorMauiSystemRequirementsSurface missing from {dlls[0].FullName}.");
    }

    // ── Test helpers ─────────────────────────────────────────────────────────────

    private sealed class FakeAnchorSurface : IAnchorSystemRequirementsSurface
    {
        private readonly FakeNavigationManager _nav;

        public FakeAnchorSurface(FakeNavigationManager nav) => _nav = nav;

        public string Platform => "test-platform";
        public string? BundleId { get; set; }
        public RenderFragment? InlineFragment { get; set; }
        public RenderFragment? RegressionBannerFragment { get; set; }
        public NavigationManager NavigationManager => _nav;
    }

    private sealed class FakeNavigationManager : NavigationManager
    {
        public string? LastUri { get; private set; }

        public FakeNavigationManager() => Initialize("http://test/", "http://test/");

        protected override void NavigateToCore(string uri, bool forceLoad)
            => LastUri = uri;

        protected override void NavigateToCore(string uri, NavigationOptions options)
            => LastUri = uri;
    }
}
