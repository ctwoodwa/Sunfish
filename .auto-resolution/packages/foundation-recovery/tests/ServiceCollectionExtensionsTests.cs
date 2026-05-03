using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.Recovery;
using Sunfish.Foundation.Recovery.DependencyInjection;
using Sunfish.Kernel.Security.Crypto;

namespace Sunfish.Foundation.Recovery.Tests;

/// <summary>
/// Coverage for <see cref="ServiceCollectionExtensions.AddSunfishRecoveryCoordinator"/>.
/// Pins the registration shape so post-package-split (PR #223) refactors of the DI
/// extension surface a regression test failure rather than a runtime DI miss in a
/// host composition root.
/// </summary>
public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddSunfishRecoveryCoordinator_throws_on_null_services()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ServiceCollectionExtensions.AddSunfishRecoveryCoordinator(null!));
    }

    [Fact]
    public void AddSunfishRecoveryCoordinator_returns_same_collection_for_chaining()
    {
        var services = new ServiceCollection();
        var result = services.AddSunfishRecoveryCoordinator();
        Assert.Same(services, result);
    }

    [Fact]
    public void AddSunfishRecoveryCoordinator_registers_default_implementations()
    {
        var services = new ServiceCollection();
        services.AddSunfishRecoveryCoordinator();

        // RecoveryCoordinator constructs against IDisputerValidator + Ed25519Signer,
        // so we register those minimally to allow IRecoveryCoordinator to resolve
        // without exercising the dispute path (which is covered by RecoveryCoordinatorTests).
        services.AddSingleton<IEd25519Signer, Ed25519Signer>();
        services.AddSingleton<IDisputerValidator>(new FixedDisputerValidator(Array.Empty<byte[]>()));

        using var provider = services.BuildServiceProvider();

        Assert.IsType<SystemRecoveryClock>(provider.GetRequiredService<IRecoveryClock>());
        Assert.IsType<InMemoryRecoveryStateStore>(provider.GetRequiredService<IRecoveryStateStore>());
        Assert.IsType<RecoveryCoordinator>(provider.GetRequiredService<IRecoveryCoordinator>());
    }

    [Fact]
    public void AddSunfishRecoveryCoordinator_preserves_pre_registered_overrides()
    {
        var services = new ServiceCollection();
        var customClock = new FakeClock();
        services.AddSingleton<IRecoveryClock>(customClock);

        services.AddSunfishRecoveryCoordinator();

        using var provider = services.BuildServiceProvider();

        // TryAddSingleton in the extension must not displace the caller's earlier registration.
        Assert.Same(customClock, provider.GetRequiredService<IRecoveryClock>());
    }

    [Fact]
    public void AddSunfishRecoveryCoordinator_invokes_configure_callback_with_fresh_options()
    {
        RecoveryCoordinatorOptions? captured = null;
        var services = new ServiceCollection();

        services.AddSunfishRecoveryCoordinator(opts =>
        {
            captured = opts;
        });

        Assert.NotNull(captured);
        // The captured instance is the one registered in DI.
        using var provider = services.BuildServiceProvider();
        Assert.Same(captured, provider.GetRequiredService<RecoveryCoordinatorOptions>());
    }

    private sealed class FakeClock : IRecoveryClock
    {
        public DateTimeOffset UtcNow() => DateTimeOffset.MinValue;
    }
}
