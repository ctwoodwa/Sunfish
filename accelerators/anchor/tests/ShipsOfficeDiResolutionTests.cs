using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sunfish.Blocks.ShipsOffice;
using Sunfish.Blocks.Leases.Services;
using Sunfish.Blocks.Maintenance.Services;
using Sunfish.Foundation.Catalog.Bundles;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.MissionSpace;
using Sunfish.Foundation.Ship.Common;
using Sunfish.Foundation.Assets.Audit;
using Sunfish.Kernel.Audit;

namespace Sunfish.Anchor.Tests;

/// <summary>
/// W#55 Phase 4 — DI registration smoke tests for
/// <see cref="ShipsOfficeServiceCollectionExtensions.AddSunfishShipsOfficeDefaults"/>.
/// Mirrors the pattern from <c>MauiProgramTransportRegistrationTests</c>:
/// compose a <see cref="ServiceCollection"/>, stub the cross-package
/// dependencies, call the extension method, and verify the services resolve.
/// We cannot boot <c>MauiProgram.CreateMauiApp()</c> in tests (MAUI requires
/// a device-class graphics context), so this is a composition-graph check only.
/// </summary>
public sealed class ShipsOfficeDiResolutionTests
{
    private static ServiceProvider BuildProviderWithDefaults()
    {
        var services = new ServiceCollection();

        // Required cross-package stubs so IShipsOfficeDataProvider resolves.
        services.AddSingleton(Substitute.For<IBundleCatalog>());
        services.AddSingleton(Substitute.For<ILeaseService>());
        services.AddSingleton(Substitute.For<ILeaseDocumentVersionLog>());
        services.AddSingleton(Substitute.For<IMaintenanceService>());
        services.AddSingleton(Substitute.For<IW9DocumentService>());
        services.AddSingleton(Substitute.For<IMissionEnvelopeProvider>());

        // Required for IShipsOfficeCommandService.
        services.AddSingleton(Substitute.For<IPermissionResolver>());
        services.AddSingleton(Substitute.For<IActorPrincipalResolver>());
        services.AddSingleton(Substitute.For<IAuditContextProvider>());
        services.AddSingleton(Substitute.For<IAuditTrail>());
        services.AddSingleton(Substitute.For<IOperationSigner>());

        services.AddSunfishShipsOfficeDefaults();

        return services.BuildServiceProvider();
    }

    [Fact]
    public void Anchor_DI_resolves_IShipsOfficeDataProvider()
    {
        using var provider = BuildProviderWithDefaults();
        var service = provider.GetService<IShipsOfficeDataProvider>();
        Assert.NotNull(service);
    }

    [Fact]
    public void Anchor_DI_resolves_IShipsOfficeCommandService()
    {
        using var provider = BuildProviderWithDefaults();
        var service = provider.GetService<IShipsOfficeCommandService>();
        Assert.NotNull(service);
    }

    [Fact]
    public void Ships_Office_demo_page_exists_in_Anchor_assembly()
    {
        // Verify the ShipsOfficePage.razor component type is present in the
        // compiled Anchor assembly. We use reflection rather than loading the
        // full MAUI app (which requires a device-class graphics context).
        // If this test fails, the component was removed or renamed without
        // updating this assertion — treat as a route-regression guard.
        var testDir = AppContext.BaseDirectory;
        var current = new DirectoryInfo(testDir);
        DirectoryInfo? anchorBin = null;
        while (current is not null && anchorBin is null)
        {
            var candidate = Path.Combine(current.FullName, "accelerators", "anchor", "bin");
            if (Directory.Exists(candidate)) { anchorBin = new DirectoryInfo(candidate); break; }
            current = current.Parent;
        }

        // If the Anchor assembly hasn't been built yet, skip rather than fail —
        // CI always builds Anchor before running tests; locally a dev may not have.
        if (anchorBin is null) return;
        var dlls = anchorBin
            .EnumerateFiles("Sunfish.Anchor.dll", SearchOption.AllDirectories)
            .ToList();
        if (dlls.Count == 0) return;

        var asm = System.Reflection.Assembly.LoadFrom(dlls[0].FullName);
        var pageType = asm.GetType("Sunfish.Anchor.Components.Pages.ShipsOfficePage");
        Assert.NotNull(pageType);
    }
}
