using System;
using Xunit;

namespace Sunfish.Bridge.Subscription.Tests;

public sealed class WebhookRetryPolicyTests
{
    [Fact]
    public void Delays_AreA1_5_Schedule()
    {
        Assert.Equal(7, WebhookRetryPolicy.Delays.Count);
        Assert.Equal(TimeSpan.FromSeconds(1), WebhookRetryPolicy.Delays[0]);
        Assert.Equal(TimeSpan.FromSeconds(5), WebhookRetryPolicy.Delays[1]);
        Assert.Equal(TimeSpan.FromSeconds(30), WebhookRetryPolicy.Delays[2]);
        Assert.Equal(TimeSpan.FromMinutes(5), WebhookRetryPolicy.Delays[3]);
        Assert.Equal(TimeSpan.FromMinutes(30), WebhookRetryPolicy.Delays[4]);
        Assert.Equal(TimeSpan.FromHours(2), WebhookRetryPolicy.Delays[5]);
        Assert.Equal(TimeSpan.FromHours(12), WebhookRetryPolicy.Delays[6]);
    }

    [Fact]
    public void MaxAttempts_Is8()
    {
        Assert.Equal(8, WebhookRetryPolicy.MaxAttempts); // 1 initial + 7 retries
    }

    [Fact]
    public void DelayBeforeAttempt_FirstAttemptIsZero()
    {
        Assert.Equal(TimeSpan.Zero, WebhookRetryPolicy.DelayBeforeAttempt(1));
    }

    [Theory]
    [InlineData(2, 1)]      // 1s
    [InlineData(3, 5)]      // 5s
    [InlineData(4, 30)]     // 30s
    [InlineData(5, 300)]    // 5min
    [InlineData(6, 1800)]   // 30min
    [InlineData(7, 7200)]   // 2h
    [InlineData(8, 43200)]  // 12h
    public void DelayBeforeAttempt_MatchesA1_5_Schedule(int attempt, int expectedSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), WebhookRetryPolicy.DelayBeforeAttempt(attempt));
    }

    [Fact]
    public void DelayBeforeAttempt_ZeroOrNegative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WebhookRetryPolicy.DelayBeforeAttempt(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => WebhookRetryPolicy.DelayBeforeAttempt(-1));
    }

    [Fact]
    public void DelayBeforeAttempt_PastMaxAttempts_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WebhookRetryPolicy.DelayBeforeAttempt(9));
    }

    [Fact]
    public void IsTerminalAttempt_8IsTerminal_7IsNot()
    {
        Assert.True(WebhookRetryPolicy.IsTerminalAttempt(8));
        Assert.False(WebhookRetryPolicy.IsTerminalAttempt(7));
        Assert.False(WebhookRetryPolicy.IsTerminalAttempt(1));
    }
}
