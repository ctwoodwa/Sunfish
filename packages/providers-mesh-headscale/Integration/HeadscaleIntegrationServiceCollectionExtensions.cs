using Microsoft.Extensions.DependencyInjection;
using Sunfish.UICore.Wayfinder.Integrations;

namespace Sunfish.Providers.Mesh.Headscale.Integration;

/// <summary>
/// DI registration for the Headscale integration-config surface per
/// ADR 0067 §6.2 / W#48 Phase 3b.
/// </summary>
public static class HeadscaleIntegrationServiceCollectionExtensions
{
    /// <summary>
    /// Registers Headscale's <see cref="IIntegrationSchemaProvider"/> and
    /// <see cref="IIntegrationProviderValidator"/> into the DI container.
    /// Call this from the host's composition root alongside
    /// <c>AddSunfishIntegrationAtlasDefaults()</c>.
    /// </summary>
    public static IServiceCollection AddHeadscaleIntegration(
        this IServiceCollection services)
    {
        // M4: IHttpClientFactory must be registered so HeadscaleIntegrationValidator can resolve it
        services.AddHttpClient();
        services.AddSingleton<IIntegrationSchemaProvider, HeadscaleIntegrationSchemaProvider>();
        services.AddSingleton<IIntegrationProviderValidator, HeadscaleIntegrationValidator>();
        return services;
    }
}
