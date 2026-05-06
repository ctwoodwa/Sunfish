using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.SickBay.Tests;

public class NoopKeyRotationSchedulerTests
{
    [Fact]
    public async Task ScheduleAsync_CompletesWithoutThrowing()
    {
        var scheduler = new NoopKeyRotationScheduler();
        await scheduler.ScheduleAsync(
            new TenantId("alpha"),
            "recovery-key",
            "manual-trigger");
    }

    [Fact]
    public async Task ScheduleAsync_Cancellation_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var scheduler = new NoopKeyRotationScheduler();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => scheduler.ScheduleAsync(
                new TenantId("alpha"),
                "recovery-key",
                "manual-trigger",
                cts.Token));
    }
}
