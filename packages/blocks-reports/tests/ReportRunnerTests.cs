using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Blocks.Reports.Exceptions;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.Reports.Tests;

public sealed class ReportRunnerTests
{
    public sealed class P { }
    public sealed class R { public bool Sentinel { get; init; } }
    public sealed class ProvR : IReportProvisionalityCarrier
    {
        public bool IsProvisional { get; init; }
        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    }

    private static readonly TenantId Tenant = new("tenant-reports");
    private static readonly PrincipalId Principal = PrincipalId.FromBytes(new byte[32]);

    private sealed class FakeTime : TimeProvider
    {
        public DateTimeOffset Current = new(2026, 5, 17, 12, 0, 0, TimeSpan.Zero);
        public TimeSpan Step = TimeSpan.FromMilliseconds(5);
        private int _calls;
        public override DateTimeOffset GetUtcNow()
        {
            // First call → Current; every subsequent call advances by Step
            // so RunDuration > 0 on the runner.
            if (_calls++ == 0) return Current;
            Current += Step;
            return Current;
        }
    }

    private sealed class CountingMarker : ISnapshotMarkerSource
    {
        public int Calls;
        public Task<string> CaptureAsync(TenantId tenantId, CancellationToken ct = default)
        {
            Interlocked.Increment(ref Calls);
            return Task.FromResult($"marker:{tenantId.Value}:{Calls}");
        }
    }

    private sealed class CapturingCartridge : IReportCartridge<P, R>
    {
        public ReportKind Kind => ReportKind.TrialBalance;
        public ReportExecutionContext? LastContext;
        public CancellationToken LastCt;
        public R Next = new() { Sentinel = true };
        public Task<R> ExecuteAsync(ReportExecutionContext context, P parameters, CancellationToken ct = default)
        {
            LastContext = context;
            LastCt = ct;
            return Task.FromResult(Next);
        }
    }

    private sealed class ProvisionalCartridge : IReportCartridge<P, ProvR>
    {
        public ReportKind Kind => ReportKind.TrialBalance;
        public ProvR Next = new() { IsProvisional = true, Warnings = new[] { "crosses Open period" } };
        public Task<ProvR> ExecuteAsync(ReportExecutionContext context, P parameters, CancellationToken ct = default)
            => Task.FromResult(Next);
    }

    private sealed class ThrowingCartridge : IReportCartridge<P, R>
    {
        public ReportKind Kind => ReportKind.TrialBalance;
        public Exception ToThrow = new InvalidOperationException("boom");
        public Task<R> ExecuteAsync(ReportExecutionContext context, P parameters, CancellationToken ct = default)
            => throw ToThrow;
    }

    private static (ReportRunner Runner, ReportCartridgeRegistry Registry, CountingMarker Markers, FakeTime Time) Build()
    {
        var registry = new ReportCartridgeRegistry();
        var markers = new CountingMarker();
        var time = new FakeTime();
        var opts = new ReportRunnerOptions();
        var runner = new ReportRunner(registry, markers, time, opts);
        return (runner, registry, markers, time);
    }

    [Fact]
    public async Task RunAsync_DispatchesToRegisteredCartridge()
    {
        var (runner, registry, _, _) = Build();
        var cartridge = new CapturingCartridge();
        registry.Register<P, R>(cartridge);
        var result = await runner.RunAsync<P, R>(ReportKind.TrialBalance, new P(), Tenant, Principal);
        Assert.True(result.Result.Sentinel);
    }

    [Fact]
    public async Task RunAsync_UnknownKind_ThrowsUnknownReportKindException()
    {
        var (runner, _, _, _) = Build();
        await Assert.ThrowsAsync<UnknownReportKindException>(() =>
            runner.RunAsync<P, R>(ReportKind.TrialBalance, new P(), Tenant, Principal));
    }

    [Fact]
    public async Task RunAsync_BindsTenantIdIntoContext()
    {
        var (runner, registry, _, _) = Build();
        var cartridge = new CapturingCartridge();
        registry.Register<P, R>(cartridge);
        await runner.RunAsync<P, R>(ReportKind.TrialBalance, new P(), Tenant, Principal);
        Assert.Equal(Tenant, cartridge.LastContext!.TenantId);
    }

    [Fact]
    public async Task RunAsync_CapturesSnapshotMarkerBeforeCartridgeInvocation()
    {
        var (runner, registry, markers, _) = Build();
        var cartridge = new CapturingCartridge();
        registry.Register<P, R>(cartridge);
        await runner.RunAsync<P, R>(ReportKind.TrialBalance, new P(), Tenant, Principal);
        Assert.Equal(1, markers.Calls);
        Assert.False(string.IsNullOrEmpty(cartridge.LastContext!.SnapshotMarker));
    }

    [Fact]
    public async Task RunAsync_BindsAsOfUtcFromClock()
    {
        var (runner, registry, _, time) = Build();
        var cartridge = new CapturingCartridge();
        registry.Register<P, R>(cartridge);
        await runner.RunAsync<P, R>(ReportKind.TrialBalance, new P(), Tenant, Principal);
        // First clock call (the started-at) was the original FakeTime.Current.
        Assert.Equal(new DateTimeOffset(2026, 5, 17, 12, 0, 0, TimeSpan.Zero), cartridge.LastContext!.AsOfUtc);
    }

    [Fact]
    public async Task RunAsync_BindsPrincipalIdIntoContext()
    {
        var (runner, registry, _, _) = Build();
        var cartridge = new CapturingCartridge();
        registry.Register<P, R>(cartridge);
        await runner.RunAsync<P, R>(ReportKind.TrialBalance, new P(), Tenant, Principal);
        Assert.Equal(Principal, cartridge.LastContext!.RequestedBy);
    }

    private sealed class CancellationObservingCartridge : IReportCartridge<P, R>
    {
        public ReportKind Kind => ReportKind.TrialBalance;
        public CancellationToken Observed;
        public Task<R> ExecuteAsync(ReportExecutionContext context, P parameters, CancellationToken ct = default)
        {
            Observed = ct;
            return Task.FromResult(new R());
        }
    }

    [Fact]
    public async Task RunAsync_PropagatesCancellationTokenToCartridge()
    {
        var (runner, registry, _, _) = Build();
        var cartridge = new CancellationObservingCartridge();
        registry.Register<P, R>(cartridge);
        using var cts = new CancellationTokenSource();
        await runner.RunAsync<P, R>(ReportKind.TrialBalance, new P(), Tenant, Principal, cts.Token);
        // The cartridge observed the exact token we passed in.
        Assert.Equal(cts.Token, cartridge.Observed);
    }

    [Fact]
    public async Task RunAsync_PopulatesRunDuration()
    {
        var (runner, registry, _, _) = Build();
        var cartridge = new CapturingCartridge();
        registry.Register<P, R>(cartridge);
        var result = await runner.RunAsync<P, R>(ReportKind.TrialBalance, new P(), Tenant, Principal);
        Assert.True(result.RunDuration > TimeSpan.Zero);
    }

    [Fact]
    public async Task RunAsync_PopulatesSnapshotMarkerInResult()
    {
        var (runner, registry, _, _) = Build();
        var cartridge = new CapturingCartridge();
        registry.Register<P, R>(cartridge);
        var result = await runner.RunAsync<P, R>(ReportKind.TrialBalance, new P(), Tenant, Principal);
        Assert.Equal(cartridge.LastContext!.SnapshotMarker, result.SnapshotMarker);
    }

    [Fact]
    public async Task RunAsync_ResultImplementingProvisionalityCarrier_PropagatesIsProvisionalAndWarnings()
    {
        var (runner, registry, _, _) = Build();
        registry.Register<P, ProvR>(new ProvisionalCartridge());
        var result = await runner.RunAsync<P, ProvR>(ReportKind.TrialBalance, new P(), Tenant, Principal);
        Assert.True(result.IsProvisional);
        Assert.Contains("crosses Open period", result.Warnings);
    }

    [Fact]
    public async Task RunAsync_ResultWithoutProvisionalityCarrier_DefaultsToFalseAndEmpty()
    {
        var (runner, registry, _, _) = Build();
        registry.Register<P, R>(new CapturingCartridge());
        var result = await runner.RunAsync<P, R>(ReportKind.TrialBalance, new P(), Tenant, Principal);
        Assert.False(result.IsProvisional);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task RunAsync_CartridgeThrows_WrapsInReportCartridgeExecutionException()
    {
        var (runner, registry, _, _) = Build();
        registry.Register<P, R>(new ThrowingCartridge());
        var ex = await Assert.ThrowsAsync<ReportCartridgeExecutionException>(() =>
            runner.RunAsync<P, R>(ReportKind.TrialBalance, new P(), Tenant, Principal));
        Assert.Equal(ReportKind.TrialBalance, ex.Kind);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public async Task RunAsync_ParameterValidationException_PassesThroughUnwrapped()
    {
        var (runner, registry, _, _) = Build();
        var paramEx = new ReportParameterValidationException("propertyId", "cross-tenant");
        registry.Register<P, R>(new ThrowingCartridge { ToThrow = paramEx });
        var thrown = await Assert.ThrowsAsync<ReportParameterValidationException>(() =>
            runner.RunAsync<P, R>(ReportKind.TrialBalance, new P(), Tenant, Principal));
        Assert.Same(paramEx, thrown);
    }
}
