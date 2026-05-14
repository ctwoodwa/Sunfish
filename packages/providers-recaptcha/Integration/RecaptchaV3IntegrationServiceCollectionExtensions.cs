using Microsoft.Extensions.DependencyInjection;
using Sunfish.UICore.Wayfinder.Integrations;

namespace Sunfish.Providers.Recaptcha.Integration;

/// <summary>
/// DI registration for the reCAPTCHA v3 integration-config surface per
/// ADR 0067 §6.2 / W#48 Phase 3b.
/// </summary>
public static class RecaptchaV3IntegrationServiceCollectionExtensions
{
    /// <summary>
    /// Registers reCAPTCHA v3's <see cref="IIntegrationSchemaProvider"/> and
    /// <see cref="IIntegrationProviderValidator"/> into the DI container.
    /// Call this from the host's composition root alongside
    /// <c>AddSunfishIntegrationAtlasDefaults()</c>.
    /// </summary>
    public static IServiceCollection AddRecaptchaV3Integration(
        this IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddSingleton<IIntegrationSchemaProvider, RecaptchaV3IntegrationSchemaProvider>();
        services.AddSingleton<IIntegrationProviderValidator, RecaptchaV3IntegrationValidator>();
        return services;
    }
}
