using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using NSubstitute;
using Sunfish.UIAdapters.Blazor.A11y;
using Sunfish.UIAdapters.Blazor.Maui;
using Sunfish.UICore.Conformance;
using Sunfish.UICore.Primitives;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.Tests;

/// <summary>
/// W#46 Phase 4 — 6 cross-adapter integration tests per the Stage 06 hand-off.
/// Tests 1–3: Blazor JS interop adapters.
/// Tests 4–5: MAUI simulated-platform adapters.
/// Test 6: DefaultConformanceRegistry (shared adapter layer).
/// </summary>
public class BlazorLiveAnnouncerTests
{
    /// <summary>Test 1 — Announce(Polite) invokes JS with "polite".</summary>
    [Fact]
    [Trait("Category", "A11yBindings")]
    public async Task BlazorLiveAnnouncer_Announce_Polite_InvokesJsWithPolite()
    {
        var (js, module) = BuildJsMock();
        var announcer = new BlazorLiveAnnouncer(js);

        // Use internal awaitable to avoid fire-and-forget timing in tests.
        await announcer.AnnounceAsync("Hello screen reader", LiveRegionPoliteness.Polite);

        AssertAnnounceCall(module, "Hello screen reader", "polite");
    }

    /// <summary>Test 2 — Announce(Assertive) invokes JS with "assertive".</summary>
    [Fact]
    [Trait("Category", "A11yBindings")]
    public async Task BlazorLiveAnnouncer_Announce_Assertive_InvokesJsWithAssertive()
    {
        var (js, module) = BuildJsMock();
        var announcer = new BlazorLiveAnnouncer(js);

        await announcer.AnnounceAsync("Important update", LiveRegionPoliteness.Assertive);

        AssertAnnounceCall(module, "Important update", "assertive");
    }

    private static (IJSRuntime js, IJSObjectReference module) BuildJsMock()
    {
        var module = Substitute.For<IJSObjectReference>();
        var js = Substitute.For<IJSRuntime>();
        js.InvokeAsync<IJSObjectReference>(Arg.Any<string>(), Arg.Any<object?[]?>())
          .Returns(new ValueTask<IJSObjectReference>(module));
        return (js, module);
    }

    // InvokeVoidAsync is an extension method — NSubstitute records the underlying
    // InvokeAsync<IJSVoidResult> call. Use ReceivedCalls() to inspect args directly
    // and avoid array reference-equality mismatches in Arg matchers.
    private static void AssertAnnounceCall(
        IJSObjectReference module, string expectedMessage, string expectedPoliteness)
    {
        var calls = module.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name.Contains("InvokeAsync") &&
                        c.GetArguments().Length > 0 &&
                        c.GetArguments()[0]?.ToString() == "announce")
            .ToList();

        Assert.True(calls.Count > 0, $"Expected JS 'announce' call; received {module.ReceivedCalls().Count()} total calls.");

        // Args array from InvokeVoidAsync(id, CT, params args[]) → InvokeAsync<T>(id, CT, args[])
        // position 0 = identifier, 1 = CancellationToken, 2 = object?[] args
        var callArgs = calls[0].GetArguments();
        var argsArray = callArgs.Length > 2 ? callArgs[2] as object?[] : null;
        Assert.NotNull(argsArray);
        Assert.Equal(expectedMessage,    argsArray![0]?.ToString());
        Assert.Equal(expectedPoliteness, argsArray[1]?.ToString());
    }
}

public class BlazorFocusTrapTests
{
    /// <summary>Test 3 — EnterAsync then ExitAsync: no exception; JS interop observable.</summary>
    [Fact]
    [Trait("Category", "A11yBindings")]
    public async Task BlazorFocusTrap_EnterThenExit_NoException_InteropObservable()
    {
        var module = Substitute.For<IJSObjectReference>();
        var js = Substitute.For<IJSRuntime>();
        js.InvokeAsync<IJSObjectReference>(Arg.Any<string>(), Arg.Any<object?[]?>())
          .Returns(new ValueTask<IJSObjectReference>(module));

        var trap = new BlazorFocusTrap(js) { ContainerId = "test-container" };

        await trap.EnterAsync();
        await trap.ExitAsync();

        // Verify 'trapFocus' and 'releaseFocus' were called via ReceivedCalls inspection
        // (InvokeVoidAsync is an extension method; NSubstitute records InvokeAsync<T>).
        AssertJsCall(module, "trapFocus",    "test-container");
        AssertJsCall(module, "releaseFocus", "test-container");
    }

    private static void AssertJsCall(
        IJSObjectReference module, string identifier, string expectedArg)
    {
        var calls = module.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name.Contains("InvokeAsync") &&
                        c.GetArguments().Length > 0 &&
                        c.GetArguments()[0]?.ToString() == identifier)
            .ToList();

        Assert.True(calls.Count > 0,
            $"Expected JS '{identifier}' call; received calls: " +
            string.Join(", ", module.ReceivedCalls().Select(c =>
                c.GetMethodInfo().Name + "(" + c.GetArguments()[0] + ")")));

        // InvokeVoidAsync(id, CT, params args) → InvokeAsync<T>(id, CT, args[])
        // position: 0=id, 1=CT, 2=args[]
        var callArgs = calls[0].GetArguments();
        if (callArgs.Length > 2 && callArgs[2] is object?[] argsArray)
            Assert.Contains(expectedArg, argsArray.Select(a => a?.ToString()));
    }
}

public class MauiLiveAnnouncerTests
{
    /// <summary>Test 4 — Simulated Windows: IPlatformA11yNotifier.Notify called with message.</summary>
    [Fact]
    [Trait("Category", "A11yBindings")]
    public void MauiLiveAnnouncer_Announce_SimulatedWindows_NotifierCalled()
    {
        string? capturedMessage = null;
        LiveRegionPoliteness? capturedPoliteness = null;
        var windowsNotifier = new RecordingA11yNotifier(
            (msg, pol) => { capturedMessage = msg; capturedPoliteness = pol; });

        var announcer = new MauiLiveAnnouncer(windowsNotifier);
        announcer.Announce("Deploy complete", LiveRegionPoliteness.Polite);

        Assert.Equal("Deploy complete", capturedMessage);
        Assert.Equal(LiveRegionPoliteness.Polite, capturedPoliteness);
    }

    /// <summary>Test 5 — Simulated MacCatalyst: IPlatformA11yNotifier.Notify called with critical message.</summary>
    [Fact]
    [Trait("Category", "A11yBindings")]
    public void MauiLiveAnnouncer_Announce_SimulatedMacCatalyst_CriticalPolitenessPreserved()
    {
        string? capturedMessage = null;
        LiveRegionPoliteness? capturedPoliteness = null;
        var macNotifier = new RecordingA11yNotifier(
            (msg, pol) => { capturedMessage = msg; capturedPoliteness = pol; });

        var announcer = new MauiLiveAnnouncer(macNotifier);
        announcer.Announce("Security warning", LiveRegionPoliteness.Critical);

        Assert.Equal("Security warning", capturedMessage);
        Assert.Equal(LiveRegionPoliteness.Critical, capturedPoliteness);
    }

    private sealed class RecordingA11yNotifier : IPlatformA11yNotifier
    {
        private readonly Action<string, LiveRegionPoliteness> _record;
        public RecordingA11yNotifier(Action<string, LiveRegionPoliteness> record) => _record = record;
        public void Notify(string message, LiveRegionPoliteness politeness) => _record(message, politeness);
    }
}

public class DefaultConformanceRegistryTests
{
    /// <summary>Test 6 — Register then ForLocation returns the declared surface at AA level.</summary>
    [Fact]
    [Trait("Category", "ConformanceCoverage")]
    public void DefaultConformanceRegistry_RegisterAndQuery_ReturnsSurfaceAtAALevel()
    {
        var registry = new DefaultConformanceRegistry();
        var declaration = new ConformanceDeclaration(
            LocationId: "quarterdeck",
            SurfaceId: "watch-banner",
            Level: Wcag22Level.AA,
            Covered: new[] { new WcagSuccessCriterion("4.1.3", "Status Messages") },
            Chapters: new[] { new En301549Chapter("9.4.1.3", "Status Messages") },
            Exceptions: Array.Empty<ConformanceException>(),
            DeclaredAt: DateTimeOffset.UtcNow);

        registry.Register(declaration);
        var results = registry.ForLocation("quarterdeck");

        Assert.Single(results);
        Assert.Equal("watch-banner", results[0].SurfaceId);
        Assert.Equal(Wcag22Level.AA, results[0].Level);
    }

    [Fact]
    [Trait("Category", "ConformanceCoverage")]
    public void DefaultConformanceRegistry_ReRegister_OverwritesPriorEntry()
    {
        var registry = new DefaultConformanceRegistry();
        var d1 = MakeDeclaration("bridge", "nav-bar", Wcag22Level.A);
        var d2 = MakeDeclaration("bridge", "nav-bar", Wcag22Level.AA);

        registry.Register(d1);
        registry.Register(d2);

        var results = registry.ForLocation("bridge");
        Assert.Single(results);
        Assert.Equal(Wcag22Level.AA, results[0].Level);
    }

    [Fact]
    [Trait("Category", "ConformanceCoverage")]
    public void DefaultConformanceRegistry_ForLocation_CaseInsensitive()
    {
        var registry = new DefaultConformanceRegistry();
        registry.Register(MakeDeclaration("SickBay", "vitals-panel", Wcag22Level.AA));

        Assert.Single(registry.ForLocation("sickbay"));
        Assert.Single(registry.ForLocation("SICKBAY"));
    }

    private static ConformanceDeclaration MakeDeclaration(
        string locationId, string surfaceId, Wcag22Level level) =>
        new(locationId, surfaceId, level,
            Array.Empty<WcagSuccessCriterion>(),
            Array.Empty<En301549Chapter>(),
            Array.Empty<ConformanceException>(),
            DateTimeOffset.UtcNow);
}
