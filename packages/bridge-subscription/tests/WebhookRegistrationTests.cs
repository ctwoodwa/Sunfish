using System;
using System.Threading.Tasks;
using Xunit;

namespace Sunfish.Bridge.Subscription.Tests;

public sealed class WebhookRegistrationServiceTests
{
    private static readonly Uri ValidHttps = new("https://anchor.example.com/sunfish/bridge-events");

    private static WebhookRegistration NewReg(Uri? url = null, string? secret = "") => new()
    {
        CallbackUrl = url ?? ValidHttps,
        DeliveryMode = DeliveryMode.Webhook,
        SubscribedEvents = null,
        SharedSecret = secret ?? string.Empty,
    };

    [Fact]
    public async Task RegisterAsync_HttpScheme_Throws()
    {
        var svc = new DefaultWebhookRegistrationService();
        var http = NewReg(new Uri("http://anchor.example.com/x"));
        await Assert.ThrowsAsync<WebhookRegistrationException>(() =>
            svc.RegisterAsync("tenant-a", http).AsTask());
    }

    [Theory]
    [InlineData("https://localhost/x")]
    [InlineData("https://127.0.0.1/x")]
    [InlineData("https://[::1]/x")]
    public async Task RegisterAsync_LoopbackHost_Throws(string url)
    {
        var svc = new DefaultWebhookRegistrationService();
        var reg = NewReg(new Uri(url));
        await Assert.ThrowsAsync<WebhookRegistrationException>(() =>
            svc.RegisterAsync("tenant-a", reg).AsTask());
    }

    [Fact]
    public async Task RegisterAsync_HttpsNonLoopback_Succeeds()
    {
        var svc = new DefaultWebhookRegistrationService();
        var registered = await svc.RegisterAsync("tenant-a", NewReg());
        Assert.Equal(ValidHttps, registered.CallbackUrl);
    }

    [Fact]
    public async Task RegisterAsync_NoSharedSecret_AutoGeneratesNonEmpty()
    {
        var svc = new DefaultWebhookRegistrationService();
        var registered = await svc.RegisterAsync("tenant-a", NewReg(secret: string.Empty));
        Assert.False(string.IsNullOrEmpty(registered.SharedSecret));
        Assert.True(registered.SharedSecret.Length >= 32); // 32-byte random → ~43 chars base64url
    }

    [Fact]
    public async Task RegisterAsync_GeneratedSecrets_AreUniquePerCall()
    {
        var svc = new DefaultWebhookRegistrationService();
        var a = await svc.RegisterAsync("tenant-a", NewReg(secret: string.Empty));
        var b = await svc.RegisterAsync("tenant-a", NewReg(secret: string.Empty));
        Assert.NotEqual(a.SharedSecret, b.SharedSecret);
    }

    [Fact]
    public async Task RegisterAsync_SuppliedSharedSecret_PreservedVerbatim()
    {
        var svc = new DefaultWebhookRegistrationService();
        var registered = await svc.RegisterAsync("tenant-a", NewReg(secret: "caller-supplied"));
        Assert.Equal("caller-supplied", registered.SharedSecret);
    }

    [Fact]
    public async Task RegisterAsync_NullArgs_Throw()
    {
        var svc = new DefaultWebhookRegistrationService();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.RegisterAsync(string.Empty, NewReg()).AsTask());
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            svc.RegisterAsync("tenant-a", null!).AsTask());
    }
}
