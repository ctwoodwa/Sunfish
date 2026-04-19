using Microsoft.Extensions.DependencyInjection;

using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Events;
using Sunfish.Kernel.Events.DependencyInjection;

namespace Sunfish.Kernel.EventBus.Tests;

/// <summary>
/// Coverage for <see cref="EventBusServiceCollectionExtensions.AddSunfishKernelEventBus"/>.
/// </summary>
public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddSunfishKernelEventBus_RegistersInMemoryEventBus()
    {
        // Arrange — caller is expected to register IOperationVerifier; mimic
        //           that here so the IEventBus can actually resolve.
        var services = new ServiceCollection();
        services.AddSingleton<IOperationVerifier, Ed25519Verifier>();
        services.AddSunfishKernelEventBus();

        // Act
        using var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();

        // Assert
        Assert.IsType<InMemoryEventBus>(bus);
    }

    [Fact]
    public void AddSunfishKernelEventBus_Idempotent_MultipleCallsDoNotThrow()
    {
        // TryAdd semantics — multiple calls should not stack registrations
        // or throw; the first registration wins.
        var services = new ServiceCollection();
        services.AddSingleton<IOperationVerifier, Ed25519Verifier>();

        services.AddSunfishKernelEventBus();
        services.AddSunfishKernelEventBus();
        services.AddSunfishKernelEventBus();

        using var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();
        Assert.IsType<InMemoryEventBus>(bus);

        // Only one IEventBus registration should be present thanks to TryAdd.
        var count = services.Count(sd => sd.ServiceType == typeof(IEventBus));
        Assert.Equal(1, count);
    }
}
