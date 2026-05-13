using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sunfish.Blocks.SickBay;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.MissionSpace;
using Sunfish.Foundation.SickBay;
using Sunfish.Kernel.Audit;

namespace Sunfish.Anchor.Tests;

/// <summary>
/// W#54 Phase 4 — DI registration smoke tests for
/// <see cref="SickBayServiceCollectionExtensions.AddSunfishSickBayDefaults"/>.
/// Mirrors the pattern from <see cref="ShipsOfficeDiResolutionTests"/>:
/// compose a <see cref="ServiceCollection"/>, stub the cross-package
/// dependencies, call the extension method, and verify the services resolve.
/// We cannot boot <c>MauiProgram.CreateMauiApp()</c> in tests (MAUI requires
/// a device-class graphics context), so this is a composition-graph check only.
/// </summary>
public sealed class SickBayDiResolutionTests
{
    private static ServiceProvider BuildProviderWithDefaults()
    {
        var services = new ServiceCollection();

        // Required for SickBayCommandService + MedevacServiceImpl.
        services.AddSingleton(Substitute.For<IAuditTrail>());
        services.AddSingleton(Substitute.For<IOperationSigner>());

        // IMissionEnvelopeProvider is optional in SickBayDataProvider
        // but stub it so integration tests can wire a real provider later.
        services.AddSingleton(Substitute.For<IMissionEnvelopeProvider>());

        // RegisterNoopKeyRotationScheduler=true so IKeyRotationScheduler
        // resolves via NoopKeyRotationScheduler; required by SickBayCommandService.
        services.AddSunfishSickBayDefaults(opts =>
        {
            opts.RegisterNoopKeyRotationScheduler = true;
            opts.RegisterPurpose("ssn", "Social Security Number");
        });

        return services.BuildServiceProvider();
    }

    [Fact]
    public void Anchor_DI_resolves_ISickBayDataProvider()
    {
        using var provider = BuildProviderWithDefaults();
        var service = provider.GetService<ISickBayDataProvider>();
        Assert.NotNull(service);
    }

    [Fact]
    public void Anchor_DI_resolves_IMedevacService()
    {
        using var provider = BuildProviderWithDefaults();
        var service = provider.GetService<IMedevacService>();
        Assert.NotNull(service);
    }

    [Fact]
    public void Sick_Bay_page_exists_in_Anchor_assembly()
    {
        // Verify the SickBayPage.razor component type is present in the
        // compiled Anchor assembly. We use reflection rather than loading the
        // full MAUI app (which requires a device-class graphics context).
        // If this test fails, the component was removed or renamed without
        // updating this assertion — treat as a route-regression guard.
        // Mirrors the pattern from ShipsOfficeDiResolutionTests.
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
        var pageType = asm.GetType("Sunfish.Anchor.Components.Pages.SickBayPage");
        Assert.NotNull(pageType);
    }
}
