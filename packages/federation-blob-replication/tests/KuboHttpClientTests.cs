using System.Net;
using System.Text;
using Sunfish.Federation.BlobReplication.Kubo;
using Xunit;

namespace Sunfish.Federation.BlobReplication.Tests;

public sealed class KuboHttpClientTests
{
    [Fact]
    public async Task AddAsync_PostsMultipartFormToAddEndpointWithCidV1Flags()
    {
        var handler = new RecordingHandler((req, _) =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.NotNull(req.RequestUri);
            var url = req.RequestUri!.ToString();
            Assert.Contains("/api/v0/add", url);
            Assert.Contains("cid-version=1", url);
            Assert.Contains("raw-leaves=true", url);
            Assert.Contains("pin=true", url);

            var contentType = req.Content?.Headers.ContentType;
            Assert.NotNull(contentType);
            Assert.Equal("multipart/form-data", contentType!.MediaType);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"Name\":\"blob\",\"Hash\":\"bafkreigh2akiscaildc5fv7zpsg72oq2q4lbxpkx6a4qlfygh2vx6n4ff4\",\"Size\":\"5\"}",
                    Encoding.UTF8,
                    "application/json"),
            };
        });

        var http = new HttpClient(handler) { BaseAddress = new Uri("http://kubo.test/") };
        var client = new KuboHttpClient(http);

        var response = await client.AddAsync(Encoding.UTF8.GetBytes("hello"), pin: true, CancellationToken.None);

        Assert.Equal("blob", response.Name);
        Assert.Equal("bafkreigh2akiscaildc5fv7zpsg72oq2q4lbxpkx6a4qlfygh2vx6n4ff4", response.Hash);
        Assert.Equal("5", response.Size);
    }

    [Fact]
    public async Task CatAsync_PassesCidViaArgQueryParamAndReturnsBinaryBody()
    {
        var payload = new byte[] { 0x01, 0x02, 0x03, 0xff };
        var handler = new RecordingHandler((req, _) =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.NotNull(req.RequestUri);
            var query = req.RequestUri!.Query;
            Assert.Contains("arg=bafkrei", query);
            Assert.Contains("/api/v0/cat", req.RequestUri!.AbsolutePath);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload),
            };
        });

        var http = new HttpClient(handler) { BaseAddress = new Uri("http://kubo.test/") };
        var client = new KuboHttpClient(http);

        var bytes = await client.CatAsync("bafkreigh2akiscaildc5fv7zpsg72oq2q4lbxpkx6a4qlfygh2vx6n4ff4", CancellationToken.None);

        Assert.NotNull(bytes);
        Assert.Equal(payload, bytes);
    }

    [Fact]
    public async Task CatAsync_NotFoundErrorBody_ReturnsNull()
    {
        var handler = new RecordingHandler((_, _) =>
        {
            return new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent(
                    "{\"Message\":\"block not found\",\"Code\":0,\"Type\":\"error\"}",
                    Encoding.UTF8,
                    "application/json"),
            };
        });

        var http = new HttpClient(handler) { BaseAddress = new Uri("http://kubo.test/") };
        var client = new KuboHttpClient(http);

        var bytes = await client.CatAsync("bafkreigh2akiscaildc5fv7zpsg72oq2q4lbxpkx6a4qlfygh2vx6n4ff4", CancellationToken.None);

        Assert.Null(bytes);
    }

    [Fact]
    public async Task GetConfigAsync_PostsToConfigShowAndParsesSwarmKey()
    {
        var handler = new RecordingHandler((req, _) =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Equal("/api/v0/config/show", req.RequestUri!.AbsolutePath);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"Swarm\":{\"SwarmKey\":\"/key/swarm/psk/1.0.0/\\nbase16\\naabbcc\"}}",
                    Encoding.UTF8,
                    "application/json"),
            };
        });

        var http = new HttpClient(handler) { BaseAddress = new Uri("http://kubo.test/") };
        var client = new KuboHttpClient(http);

        var config = await client.GetConfigAsync(CancellationToken.None);

        Assert.NotNull(config.Swarm);
        Assert.False(string.IsNullOrEmpty(config.Swarm.SwarmKey));
        Assert.Contains("base16", config.Swarm.SwarmKey!);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _handler;

        public RecordingHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(_handler(request, ct));
    }
}
