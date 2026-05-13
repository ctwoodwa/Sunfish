using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Sunfish.Foundation.IdentityAtlas;

/// <summary>
/// DI registration helpers for the Identity Atlas foundation contracts.
/// </summary>
public static class IdentityAtlasServiceCollectionExtensions
{
    /// <summary>
    /// Registers null-object default implementations for all Identity Atlas backing-service
    /// contracts (<see cref="IKeyStore"/>, <see cref="ITrusteeRegistry"/>,
    /// <see cref="ITeamRegistry"/>).
    /// Use <see cref="Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.Replace"/>
    /// in the host registration to swap in real implementations when a wallet/keystore
    /// workstream ships.
    /// </summary>
    public static IServiceCollection AddSunfishIdentityAtlasDefaults(
        this IServiceCollection services)
    {
        services.TryAddSingleton<IKeyStore, NullKeyStore>();
        services.TryAddSingleton<ITrusteeRegistry, NullTrusteeRegistry>();
        services.TryAddSingleton<ITeamRegistry, NullTeamRegistry>();
        return services;
    }
}
