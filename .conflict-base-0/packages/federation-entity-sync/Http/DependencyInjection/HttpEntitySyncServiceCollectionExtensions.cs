using Microsoft.Extensions.DependencyInjection;
using Sunfish.Federation.Common;

namespace Sunfish.Federation.EntitySync.Http.DependencyInjection;

/// <summary>
/// DI helpers for registering the HTTP+JSON sync transport.
/// </summary>
public static class HttpEntitySyncServiceCollectionExtensions
{
    /// <summary>
    /// The <see cref="IHttpClientFactory"/> named client used by <see cref="HttpSyncTransport"/>.
    /// Consumers may configure this client (base-address defaults, handlers, message inspectors,
    /// etc.) by calling <c>services.AddHttpClient(SunfishFederationHttpClientName, ...)</c>
    /// additively after <see cref="AddSunfishEntitySyncHttp"/>.
    /// </summary>
    public const string SunfishFederationHttpClientName = "sunfish-federation";

    /// <summary>
    /// Registers <see cref="HttpSyncTransport"/> as a singleton and exposes it as both
    /// <see cref="ISyncTransport"/> (for outbound <c>SendAsync</c>) and
    /// <see cref="ILocalHandlerDispatcher"/> (for the ASP.NET endpoint's inbound dispatch). The
    /// transport resolves a named <see cref="HttpClient"/> from <see cref="IHttpClientFactory"/>
    /// using <see cref="SunfishFederationHttpClientName"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This registration replaces any prior <see cref="ISyncTransport"/> and
    /// <see cref="ILocalHandlerDispatcher"/> registrations (it uses
    /// <c>TryAddSingleton</c>-equivalent semantics via descriptor replacement for the two
    /// public-facing interfaces — callers are expected to add this <em>instead of</em> the
    /// in-memory transport, not in addition).
    /// </para>
    /// <para>
    /// Callers must also map the ASP.NET endpoint to receive inbound envelopes; see
    /// <see cref="EntitySyncEndpoint.MapEntitySyncEndpoints"/>.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddSunfishEntitySyncHttp(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpClient(SunfishFederationHttpClientName);

        services.AddSingleton<HttpSyncTransport>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return new HttpSyncTransport(factory.CreateClient(SunfishFederationHttpClientName));
        });
        services.AddSingleton<ISyncTransport>(sp => sp.GetRequiredService<HttpSyncTransport>());
        services.AddSingleton<ILocalHandlerDispatcher>(sp => sp.GetRequiredService<HttpSyncTransport>());

        return services;
    }
}
