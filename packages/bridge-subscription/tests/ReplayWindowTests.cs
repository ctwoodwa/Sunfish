using System;
using Xunit;

namespace Sunfish.Bridge.Subscription.Tests;

public sealed class ReplayWindowTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IsFresh_WithinFiveMinutes_ReturnsTrue()
    {
        var window = new ReplayWindow();
        Assert.True(window.IsFresh(Now.AddMinutes(-4), Now));
        Assert.True(window.IsFresh(Now.AddMinutes(4), Now));
        Assert.True(window.IsFresh(Now, Now));
    }

    [Fact]
    public void IsFresh_ExactlyAtBoundary_ReturnsTrue()
    {
        var window = new ReplayWindow();
        Assert.True(window.IsFresh(Now.AddMinutes(-5), Now));
        Assert.True(window.IsFresh(Now.AddMinutes(5), Now));
    }

    [Fact]
    public void IsFresh_PastFiveMinutes_ReturnsFalse()
    {
        var window = new ReplayWindow();
        Assert.False(window.IsFresh(Now.AddMinutes(-6), Now));
        Assert.False(window.IsFresh(Now.AddMinutes(6), Now));
    }

    [Fact]
    public void IsFresh_RespectsTunablePerDeployment()
    {
        var window = new ReplayWindow(TimeSpan.FromSeconds(30));
        Assert.True(window.IsFresh(Now.AddSeconds(-30), Now));
        Assert.False(window.IsFresh(Now.AddSeconds(-31), Now));
    }

    [Fact]
    public void SkewSeconds_PositiveWhenEffectiveAtIsFuture()
    {
        var window = new ReplayWindow();
        Assert.Equal(60, window.SkewSeconds(Now.AddMinutes(1), Now));
        Assert.Equal(-60, window.SkewSeconds(Now.AddMinutes(-1), Now));
    }
}
