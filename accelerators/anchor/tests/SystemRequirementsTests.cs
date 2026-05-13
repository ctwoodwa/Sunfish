using System.Reflection;
using Microsoft.AspNetCore.Components;
using Sunfish.Anchor.Services;
using Sunfish.Foundation.MissionSpace;

namespace Sunfish.Anchor.Tests;

/// <summary>
/// W#47 Phase 1 — SystemRequirements PreInstallFullPage component + view-helper
/// contract tests.
/// <para>
/// The Anchor MAUI csproj targets net11.0-{windows,maccatalyst} and requires the
/// MAUI workload; the test project avoids that workload (per the existing
/// tests.csproj convention). Component route + DI-inject surface is verified via
/// reflection on the generated assembly; the platform-key and dimension-loc-key
/// helpers are compiled directly and tested as pure logic (no MAUI API calls).
/// </para>
/// </summary>
public sealed class SystemRequirementsTests
{
    // ── Component reflection helpers ─────────────────────────────────────────

    private static Type LoadSystemRequirementsPageOrFail()
    {
        var alreadyLoaded = Array.Find(
            AppDomain.CurrentDomain.GetAssemblies(),
            a => a.GetName().Name == "Sunfish.Anchor");
        if (alreadyLoaded is { } loaded)
        {
            return loaded.GetType("Sunfish.Anchor.Components.Pages.SystemRequirements")
                ?? throw new InvalidOperationException(
                    "Sunfish.Anchor loaded but SystemRequirements type missing — route regression?");
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
            throw new InvalidOperationException($"Could not locate Anchor bin dir walking up from '{testDir}'.");

        var dlls = anchorBin
            .EnumerateFiles("Sunfish.Anchor.dll", SearchOption.AllDirectories)
            .ToList();
        if (dlls.Count == 0)
            throw new InvalidOperationException($"Sunfish.Anchor.dll not found under {anchorBin.FullName}.");

        var asm = Assembly.LoadFrom(dlls[0].FullName);
        return asm.GetType("Sunfish.Anchor.Components.Pages.SystemRequirements")
            ?? throw new InvalidOperationException($"SystemRequirements type missing from {dlls[0].FullName}.");
    }

    private static Type LoadDimensionRowOrFail()
    {
        var alreadyLoaded = Array.Find(
            AppDomain.CurrentDomain.GetAssemblies(),
            a => a.GetName().Name == "Sunfish.Anchor");
        if (alreadyLoaded is { } loaded)
        {
            return loaded.GetType("Sunfish.Anchor.Components.SystemRequirementsDimensionRow")
                ?? throw new InvalidOperationException(
                    "Sunfish.Anchor loaded but SystemRequirementsDimensionRow type missing.");
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
            throw new InvalidOperationException($"Could not locate Anchor bin dir walking up from '{testDir}'.");

        var dlls = anchorBin
            .EnumerateFiles("Sunfish.Anchor.dll", SearchOption.AllDirectories)
            .ToList();
        if (dlls.Count == 0)
            throw new InvalidOperationException($"Sunfish.Anchor.dll not found under {anchorBin.FullName}.");

        var asm = Assembly.LoadFrom(dlls[0].FullName);
        return asm.GetType("Sunfish.Anchor.Components.SystemRequirementsDimensionRow")
            ?? throw new InvalidOperationException($"SystemRequirementsDimensionRow type missing from {dlls[0].FullName}.");
    }

    // ── Test 1: route attribute ───────────────────────────────────────────────

    [Fact]
    public void SystemRequirements_RouteAttribute_RegistersBundleIdRoute()
    {
        var pageType = LoadSystemRequirementsPageOrFail();

        var routes = pageType.GetCustomAttributes<RouteAttribute>().Select(a => a.Template).ToArray();
        Assert.Contains("/system-requirements/{BundleId}", routes);
    }

    // ── Test 2: required injectable properties ───────────────────────────────

    [Fact]
    public void SystemRequirements_HasRequiredInjectableProperties()
    {
        var pageType = LoadSystemRequirementsPageOrFail();

        var injected = pageType
            .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<InjectAttribute>() is not null)
            .Select(p => p.PropertyType.Name)
            .ToHashSet(StringComparer.Ordinal);

        // Every service the page injects — missing any would be a DI resolution
        // failure at runtime when navigating to /system-requirements/{id}.
        Assert.Contains("IBundleCatalog", injected);
        Assert.Contains("IMissionEnvelopeProvider", injected);
        Assert.Contains("IMinimumSpecResolver", injected);
        Assert.Contains("IInstallForceEnableSurface", injected);
    }

    // ── Test 3: ComponentBase inheritance ────────────────────────────────────

    [Fact]
    public void SystemRequirements_DerivesFromComponentBase()
    {
        var pageType = LoadSystemRequirementsPageOrFail();

        Assert.True(typeof(ComponentBase).IsAssignableFrom(pageType),
            $"{pageType.FullName} must derive from ComponentBase to participate in the Blazor render tree.");
    }

    // ── Test 4: DimensionRow has Eval parameter ───────────────────────────────

    [Fact]
    public void SystemRequirementsDimensionRow_HasEvalParameter_OfDimensionEvaluationType()
    {
        var rowType = LoadDimensionRowOrFail();

        var evalProp = rowType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(p =>
                p.Name == "Eval" &&
                p.GetCustomAttribute<ParameterAttribute>() is not null &&
                p.PropertyType == typeof(DimensionEvaluation));

        Assert.NotNull(evalProp);
    }

    // ── Test 5: platform key derivation ──────────────────────────────────────

    [Theory]
    [InlineData("WinUI",       "windows-desktop")]
    [InlineData("MacCatalyst", "macos-desktop")]
    [InlineData("iOS",         "ios")]
    [InlineData("Android",     "android")]
    [InlineData("Unknown",     "unknown")]
    public void PlatformKeys_MapsKnownPlatformStrings_ToAdr0063Keys(string platform, string expectedKey)
    {
        var actual = SystemRequirementsViewHelpers.GetPlatformKey(platform);
        Assert.Equal(expectedKey, actual);
    }

    // ── Test 6: dimension loc-key — TrustAnchor → "Trust" ────────────────────

    [Theory]
    [InlineData(DimensionChangeKind.Hardware,      "Hardware")]
    [InlineData(DimensionChangeKind.User,          "User")]
    [InlineData(DimensionChangeKind.Regulatory,    "Regulatory")]
    [InlineData(DimensionChangeKind.Runtime,       "Runtime")]
    [InlineData(DimensionChangeKind.FormFactor,    "FormFactor")]
    [InlineData(DimensionChangeKind.Edition,       "Edition")]
    [InlineData(DimensionChangeKind.Network,       "Network")]
    [InlineData(DimensionChangeKind.TrustAnchor,   "Trust")]
    [InlineData(DimensionChangeKind.SyncState,     "SyncState")]
    [InlineData(DimensionChangeKind.VersionVector, "VersionVector")]
    public void DimensionLocKey_MapsAllDimensions_ToResx26KeyRoster(
        DimensionChangeKind dimension, string expectedFragment)
    {
        var actual = SystemRequirementsViewHelpers.GetDimensionLocKey(dimension);
        Assert.Equal(expectedFragment, actual);
    }
}
