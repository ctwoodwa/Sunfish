using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sunfish.Federation.BlobReplication.Kubo;
using Sunfish.Federation.Common.Kubo;
using Sunfish.Foundation.Blobs;

namespace Sunfish.Federation.BlobReplication.DependencyInjection;

/// <summary>
/// DI registration helpers for the Kubo-backed <see cref="IBlobStore"/>.
/// </summary>
public static class IpfsBlobStoreExtensions
{
    /// <summary>
    /// Registers <see cref="IpfsBlobStore"/> (as <see cref="IBlobStore"/>),
    /// <see cref="KuboHttpClient"/> (as <see cref="IKuboHttpClient"/>), and
    /// <see cref="KuboHealthProbe"/> (as <see cref="IKuboHealthProbe"/>).
    /// </summary>
    /// <remarks>
    /// The underlying <see cref="HttpClient"/> is configured through
    /// <see cref="IHttpClientFactory"/> under the name <see cref="KuboHttpClient.HttpClientName"/>
    /// (<c>"sunfish-kubo"</c>) so applications can replace timeouts, resilience handlers, or
    /// instrumentation without touching this registration.
    /// </remarks>
    public static IServiceCollection AddSunfishIpfsBlobStore(
        this IServiceCollection services,
        Action<KuboBlobStoreOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }
        services.AddOptions<KuboBlobStoreOptions>();

        services.AddHttpClient(KuboHttpClient.HttpClientName, (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<KuboBlobStoreOptions>>().Value;
            client.BaseAddress = opts.RpcEndpoint;
        });

        services.AddSingleton<IKuboHttpClient>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return new KuboHttpClient(factory.CreateClient(KuboHttpClient.HttpClientName));
        });

        services.AddSingleton<IBlobStore, IpfsBlobStore>();
        services.AddSingleton<IKuboHealthProbe, KuboHealthProbe>();

        return services;
    }
}
