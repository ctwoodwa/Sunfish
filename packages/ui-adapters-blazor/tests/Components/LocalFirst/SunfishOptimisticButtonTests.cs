using System;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sunfish.Foundation.Configuration;
using Sunfish.Foundation.Services;
using Sunfish.UIAdapters.Blazor.Components.LocalFirst;
using Sunfish.UIAdapters.Blazor.Internal.Interop;
using Sunfish.UICore.Contracts;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.Tests.Components.LocalFirst;

public class SunfishOptimisticButtonTests : BunitContext
{
    public SunfishOptimisticButtonTests()
    {
        Services.AddSingleton<SunfishOptions>();
        Services.AddScoped<ISunfishThemeService, SunfishThemeService>();
        Services.AddScoped<ISunfishCssProvider, StubCssProvider>();
        Services.AddScoped<ISunfishIconProvider, StubIconProvider>();
        Services.AddScoped<ISunfishJsModuleLoader>(
            _ => Substitute.For<ISunfishJsModuleLoader>());
    }

    [Fact]
    public void IdleState_RendersIdleClass_AndLabel()
    {
        var cut = Render<SunfishOptimisticButton>(p => p
            .Add(x => x.Label, "Save record"));

        Assert.Contains("sf-optimistic-button--idle", cut.Markup);
        Assert.Contains("Save record", cut.Markup);
    }

    [Fact]
    public void ExplicitPendingState_Overrides_InternalState()
    {
        var cut = Render<SunfishOptimisticButton>(p => p
            .Add(x => x.State, OptimisticButtonState.Pending));

        Assert.Contains("sf-optimistic-button--pending", cut.Markup);
        Assert.Equal(OptimisticButtonState.Pending, cut.Instance.CurrentState);
    }

    [Fact]
    public async Task OnClick_SuccessfulWrite_TransitionsPending_ThenConfirmed()
    {
        var tcs = new TaskCompletionSource<bool>();
        var cut = Render<SunfishOptimisticButton>(p => p
            .Add(x => x.OnClick, () => tcs.Task)
            .Add(x => x.ConfirmedRevertDelay, TimeSpan.Zero));

        // Click: fires the async handler. Don't await the click task —
        // the handler is suspended on tcs.Task, and awaiting would deadlock.
        _ = cut.Find("button").ClickAsync(new());

        // Button should now be in Pending state.
        cut.WaitForAssertion(
            () => Assert.Equal(OptimisticButtonState.Pending, cut.Instance.CurrentState));

        // Let the handler complete successfully.
        tcs.SetResult(true);

        // Without a revert delay the state machine flows Pending → Confirmed → Idle
        // immediately. Either Confirmed or Idle is an acceptable end state.
        cut.WaitForAssertion(() =>
        {
            Assert.NotEqual(OptimisticButtonState.Pending, cut.Instance.CurrentState);
            Assert.NotEqual(OptimisticButtonState.Failed,  cut.Instance.CurrentState);
        });

        await Task.CompletedTask;
    }

    [Fact]
    public void OnClick_ReturningFalse_TransitionsTo_Failed()
    {
        var cut = Render<SunfishOptimisticButton>(p => p
            .Add(x => x.OnClick, () => Task.FromResult(false)));

        cut.Find("button").Click();

        cut.WaitForAssertion(
            () => Assert.Equal(OptimisticButtonState.Failed, cut.Instance.CurrentState));
        Assert.Contains("sf-optimistic-button--failed", cut.Markup);
    }

    [Fact]
    public void OnClick_ThrowingException_TransitionsTo_Failed()
    {
        var cut = Render<SunfishOptimisticButton>(p => p
            .Add(x => x.OnClick,
                () => throw new InvalidOperationException("boom")));

        cut.Find("button").Click();

        cut.WaitForAssertion(
            () => Assert.Equal(OptimisticButtonState.Failed, cut.Instance.CurrentState));
    }

    [Fact]
    public void DisabledPropertyTrue_DisablesButton()
    {
        var cut = Render<SunfishOptimisticButton>(p => p
            .Add(x => x.Disabled, true));

        var button = cut.Find("button");
        Assert.True(button.HasAttribute("disabled"));
    }
}
