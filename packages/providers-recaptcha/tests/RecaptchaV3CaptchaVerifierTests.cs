using System.Net;
using System.Text;
using Sunfish.Providers.Recaptcha;
using Xunit;

namespace Sunfish.Providers.Recaptcha.Tests;

public sealed class RecaptchaV3CaptchaVerifierTests
{
    private static readonly IPAddress TestIp = IPAddress.Parse("198.51.100.42");
    private const string TestSecret = "test-secret-key-1234567890";

    private sealed class StubHttpHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastFormBody { get; private set; }
        public Func<HttpRequestMessage, HttpResponseMessage>? Responder { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
            {
                LastFormBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }
            return Responder?.Invoke(request) ?? new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"success":true,"score":0.9}""", Encoding.UTF8, "application/json"),
            };
        }
    }

    private static (RecaptchaV3CaptchaVerifier verifier, StubHttpHandler handler) NewVerifier(double minScore = 0.3)
    {
        var handler = new StubHttpHandler();
        var http = new HttpClient(handler);
        var verifier = new RecaptchaV3CaptchaVerifier(http, new RecaptchaV3Config
        {
            SiteKey = "test-site-key",
            SecretKey = TestSecret,
            MinPassingScore = minScore,
            VerifyEndpoint = "https://test.invalid/recaptcha/api/siteverify",
        });
        return (verifier, handler);
    }

    [Fact]
    public async Task VerifyAsync_PostsToConfiguredEndpoint()
    {
        var (verifier, handler) = NewVerifier();

        await verifier.VerifyAsync("user-token", TestIp, default);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("https://test.invalid/recaptcha/api/siteverify", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task VerifyAsync_SendsSecret_Token_RemoteIp_AsForm()
    {
        var (verifier, handler) = NewVerifier();
        await verifier.VerifyAsync("user-token", TestIp, default);

        Assert.NotNull(handler.LastFormBody);
        Assert.Contains($"secret={TestSecret}", handler.LastFormBody!);
        Assert.Contains("response=user-token", handler.LastFormBody!);
        Assert.Contains($"remoteip={TestIp}", handler.LastFormBody!);
    }

    [Fact]
    public async Task VerifyAsync_HighScore_AboveThreshold_Passes()
    {
        var (verifier, handler) = NewVerifier(minScore: 0.5);
        handler.Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"success":true,"score":0.9}""", Encoding.UTF8, "application/json"),
        };

        var result = await verifier.VerifyAsync("user-token", TestIp, default);

        Assert.True(result.Passed);
        Assert.Equal(0.9, result.Score);
        Assert.Equal("recaptcha-v3", result.Provider);
    }

    [Fact]
    public async Task VerifyAsync_LowScore_BelowThreshold_Fails()
    {
        var (verifier, handler) = NewVerifier(minScore: 0.5);
        handler.Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"success":true,"score":0.2}""", Encoding.UTF8, "application/json"),
        };

        var result = await verifier.VerifyAsync("user-token", TestIp, default);

        Assert.False(result.Passed);
        Assert.Equal(0.2, result.Score);
    }

    [Fact]
    public async Task VerifyAsync_SuccessFalse_ReturnsFailedRegardlessOfScore()
    {
        var (verifier, handler) = NewVerifier();
        handler.Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"success":false,"score":0.95,"error-codes":["invalid-input-response"]}""", Encoding.UTF8, "application/json"),
        };

        var result = await verifier.VerifyAsync("user-token", TestIp, default);

        Assert.False(result.Passed);
    }

    [Fact]
    public async Task VerifyAsync_5xxResponse_FailsClosed()
    {
        var (verifier, handler) = NewVerifier();
        handler.Responder = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("upstream error"),
        };

        var result = await verifier.VerifyAsync("user-token", TestIp, default);

        Assert.False(result.Passed);
        Assert.Equal(0.0, result.Score);
    }

    [Fact]
    public async Task VerifyAsync_BoundaryScore_AtThreshold_Passes()
    {
        var (verifier, handler) = NewVerifier(minScore: 0.3);
        handler.Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"success":true,"score":0.3}""", Encoding.UTF8, "application/json"),
        };

        var result = await verifier.VerifyAsync("user-token", TestIp, default);

        Assert.True(result.Passed);
    }

    [Fact]
    public async Task VerifyAsync_RejectsEmptyToken()
    {
        var (verifier, _) = NewVerifier();
        await Assert.ThrowsAsync<ArgumentException>(() => verifier.VerifyAsync("", TestIp, default));
    }

    [Fact]
    public async Task VerifyAsync_RejectsNullIp()
    {
        var (verifier, _) = NewVerifier();
        await Assert.ThrowsAsync<ArgumentNullException>(() => verifier.VerifyAsync("token", null!, default));
    }

    [Fact]
    public void Constructor_RejectsEmptySecretKey()
    {
        var http = new HttpClient(new StubHttpHandler());
        Assert.Throws<ArgumentException>(() =>
            new RecaptchaV3CaptchaVerifier(http, new RecaptchaV3Config
            {
                SiteKey = "site",
                SecretKey = "",
            }));
    }

    [Fact]
    public void Constructor_RejectsNullArgs()
    {
        var http = new HttpClient(new StubHttpHandler());
        var config = new RecaptchaV3Config { SiteKey = "site", SecretKey = "secret" };
        Assert.Throws<ArgumentNullException>(() => new RecaptchaV3CaptchaVerifier(null!, config));
        Assert.Throws<ArgumentNullException>(() => new RecaptchaV3CaptchaVerifier(http, null!));
    }
}
