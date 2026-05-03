using System;
using Xunit;

namespace Sunfish.Bridge.Subscription.Tests;

public sealed class SseReconnectPolicyTests
{
    [Fact]
    public void Delays_AreA1_12_4_Schedule()
    {
        Assert.Equal(4, SseReconnectPolicy.Delays.Count);
        Assert.Equal(TimeSpan.FromSeconds(1), SseReconnectPolicy.Delays[0]);
        Assert.Equal(TimeSpan.FromSeconds(5), SseReconnectPolicy.Delays[1]);
        Assert.Equal(TimeSpan.FromSeconds(30), SseReconnectPolicy.Delays[2]);
        Assert.Equal(TimeSpan.FromSeconds(60), SseReconnectPolicy.Delays[3]);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 5)]
    [InlineData(3, 30)]
    [InlineData(4, 60)]
    public void DelayBeforeAttempt_ReturnsScheduledDelay(int attempt, int expectedSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), SseReconnectPolicy.DelayBeforeAttempt(attempt));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(int.MaxValue)]
    public void DelayBeforeAttempt_PastFour_CapsAt60Seconds(int attempt)
    {
        // Per A1.12.4: SSE reconnect is UNBOUNDED; the cap is 60s
        // (NOT a 7-attempt dead-letter like webhook). This is the most
        // common drift the W#36 hand-off halt-condition #2 calls out.
        Assert.Equal(TimeSpan.FromSeconds(60), SseReconnectPolicy.DelayBeforeAttempt(attempt));
    }

    [Fact]
    public void DelayBeforeAttempt_ZeroOrNegative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SseReconnectPolicy.DelayBeforeAttempt(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => SseReconnectPolicy.DelayBeforeAttempt(-1));
    }
}

public sealed class SseQueueOverflowPolicyTests
{
    [Fact]
    public void HasOverflowed_BelowBothThresholds_ReturnsFalse()
    {
        var policy = new SseQueueOverflowPolicy();
        Assert.False(policy.HasOverflowed(5_000, TimeSpan.FromMinutes(30)));
    }

    [Fact]
    public void HasOverflowed_QueueDepthAtThreshold_ReturnsTrue()
    {
        var policy = new SseQueueOverflowPolicy();
        Assert.True(policy.HasOverflowed(SseQueueOverflowPolicy.DefaultMaxEvents, TimeSpan.Zero));
    }

    [Fact]
    public void HasOverflowed_QueueDepthAboveThreshold_ReturnsTrue()
    {
        var policy = new SseQueueOverflowPolicy();
        Assert.True(policy.HasOverflowed(SseQueueOverflowPolicy.DefaultMaxEvents + 1, TimeSpan.Zero));
    }

    [Fact]
    public void HasOverflowed_AgeAtOneHour_ReturnsTrue()
    {
        var policy = new SseQueueOverflowPolicy();
        Assert.True(policy.HasOverflowed(0, SseQueueOverflowPolicy.DefaultMaxAge));
    }

    [Fact]
    public void HasOverflowed_AgePastOneHour_ReturnsTrue()
    {
        var policy = new SseQueueOverflowPolicy();
        Assert.True(policy.HasOverflowed(0, SseQueueOverflowPolicy.DefaultMaxAge.Add(TimeSpan.FromSeconds(1))));
    }

    [Fact]
    public void HasOverflowed_TunablePerDeployment()
    {
        var policy = new SseQueueOverflowPolicy(maxAge: TimeSpan.FromMinutes(15), maxEvents: 100);
        Assert.False(policy.HasOverflowed(99, TimeSpan.FromMinutes(14)));
        Assert.True(policy.HasOverflowed(100, TimeSpan.Zero));
        Assert.True(policy.HasOverflowed(0, TimeSpan.FromMinutes(15)));
    }
}
