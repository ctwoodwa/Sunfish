using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Foundation.IdentityAtlas.Tests;

public class IdentityAtlasServiceCollectionExtensionsTests
{
    [Fact]
    public void AddSunfishIdentityAtlasDefaults_RegistersIKeyStore()
    {
        var services = new ServiceCollection().AddSunfishIdentityAtlasDefaults();
        var provider = services.BuildServiceProvider();
        Assert.IsType<NullKeyStore>(provider.GetRequiredService<IKeyStore>());
    }

    [Fact]
    public void AddSunfishIdentityAtlasDefaults_RegistersITrusteeRegistry()
    {
        var services = new ServiceCollection().AddSunfishIdentityAtlasDefaults();
        var provider = services.BuildServiceProvider();
        Assert.IsType<NullTrusteeRegistry>(provider.GetRequiredService<ITrusteeRegistry>());
    }

    [Fact]
    public void AddSunfishIdentityAtlasDefaults_RegistersITeamRegistry()
    {
        var services = new ServiceCollection().AddSunfishIdentityAtlasDefaults();
        var provider = services.BuildServiceProvider();
        Assert.IsType<NullTeamRegistry>(provider.GetRequiredService<ITeamRegistry>());
    }

    [Fact]
    public void AddSunfishIdentityAtlasDefaults_IsTryAdd_DoesNotOverrideExisting()
    {
        var custom = new CustomKeyStore();
        var services = new ServiceCollection();
        services.AddSingleton<IKeyStore>(custom);
        services.AddSunfishIdentityAtlasDefaults();
        var provider = services.BuildServiceProvider();
        Assert.Same(custom, provider.GetRequiredService<IKeyStore>());
    }

    private sealed class CustomKeyStore : IKeyStore
    {
        public ValueTask<IdentityProfile?> GetIdentityProfileAsync(
            TenantId tenant,
            ActorId actor,
            CancellationToken ct = default) =>
            ValueTask.FromResult<IdentityProfile?>(null);

        public ValueTask<KeyInfo?> GetCurrentKeyInfoAsync(
            TenantId tenant,
            ActorId actor,
            CancellationToken ct = default) =>
            ValueTask.FromResult<KeyInfo?>(null);
    }
}
