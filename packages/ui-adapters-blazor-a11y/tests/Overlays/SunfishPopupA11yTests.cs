using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Sunfish.UIAdapters.Blazor.Components.Overlays;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Overlays;

/// <summary>
/// Wave-1 Plan 4 Cluster C — bUnit-axe a11y harness for <c>SunfishPopup</c>.
/// Renders the popup in its <c>Visible=true + FocusTrap=true</c> shape (so it emits
/// <c>role="dialog"</c> + <c>aria-modal="true"</c> per the existing markup), hosts
/// the markup in the Playwright bridge, and asserts axe-core surfaces zero moderate+
/// violations.
/// </summary>
/// <remarks>
/// Per the brief, Overlays are tested in their <i>opened</i> state — for popups that
/// is <c>Visible=true</c>. <see cref="JSRuntimeMode.Loose"/> is required because the
/// component fires JS interop in <c>OnAfterRenderAsync</c> for anchor positioning;
/// loose mode lets bUnit auto-satisfy the calls so the markup pass completes.
/// </remarks>
public class SunfishPopupA11yTests : IClassFixture<SunfishPopupA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishPopupA11yTests(Ctx ctx) => _ctx = ctx;

    // axe violation: aria-dialog-name — SunfishPopup emits role="dialog" + aria-modal="true"
    // when FocusTrap=true but does not set aria-label or aria-labelledby on the popup root,
    // so axe (WCAG 4.1.2 Name, Role, Value) reports the dialog has no accessible name.
    // This is a real component bug; per Cluster C brief we mark it Skip and DO NOT fix here.
    [Fact(Skip = "axe violation: aria-dialog-name — popup root needs aria-label or aria-labelledby")]
    public async Task SunfishPopup_VisibleWithFocusTrap_ZeroAxeViolations()
    {
        var rendered = _ctx.Bunit.Render<SunfishPopup>(p => p
            .Add(c => c.Visible, true)
            .Add(c => c.FocusTrap, true)
            .AddChildContent(
                "<button type=\"button\">First action</button>" +
                "<button type=\"button\">Second action</button>"));

        var page = await _ctx.Host.NewPageAsync(new CultureInfo("en-US"));
        try
        {
            var result = await AxeRunner.RunAxeAsync(rendered.Markup, page);
            var moderatePlus = result.Violations
                .Where(v => v.Impact >= AxeImpact.Moderate)
                .ToList();

            Assert.True(moderatePlus.Count == 0,
                $"SunfishPopup surfaced {moderatePlus.Count} moderate+ axe violation(s): " +
                string.Join(", ", moderatePlus.Select(v => v.Id)));
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    public sealed class Ctx : IAsyncLifetime, IDisposable
    {
        public BunitContext Bunit { get; } = new();
        public PlaywrightPageHost Host { get; private set; } = null!;

        public Ctx()
        {
            Bunit.Services.AddSingleton(Substitute.For<ISunfishCssProvider>());
            Bunit.Services.AddSingleton(Substitute.For<ISunfishIconProvider>());
            Bunit.Services.AddSingleton(Substitute.For<ISunfishThemeService>());
            // SunfishPopup invokes JS in OnAfterRenderAsync for anchor positioning;
            // loose mode auto-satisfies the calls so render completes without throwing.
            Bunit.JSInterop.Mode = JSRuntimeMode.Loose;
        }

        public async Task InitializeAsync() => Host = await PlaywrightPageHost.GetAsync();

        public Task DisposeAsync() => Task.CompletedTask;

        public void Dispose() => Bunit.Dispose();
    }
}
