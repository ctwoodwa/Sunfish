using System.Collections.Concurrent;
using System.Net.Http.Json;
using Sunfish.Federation.Common;

namespace Sunfish.Federation.EntitySync.Http;

/// <summary>
/// HTTP+JSON implementation of <see cref="ISyncTransport"/>. Outbound envelopes are POSTed as
/// <see cref="SyncEnvelopeDto"/> JSON to <c>{peer.Endpoint}/.well-known/sunfish/federation/entity-sync</c>;
/// the response body is deserialized and converted back into a <see cref="SyncEnvelope"/>.
/// Inbound envelopes are delivered by the ASP.NET Core endpoint (see <see cref="EntitySyncEndpoint"/>),
/// which calls <see cref="DispatchAsync"/> on this transport to route to the locally-registered handler.
/// </summary>
/// <remarks>
/// <para>
/// This type doubles as <see cref="ILocalHandlerDispatcher"/> so the HTTP endpoint and a typical
/// in-process syncer share one handler routing map: the syncer calls <see cref="RegisterHandler"/>
/// on its injected <see cref="ISyncTransport"/>, and the endpoint calls <see cref="DispatchAsync"/>
/// on the same instance (both resolved through DI as the same singleton — see
/// <see cref="DependencyInjection.HttpEntitySyncServiceCollectionExtensions"/>).
/// </para>
/// <para>
/// Register a single <see cref="HttpSyncTransport"/> as a singleton per process. Outbound
/// <see cref="HttpClient"/> calls are routed through <see cref="IHttpClientFactory"/> to pick up
/// handler lifetime management.
/// </para>
/// </remarks>
public sealed class HttpSyncTransport : ISyncTransport, ILocalHandlerDispatcher
{
    /// <summary>
    /// The HTTP path mounted by <see cref="EntitySyncEndpoint.MapEntitySyncEndpoints"/>. Clients
    /// POST envelopes here; the path is namespaced under <c>/.well-known/</c> so federation
    /// endpoints do not collide with application routes.
    /// </summary>
    public const string EndpointPath = "/.well-known/sunfish/federation/entity-sync";

    private readonly HttpClient _http;
    private readonly ConcurrentDictionary<PeerId, Func<SyncEnvelope, ValueTask<SyncEnvelope>>> _handlers = new();

    /// <summary>Creates a transport backed by <paramref name="http"/>.</summary>
    public HttpSyncTransport(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    /// <inheritdoc />
    public async ValueTask<SyncEnvelope> SendAsync(PeerDescriptor target, SyncEnvelope envelope, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(envelope);

        var url = new Uri(target.Endpoint, EndpointPath);
        var dto = SyncEnvelopeDto.From(envelope);

        using var response = await _http.PostAsJsonAsync(url, dto, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var replyDto = await response.Content
            .ReadFromJsonAsync<SyncEnvelopeDto>(cancellationToken: ct)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Empty response body from {url}.");

        return replyDto.ToEnvelope();
    }

    /// <inheritdoc />
    public IDisposable RegisterHandler(PeerId local, Func<SyncEnvelope, ValueTask<SyncEnvelope>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        if (!_handlers.TryAdd(local, handler))
            throw new InvalidOperationException($"Peer {local} already registered.");

        return new Unregister(() => _handlers.TryRemove(local, out _));
    }

    /// <inheritdoc />
    public ValueTask<SyncEnvelope> DispatchAsync(SyncEnvelope incoming, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        ct.ThrowIfCancellationRequested();

        if (!_handlers.TryGetValue(incoming.ToPeer, out var handler))
            throw new InvalidOperationException($"No handler registered for peer {incoming.ToPeer}.");

        return handler(incoming);
    }

    private sealed class Unregister : IDisposable
    {
        private readonly Action _onDispose;
        private int _disposed;

        public Unregister(Action onDispose)
        {
            _onDispose = onDispose;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                _onDispose();
        }
    }
}
