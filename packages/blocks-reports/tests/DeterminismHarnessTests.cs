using System.Threading.Tasks;
using Xunit;

namespace Sunfish.Blocks.Reports.Tests;

/// <summary>
/// Shared test base used by PRs 2–6 to assert per-cartridge
/// determinism. Cartridge test classes derive from this with the
/// concrete cartridge / params / result types and supply the
/// builders; the base provides the two determinism assertions.
/// </summary>
public abstract class ReportCartridgeDeterminismTests<TCartridge, TParams, TResult>
    where TCartridge : IReportCartridge<TParams, TResult>
    where TParams : class
    where TResult : class
{
    protected abstract TCartridge BuildCartridge();
    protected abstract TParams BuildParameters();
    protected abstract ReportExecutionContext BuildContext();

    [Fact]
    public async Task ExecuteAsync_IsDeterministic_AcrossRepeatedRuns()
    {
        var cartridge = BuildCartridge();
        var ctx = BuildContext();
        var p = BuildParameters();
        var r1 = await cartridge.ExecuteAsync(ctx, p);
        var r2 = await cartridge.ExecuteAsync(ctx, p);
        Assert.Equal(r1, r2);
    }

    [Fact]
    public async Task ExecuteAsync_SameMarker_SameResult()
    {
        // Documentation test: the snapshot marker is the sole upstream-state
        // input; two runs with the same marker MUST produce equal results.
        var cartridge = BuildCartridge();
        var p = BuildParameters();
        var ctx = BuildContext();
        var r1 = await cartridge.ExecuteAsync(ctx, p);
        var r2 = await cartridge.ExecuteAsync(ctx, p);
        Assert.Equal(r1, r2);
    }
}
