using System.Net;
using Sunfish.Foundation.Integrations.Captcha;

namespace Sunfish.Foundation.Integrations.Tests;

public class InMemoryCaptchaVerifierTests
{
    private static readonly IPAddress TestIp = IPAddress.Parse("198.51.100.42");

    [Fact]
    public async Task UnseededToken_FailsWithZeroScore()
    {
        var verifier = new InMemoryCaptchaVerifier();
        var result = await verifier.VerifyAsync("unknown-token", TestIp, default);

        Assert.False(result.Passed);
        Assert.Equal(0.0, result.Score);
        Assert.Equal("in-memory", result.Provider);
    }

    [Fact]
    public async Task SeededToken_AboveThreshold_Passes()
    {
        var verifier = new InMemoryCaptchaVerifier(minPassingScore: 0.3);
        verifier.Seed("good-token", score: 0.9);

        var result = await verifier.VerifyAsync("good-token", TestIp, default);

        Assert.True(result.Passed);
        Assert.Equal(0.9, result.Score);
    }

    [Fact]
    public async Task SeededToken_BelowThreshold_Fails()
    {
        var verifier = new InMemoryCaptchaVerifier(minPassingScore: 0.5);
        verifier.Seed("bot-token", score: 0.2);

        var result = await verifier.VerifyAsync("bot-token", TestIp, default);

        Assert.False(result.Passed);
        Assert.Equal(0.2, result.Score);
    }

    [Fact]
    public async Task Threshold_Boundary_AtScore_Passes()
    {
        var verifier = new InMemoryCaptchaVerifier(minPassingScore: 0.3);
        verifier.Seed("borderline", score: 0.3);

        var result = await verifier.VerifyAsync("borderline", TestIp, default);

        Assert.True(result.Passed);
    }

    [Fact]
    public async Task VerifyAsync_RecordsCallInJournal()
    {
        var verifier = new InMemoryCaptchaVerifier();
        await verifier.VerifyAsync("any-token", TestIp, default);
        await verifier.VerifyAsync("another", IPAddress.Loopback, default);

        Assert.Equal(2, verifier.Calls.Count);
    }

    [Fact]
    public async Task VerifyAsync_ThrowsOnNull_Or_Empty_Token()
    {
        var verifier = new InMemoryCaptchaVerifier();
        await Assert.ThrowsAsync<ArgumentException>(() => verifier.VerifyAsync("", TestIp, default));
        await Assert.ThrowsAsync<ArgumentNullException>(() => verifier.VerifyAsync(null!, TestIp, default));
    }

    [Fact]
    public async Task VerifyAsync_ThrowsOnNull_ClientIp()
    {
        var verifier = new InMemoryCaptchaVerifier();
        await Assert.ThrowsAsync<ArgumentNullException>(() => verifier.VerifyAsync("t", null!, default));
    }

    [Fact]
    public void Constructor_RejectsScoreOutsideRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new InMemoryCaptchaVerifier(minPassingScore: 1.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => new InMemoryCaptchaVerifier(minPassingScore: -0.1));
    }

    [Fact]
    public void Seed_RejectsScoreOutsideRange()
    {
        var verifier = new InMemoryCaptchaVerifier();
        Assert.Throws<ArgumentOutOfRangeException>(() => verifier.Seed("t", 1.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => verifier.Seed("t", -0.1));
    }

    [Fact]
    public async Task VerifyAsync_HonorsCancellationToken()
    {
        var verifier = new InMemoryCaptchaVerifier();
        var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => verifier.VerifyAsync("t", TestIp, cts.Token));
    }
}
