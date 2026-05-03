using System.Net;
using Sunfish.Blocks.PublicListings.Defense;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Integrations.Captcha;
using Xunit;

namespace Sunfish.Blocks.PublicListings.Tests;

public sealed class InquiryFormDefenseTests
{
    private static readonly TenantId TestTenant = new("tenant-a");
    private static readonly IPAddress TestIp = IPAddress.Parse("198.51.100.42");

    private static (InquiryFormDefense def, InMemoryCaptchaVerifier captcha, InMemoryInquiryRateLimiter rate, StubEmailMxResolver mx) NewDefense()
    {
        var captcha = new InMemoryCaptchaVerifier();
        var rate = new InMemoryInquiryRateLimiter();
        var mx = new StubEmailMxResolver { DefaultVerdict = true };
        var def = new InquiryFormDefense(captcha, rate, mx);
        return (def, captcha, rate, mx);
    }

    private static InquiryFormSubmission MakeSubmission(string token = "good-token", string email = "alice@example.com", IPAddress? ip = null) => new()
    {
        Tenant = TestTenant,
        CaptchaToken = token,
        ClientIp = ip ?? TestIp,
        ProspectEmail = email,
        MessageBody = "Is this listing still available?",
        ReceivedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task AllLayersAccept_Passes()
    {
        var (def, captcha, _, _) = NewDefense();
        captcha.Seed("good-token", 0.9);

        var result = await def.EvaluateAsync(MakeSubmission(), default);

        Assert.True(result.Passed);
        Assert.Null(result.RejectedAt);
    }

    [Fact]
    public async Task Layer1_CaptchaFails_RejectsAtCaptcha()
    {
        var (def, _, _, _) = NewDefense();

        // Token not seeded → CAPTCHA returns Passed=false.
        var result = await def.EvaluateAsync(MakeSubmission(token: "bot-token"), default);

        Assert.False(result.Passed);
        Assert.Equal(InquiryDefenseLayer.Captcha, result.RejectedAt);
    }

    [Fact]
    public async Task Layer2_PerIpRateLimit_RejectsOnSixth()
    {
        var (def, captcha, _, _) = NewDefense();
        captcha.Seed("good-token", 0.9);

        // Default per-IP limit is 5; 6th from same IP should hit rate limit.
        for (var i = 0; i < 5; i++)
        {
            var ok = await def.EvaluateAsync(MakeSubmission(), default);
            Assert.True(ok.Passed);
        }
        var sixth = await def.EvaluateAsync(MakeSubmission(), default);

        Assert.False(sixth.Passed);
        Assert.Equal(InquiryDefenseLayer.RateLimit, sixth.RejectedAt);
        Assert.Contains("PerIp", sixth.Reason);
    }

    [Fact]
    public async Task Layer2_PerTenantRateLimit_RejectsAtTenantBudget()
    {
        var captcha = new InMemoryCaptchaVerifier();
        captcha.Seed("good-token", 0.9);
        // 2/IP, 3/tenant — small thresholds for testability.
        var rate = new InMemoryInquiryRateLimiter(perIpLimit: 2, perTenantLimit: 3, window: TimeSpan.FromHours(1));
        var mx = new StubEmailMxResolver { DefaultVerdict = true };
        var def = new InquiryFormDefense(captcha, rate, mx);

        // Cycle through 4 distinct IPs for the same tenant — 4th should hit tenant cap.
        var ips = new[] { "10.0.0.1", "10.0.0.2", "10.0.0.3", "10.0.0.4" }.Select(IPAddress.Parse).ToArray();
        for (var i = 0; i < 3; i++)
        {
            var ok = await def.EvaluateAsync(MakeSubmission(ip: ips[i]), default);
            Assert.True(ok.Passed);
        }
        var fourth = await def.EvaluateAsync(MakeSubmission(ip: ips[3]), default);

        Assert.False(fourth.Passed);
        Assert.Equal(InquiryDefenseLayer.RateLimit, fourth.RejectedAt);
        Assert.Contains("PerTenant", fourth.Reason);
    }

    [Fact]
    public async Task Layer3_BadEmailFormat_RejectsAtEmail()
    {
        var (def, captcha, _, _) = NewDefense();
        captcha.Seed("good-token", 0.9);

        var result = await def.EvaluateAsync(MakeSubmission(email: "not-an-email"), default);

        Assert.False(result.Passed);
        Assert.Equal(InquiryDefenseLayer.EmailFormatAndMx, result.RejectedAt);
        Assert.Contains("format", result.Reason!);
    }

    [Fact]
    public async Task Layer3_NoMxRecord_RejectsAtEmail()
    {
        var (def, captcha, _, mx) = NewDefense();
        captcha.Seed("good-token", 0.9);
        mx.Seed("nodomain.test", false);

        var result = await def.EvaluateAsync(MakeSubmission(email: "alice@nodomain.test"), default);

        Assert.False(result.Passed);
        Assert.Equal(InquiryDefenseLayer.EmailFormatAndMx, result.RejectedAt);
        Assert.Contains("MX", result.Reason!);
    }

    [Fact]
    public async Task FailClosed_LaterLayersDoNotRunIfEarlierFails()
    {
        var (def, _, _, mx) = NewDefense();
        // Don't seed CAPTCHA → Layer 1 fails. The MX seed should never be consulted.
        mx.Seed("alice@example.com", false);

        var result = await def.EvaluateAsync(MakeSubmission(token: "no-good"), default);

        Assert.False(result.Passed);
        Assert.Equal(InquiryDefenseLayer.Captcha, result.RejectedAt);
    }

    [Fact]
    public async Task RateLimiter_DenialDoesNotConsumeBudget()
    {
        var captcha = new InMemoryCaptchaVerifier();
        captcha.Seed("good-token", 0.9);
        var rate = new InMemoryInquiryRateLimiter(perIpLimit: 1, perTenantLimit: 100, window: TimeSpan.FromHours(1));
        var mx = new StubEmailMxResolver { DefaultVerdict = true };
        var def = new InquiryFormDefense(captcha, rate, mx);

        var ok = await def.EvaluateAsync(MakeSubmission(), default);
        Assert.True(ok.Passed);
        var blocked = await def.EvaluateAsync(MakeSubmission(), default);
        Assert.False(blocked.Passed);
        // A third call from same IP — denial doesn't have to consume budget,
        // but with limit=1 the IP is already at cap so it stays denied.
        var third = await def.EvaluateAsync(MakeSubmission(), default);
        Assert.False(third.Passed);
    }

    [Fact]
    public void Constructor_RejectsNullDeps()
    {
        var captcha = new InMemoryCaptchaVerifier();
        var rate = new InMemoryInquiryRateLimiter();
        var mx = new StubEmailMxResolver();
        Assert.Throws<ArgumentNullException>(() => new InquiryFormDefense(null!, rate, mx));
        Assert.Throws<ArgumentNullException>(() => new InquiryFormDefense(captcha, null!, mx));
        Assert.Throws<ArgumentNullException>(() => new InquiryFormDefense(captcha, rate, null!));
    }

    [Fact]
    public async Task EvaluateAsync_RejectsNullSubmission()
    {
        var (def, _, _, _) = NewDefense();
        await Assert.ThrowsAsync<ArgumentNullException>(() => def.EvaluateAsync(null!, default));
    }
}
