using System;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Blocks.Reports.Exceptions;
using Xunit;

namespace Sunfish.Blocks.Reports.Tests;

public sealed class ReportCartridgeRegistryTests
{
    public sealed class FooParams { }
    public sealed class FooResult { }
    public sealed class BarParams { }
    public sealed class BarResult { }

    private sealed class FooCartridge : IReportCartridge<FooParams, FooResult>
    {
        public ReportKind Kind => ReportKind.TrialBalance;
        public Task<FooResult> ExecuteAsync(ReportExecutionContext context, FooParams parameters, CancellationToken ct = default)
            => Task.FromResult(new FooResult());
    }

    private sealed class BarCartridge : IReportCartridge<BarParams, BarResult>
    {
        public ReportKind Kind => ReportKind.ArAgingSummary;
        public Task<BarResult> ExecuteAsync(ReportExecutionContext context, BarParams parameters, CancellationToken ct = default)
            => Task.FromResult(new BarResult());
    }

    [Fact]
    public void Register_NewKind_Succeeds()
    {
        var registry = new ReportCartridgeRegistry();
        registry.Register<FooParams, FooResult>(new FooCartridge());
        Assert.Contains(ReportKind.TrialBalance, registry.RegisteredKinds);
    }

    [Fact]
    public void Register_DuplicateKey_Throws()
    {
        var registry = new ReportCartridgeRegistry();
        registry.Register<FooParams, FooResult>(new FooCartridge());
        Assert.Throws<InvalidOperationException>(() =>
            registry.Register<FooParams, FooResult>(new FooCartridge()));
    }

    [Fact]
    public void Resolve_RegisteredKind_ReturnsCartridge()
    {
        var registry = new ReportCartridgeRegistry();
        var cartridge = new FooCartridge();
        registry.Register<FooParams, FooResult>(cartridge);
        var resolved = registry.Resolve<FooParams, FooResult>(ReportKind.TrialBalance);
        Assert.Same(cartridge, resolved);
    }

    [Fact]
    public void Resolve_UnregisteredKind_ThrowsUnknownReportKindException()
    {
        var registry = new ReportCartridgeRegistry();
        Assert.Throws<UnknownReportKindException>(() =>
            registry.Resolve<FooParams, FooResult>(ReportKind.TrialBalance));
    }

    [Fact]
    public void Resolve_RegisteredKindWithWrongParamsType_ThrowsUnknownReportKindException()
    {
        var registry = new ReportCartridgeRegistry();
        registry.Register<FooParams, FooResult>(new FooCartridge());
        // Same Kind, different TParams — must surface as unknown (not as a misroute).
        Assert.Throws<UnknownReportKindException>(() =>
            registry.Resolve<BarParams, FooResult>(ReportKind.TrialBalance));
    }

    [Fact]
    public void TryResolve_UnregisteredKind_ReturnsFalseAndNullOut()
    {
        var registry = new ReportCartridgeRegistry();
        var ok = registry.TryResolve<FooParams, FooResult>(ReportKind.TrialBalance, out var cartridge);
        Assert.False(ok);
        Assert.Null(cartridge);
    }

    [Fact]
    public void RegisteredKinds_ReturnsDistinctSet()
    {
        var registry = new ReportCartridgeRegistry();
        registry.Register<FooParams, FooResult>(new FooCartridge());
        registry.Register<BarParams, BarResult>(new BarCartridge());
        Assert.Equal(2, registry.RegisteredKinds.Count);
        Assert.Contains(ReportKind.TrialBalance, registry.RegisteredKinds);
        Assert.Contains(ReportKind.ArAgingSummary, registry.RegisteredKinds);
    }
}
