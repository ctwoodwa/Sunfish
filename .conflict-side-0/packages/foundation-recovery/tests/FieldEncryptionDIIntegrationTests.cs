using System;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Recovery.Crypto;
using Sunfish.Foundation.Recovery.DependencyInjection;
using Sunfish.Foundation.Recovery.TenantKey;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Foundation.Recovery.Tests;

/// <summary>
/// DI integration coverage for the W#32 field-encryption substrate
/// registration (per ADR 0046-A5.7). Pins the both-or-neither factory
/// guard so a host registering exactly one of (IAuditTrail,
/// IOperationSigner) fails fast on first resolution rather than
/// silently shipping non-audited decrypts.
/// </summary>
public sealed class FieldEncryptionDIIntegrationTests
{
    private static IServiceCollection BaseServices()
    {
        var services = new ServiceCollection();
        services.AddSunfishRecoveryCoordinator();
        services.AddSingleton<ITenantKeyProvider, InMemoryTenantKeyProvider>();
        return services;
    }

    [Fact]
    public void Resolves_AuditDisabled_When_Neither_AuditTrail_Nor_Signer_Registered()
    {
        using var provider = BaseServices().BuildServiceProvider();

        var enc = provider.GetRequiredService<IFieldEncryptor>();
        var dec = provider.GetRequiredService<IFieldDecryptor>();

        Assert.IsType<TenantKeyProviderFieldEncryptor>(enc);
        Assert.IsType<TenantKeyProviderFieldDecryptor>(dec);
    }

    [Fact]
    public void Resolves_AuditEnabled_When_Both_AuditTrail_And_Signer_Registered()
    {
        var services = BaseServices();
        services.AddSingleton<IAuditTrail>(Substitute.For<IAuditTrail>());
        services.AddSingleton<IOperationSigner>(new Ed25519Signer(KeyPair.Generate()));
        using var provider = services.BuildServiceProvider();

        var dec = provider.GetRequiredService<IFieldDecryptor>();

        Assert.IsType<TenantKeyProviderFieldDecryptor>(dec);
    }

    [Fact]
    public void Throws_When_Only_AuditTrail_Registered()
    {
        var services = BaseServices();
        services.AddSingleton<IAuditTrail>(Substitute.For<IAuditTrail>());
        using var provider = services.BuildServiceProvider();

        var ex = Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<IFieldDecryptor>());

        Assert.Contains("both IAuditTrail and IOperationSigner", ex.Message);
        Assert.Contains("IAuditTrail=registered", ex.Message);
        Assert.Contains("IOperationSigner=null", ex.Message);
    }

    [Fact]
    public void Throws_When_Only_Signer_Registered()
    {
        var services = BaseServices();
        services.AddSingleton<IOperationSigner>(new Ed25519Signer(KeyPair.Generate()));
        using var provider = services.BuildServiceProvider();

        var ex = Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<IFieldDecryptor>());

        Assert.Contains("both IAuditTrail and IOperationSigner", ex.Message);
        Assert.Contains("IAuditTrail=null", ex.Message);
        Assert.Contains("IOperationSigner=registered", ex.Message);
    }

    [Fact]
    public void FieldEncryptor_Is_Singleton()
    {
        using var provider = BaseServices().BuildServiceProvider();

        var a = provider.GetRequiredService<IFieldEncryptor>();
        var b = provider.GetRequiredService<IFieldEncryptor>();

        Assert.Same(a, b);
    }

    [Fact]
    public void FieldDecryptor_Is_Singleton()
    {
        using var provider = BaseServices().BuildServiceProvider();

        var a = provider.GetRequiredService<IFieldDecryptor>();
        var b = provider.GetRequiredService<IFieldDecryptor>();

        Assert.Same(a, b);
    }
}
