using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.UICore.Wayfinder.Integrations;

namespace Sunfish.Blocks.Integrations;

/// <summary>
/// DI extension methods for registering the full integration-atlas stack
/// (ADR 0067 §6.1).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the full integration-atlas stack: contracts (via
    /// <c>AddSunfishIntegrationAtlas()</c> from <c>Sunfish.UICore</c>) plus the
    /// reference <see cref="DefaultIntegrationAtlasProvider"/> implementation and
    /// the <see cref="InMemoryIntegrationAtlasProvider"/> test double.
    /// </summary>
    /// <remarks>
    /// <para>Requires <c>AddSunfishRecoveryCoordinator()</c> to have been called
    /// first — enforced by the inner <c>AddSunfishIntegrationAtlas()</c> guard
    /// that checks for <see cref="Sunfish.Foundation.Crypto.IDecryptCapabilityProvider"/>.</para>
    /// <para>Use this overload for production and integration-test hosts. Use
    /// <see cref="InMemoryIntegrationAtlasProvider"/> directly for unit-test
    /// scenarios that do not need encryption or audit emission.</para>
    /// </remarks>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The same <paramref name="services"/> for fluent chaining.</returns>
    public static IServiceCollection AddSunfishIntegrationAtlasDefaults(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Registers IValidationStatusStore + enforces AddSunfishRecoveryCoordinator guard.
        services.AddSunfishIntegrationAtlas();

        services.TryAddSingleton<IIntegrationAtlasProvider, DefaultIntegrationAtlasProvider>();

        return services;
    }
}
