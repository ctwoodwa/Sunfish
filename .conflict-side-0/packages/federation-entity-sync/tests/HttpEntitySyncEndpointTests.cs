using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sunfish.Federation.Common;
using Sunfish.Federation.EntitySync.Http;
using Sunfish.Federation.EntitySync.Http.DependencyInjection;
using Sunfish.Foundation.Crypto;
using Xunit;

namespace Sunfish.Federation.EntitySync.Tests;

/// <summary>
/// End-to-end integration tests for <see cref="HttpSyncTransport"/> + <see cref="EntitySyncEndpoint"/>.
/// Each test starts one or two in-process Kestrel hosts bound to <c>127.0.0.1:0</c> (OS-assigned
/// ports) and exercises the HTTP+JSON federation protocol across the wire.
/// </summary>
public class HttpEntitySyncEndpointTests
{
    /// <summary>
    /// Hosts a single federation peer as an in-process Kestrel app. Creates an
    /// <see cref="InMemoryChangeStore"/>, a signer/verifier pair from a fresh keypair, wires
    /// <see cref="HttpSyncTransport"/> through DI, starts the ASP.NET endpoint, and finally
    /// constructs an <see cref="InMemoryEntitySyncer"/> that registers its handler on the
    /// transport.
    /// </summary>
    private sealed class PeerHost : IAsyncDisposable
    {
        public WebApplication App { get; }
        public InMemoryChangeStore Store { get; }
        public KeyPair Key { get; }
        public Ed25519Signer Signer { get; }
        public InMemoryEntitySyncer Syncer { get; }
        public HttpSyncTransport Transport { get; }
        public Uri BaseUri { get; }
        public PeerDescriptor Descriptor { get; }

        private PeerHost(
            WebApplication app,
            InMemoryChangeStore store,
            KeyPair key,
            Ed25519Signer signer,
            InMemoryEntitySyncer syncer,
            HttpSyncTransport transport,
            Uri baseUri,
            PeerDescriptor descriptor)
        {
            App = app;
            Store = store;
            Key = key;
            Signer = signer;
            Syncer = syncer;
            Transport = transport;
            BaseUri = baseUri;
            Descriptor = descriptor;
        }

        public static async Task<PeerHost> StartAsync()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Development,
            });
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");

            builder.Services.AddSunfishEntitySyncHttp();

            var app = builder.Build();
            app.MapEntitySyncEndpoints();

            await app.StartAsync();

            var baseUrl = app.Urls.First();
            var baseUri = new Uri(baseUrl);

            var key = KeyPair.Generate();
            var signer = new Ed25519Signer(key);
            var verifier = new Ed25519Verifier();
            var store = new InMemoryChangeStore();

            var transport = app.Services.GetRequiredService<HttpSyncTransport>();
            var syncer = new InMemoryEntitySyncer(store, transport, signer, verifier);

            var descriptor = new PeerDescriptor(PeerId.From(key.PrincipalId), baseUri);

            return new PeerHost(app, store, key, signer, syncer, transport, baseUri, descriptor);
        }

        public async ValueTask DisposeAsync()
        {
            Syncer.Dispose();
            await App.DisposeAsync();
            Key.Dispose();
        }
    }

    [Fact]
    public async Task HttpRoundTrip_SendsAndReceivesEnvelope_AcrossTwoHosts()
    {
        // Two independent in-process hosts; Alice pushes a signed change chain to Bob over HTTP.
        await using var alice = await PeerHost.StartAsync();
        await using var bob = await PeerHost.StartAsync();

        var entity = TestData.NewEntity("http-item-1");
        var c1 = TestData.NewSigned(alice.Signer, entity, sequence: 1);
        var c2 = TestData.NewSigned(alice.Signer, entity, sequence: 2, parent: c1.Payload.VersionId);
        var c3 = TestData.NewSigned(alice.Signer, entity, sequence: 3, parent: c2.Payload.VersionId);
        alice.Store.Put(c1);
        alice.Store.Put(c2);
        alice.Store.Put(c3);

        var aliceHeadsBefore = alice.Store.GetHeads(null);
        var bobHeadsBefore = bob.Store.GetHeads(null);
        Assert.Single(aliceHeadsBefore);
        Assert.Empty(bobHeadsBefore);

        var result = await alice.Syncer.PushToAsync(bob.Descriptor, scope: null, CancellationToken.None);

        Assert.Equal(3, result.ChangesTransferred);
        Assert.Equal(0, result.ChangesRejected);
        Assert.Empty(result.Rejections);
        Assert.True(bob.Store.Contains(c1.Payload.VersionId));
        Assert.True(bob.Store.Contains(c2.Payload.VersionId));
        Assert.True(bob.Store.Contains(c3.Payload.VersionId));
    }

    [Fact]
    public async Task MalformedEnvelope_Returns400BadRequest()
    {
        await using var alice = await PeerHost.StartAsync();
        using var client = new HttpClient { BaseAddress = alice.BaseUri };

        var content = new StringContent("{ this is not valid envelope json");
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        var response = await client.PostAsync(HttpSyncTransport.EndpointPath, content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnknownTargetPeer_Returns404NotFound()
    {
        // Alice hosts a valid endpoint, but the envelope is addressed to a peer id she doesn't host.
        await using var alice = await PeerHost.StartAsync();
        using var strangerKey = KeyPair.Generate();
        var strangerId = PeerId.From(strangerKey.PrincipalId);

        using var senderKey = KeyPair.Generate();
        var senderSigner = new Ed25519Signer(senderKey);

        // Sign an envelope from sender → strangerId (NOT Alice), so Alice's dispatcher has no handler.
        var envelope = SyncEnvelope.SignAndCreate(
            senderSigner,
            strangerId,
            SyncMessageKind.HealthProbe,
            new byte[] { 0x01 });
        var dto = SyncEnvelopeDto.From(envelope);

        using var client = new HttpClient { BaseAddress = alice.BaseUri };
        var response = await client.PostAsJsonAsync(HttpSyncTransport.EndpointPath, dto);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
