using Microsoft.Extensions.Options;

using Sunfish.Kernel.Runtime.Scheduling;
using Sunfish.Kernel.Runtime.Teams;

namespace Sunfish.Kernel.Runtime.Tests;

public sealed class ResourceGovernorTests
{
    [Fact]
    public async Task Acquires_up_to_cap_without_blocking()
    {
        using var governor = new ResourceGovernor(WithCap(2));

        var a = await governor
            .AcquireGossipSlotAsync(TeamId.New(), CancellationToken.None)
            .AsTask()
            .WaitAsync(TimeSpan.FromSeconds(1));
        var b = await governor
            .AcquireGossipSlotAsync(TeamId.New(), CancellationToken.None)
            .AsTask()
            .WaitAsync(TimeSpan.FromSeconds(1));

        Assert.NotNull(a);
        Assert.NotNull(b);

        a.Dispose();
        b.Dispose();
    }

    [Fact]
    public async Task Blocks_over_cap_until_release()
    {
        using var governor = new ResourceGovernor(WithCap(2));

        var first = await governor.AcquireGossipSlotAsync(TeamId.New(), CancellationToken.None);
        var second = await governor.AcquireGossipSlotAsync(TeamId.New(), CancellationToken.None);

        // Third acquisition must block — cap is saturated.
        var thirdTask = governor
            .AcquireGossipSlotAsync(TeamId.New(), CancellationToken.None)
            .AsTask();

        // Give the scheduler a beat; confirm the task has not completed.
        var raced = await Task.WhenAny(thirdTask, Task.Delay(TimeSpan.FromMilliseconds(150)));
        Assert.NotSame(thirdTask, raced);
        Assert.False(thirdTask.IsCompleted);

        // Release one slot; the pending acquisition should now complete quickly.
        first.Dispose();
        var third = await thirdTask.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.NotNull(third);

        second.Dispose();
        third.Dispose();
    }

    [Fact]
    public async Task Release_is_idempotent()
    {
        using var governor = new ResourceGovernor(WithCap(1));

        var slot = await governor.AcquireGossipSlotAsync(TeamId.New(), CancellationToken.None);

        // Double-dispose must not over-release; the semaphore's count stays at cap.
        slot.Dispose();
        slot.Dispose();
        slot.Dispose();

        // If double-dispose had over-released, we could now acquire TWO slots on a cap-of-1
        // governor. Prove we can only acquire one.
        var next = await governor
            .AcquireGossipSlotAsync(TeamId.New(), CancellationToken.None)
            .AsTask()
            .WaitAsync(TimeSpan.FromSeconds(1));

        var blockedTask = governor
            .AcquireGossipSlotAsync(TeamId.New(), CancellationToken.None)
            .AsTask();

        var raced = await Task.WhenAny(blockedTask, Task.Delay(TimeSpan.FromMilliseconds(150)));
        Assert.NotSame(blockedTask, raced);
        Assert.False(blockedTask.IsCompleted);

        next.Dispose();
        var last = await blockedTask.WaitAsync(TimeSpan.FromSeconds(1));
        last.Dispose();
    }

    [Fact]
    public async Task Respects_cancellation_while_waiting()
    {
        using var governor = new ResourceGovernor(WithCap(1));

        var held = await governor.AcquireGossipSlotAsync(TeamId.New(), CancellationToken.None);

        using var cts = new CancellationTokenSource();
        var waiting = governor.AcquireGossipSlotAsync(TeamId.New(), cts.Token).AsTask();

        // Confirm it is parked waiting for a slot.
        var raced = await Task.WhenAny(waiting, Task.Delay(TimeSpan.FromMilliseconds(100)));
        Assert.NotSame(waiting, raced);

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => waiting.WaitAsync(TimeSpan.FromSeconds(1)));

        // Cancellation must NOT consume the slot — it should still be held by the first acquirer.
        // Proof: releasing the held slot lets a new acquisition proceed without blocking.
        held.Dispose();
        var next = await governor
            .AcquireGossipSlotAsync(TeamId.New(), CancellationToken.None)
            .AsTask()
            .WaitAsync(TimeSpan.FromSeconds(1));
        next.Dispose();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Options_validation_rejects_zero_or_negative(int cap)
    {
        var bad = Options.Create(new ResourceGovernorOptions { MaxActiveRoundsPerTick = cap });

        Assert.Throws<ArgumentOutOfRangeException>(() => new ResourceGovernor(bad));
    }

    [Fact]
    public void Options_default_cap_is_two()
    {
        // Spec lock: ADR 0032 pins MaxActiveRoundsPerTick = 2 as the default.
        Assert.Equal(2, new ResourceGovernorOptions().MaxActiveRoundsPerTick);
    }

    [Fact]
    public void Constructor_null_options_throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ResourceGovernor(null!));
    }

    private static IOptions<ResourceGovernorOptions> WithCap(int cap) =>
        Options.Create(new ResourceGovernorOptions { MaxActiveRoundsPerTick = cap });
}
