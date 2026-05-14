using System;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Recovery.Crypto;
using Sunfish.UICore.Wayfinder.Integrations;
using Xunit;

namespace Sunfish.Blocks.Integrations.Tests;

/// <summary>
/// ADR 0067 §6.1.1 — IFieldDecryptor must NOT be resolvable from a container
/// built via <c>AddSunfishIntegrationAtlas()</c> alone (contracts-only extension).
/// </summary>
public sealed class IFieldDecryptorScopeIsolationTests
{
    [Fact]
    public void IFieldDecryptor_NotRegistered_ByContractsOnlyExtension()
    {
        // AddSunfishIntegrationAtlas() registers contracts only.
        // IFieldDecryptor (from foundation-recovery) must NOT be in that container.
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IDecryptCapabilityProvider>());
        services.AddSunfishIntegrationAtlas();

        using var sp = services.BuildServiceProvider();

        var decryptor = sp.GetService<IFieldDecryptor>();
        Assert.Null(decryptor);
    }

    [Fact]
    public void IIntegrationAtlasProvider_NotRegistered_ByContractsOnlyExtension()
    {
        // AddSunfishIntegrationAtlas() must NOT register DefaultIntegrationAtlasProvider.
        // That registration happens only in AddSunfishIntegrationAtlasDefaults().
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IDecryptCapabilityProvider>());
        services.AddSunfishIntegrationAtlas();

        using var sp = services.BuildServiceProvider();

        var provider = sp.GetService<IIntegrationAtlasProvider>();
        Assert.Null(provider);
    }

    [Fact]
    public void AddSunfishIntegrationAtlas_WithoutDecryptCapabilityProvider_ThrowsInvalidOperation()
    {
        // The guard enforces that AddSunfishRecoveryCoordinator() was called first,
        // evidenced by IDecryptCapabilityProvider in the container.
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddSunfishIntegrationAtlas());

        Assert.Contains("AddSunfishRecoveryCoordinator", ex.Message);
    }
}
