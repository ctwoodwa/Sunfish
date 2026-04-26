using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NSubstitute;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Sunfish.UIAdapters.Blazor.Components.Overlays;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Overlays;

/// <summary>
/// bUnit-axe a11y harness for <see cref="SunfishPopup"/>.
/// <para>
/// Covers the fix for axe rule <c>aria-dialog-name</c> (WCAG 4.1.2, Serious): when
/// <see cref="SunfishPopup.FocusTrap"/> is <c>true</c>, the popup root emits
/// <c>role="dialog"</c> + <c>aria-modal="true"</c> and so must also expose an accessible
/// name via <c>aria-label</c>. The new <c>Title</c> parameter wires that name through.
/// </para>
/// </summary>
public class SunfishPopupA11yTests : IClassFixture<SunfishPopupA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishPopupA11yTests(Ctx ctx) => _ctx = ctx;

    [Fact]
    public async Task FocusTrap_WithTitle_HasNoAriaDialogNameViolation()
    {
        var rendered = _ctx.Bunit.Render<SunfishPopup>(p => p
            .Add(c => c.Visible, true)
            .Add(c => c.FocusTrap, true)
            .Add(c => c.Title, "Filter rows")
            .AddChildContent("<button>OK</button>"));

        await AssertNoModerateAxeViolationsAsync(rendered.Markup, "focus-trap+title");
    }

    [Fact]
    public async Task NoFocusTrap_HasNoAxeViolations()
    {
        // Without FocusTrap, no role="dialog" is emitted, so aria-dialog-name does not apply
        // and Title is not required.
        var rendered = _ctx.Bunit.Render<SunfishPopup>(p => p
            .Add(c => c.Visible, true)
            .AddChildContent("<button>OK</button>"));

        await AssertNoModerateAxeViolationsAsync(rendered.Markup, "no-focus-trap");
    }

    [Fact]
    public void FocusTrap_WithoutTitle_FallsBackToDialogLabel_DoesNotThrow()
    {
        // Per fix brief: the gentler default is to fall back to aria-label="Dialog" and
        // emit a console warning rather than throw. This keeps existing consumers rendering
        // while the violation is paid down. The fallback also satisfies aria-dialog-name.
        var rendered = _ctx.Bunit.Render<SunfishPopup>(p => p
            .Add(c => c.Visible, true)
            .Add(c => c.FocusTrap, true)
            .AddChildContent("<button>OK</button>"));

        Assert.Contains("aria-label=\"Dialog\"", rendered.Markup);
        Assert.Contains("role=\"dialog\"", rendered.Markup);
        Assert.Contains("aria-modal=\"true\"", rendered.Markup);
    }

    private async Task AssertNoModerateAxeViolationsAsync(string markup, string scenario)
    {
        var page = await _ctx.Host.NewPageAsync(new CultureInfo("en-US"));
        try
        {
            var result = await AxeRunner.RunAxeAsync(markup, page);
            var moderatePlus = result.Violations
                .Where(v => v.Impact >= AxeImpact.Moderate)
                .ToList();
            Assert.True(moderatePlus.Count == 0,
                $"Popup[{scenario}] surfaced {moderatePlus.Count} moderate+ axe violation(s): " +
                string.Join(", ", moderatePlus.Select(v => v.Id)));
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    public sealed class Ctx : IAsyncLifetime
    {
        public BunitContext Bunit { get; }
        public PlaywrightPageHost Host { get; private set; } = null!;

        public Ctx()
        {
            Bunit = new BunitContext();
            Bunit.Services.AddSingleton(Substitute.For<ISunfishCssProvider>());
            Bunit.Services.AddSingleton(Substitute.For<ISunfishIconProvider>());
            Bunit.Services.AddSingleton(Substitute.For<ISunfishThemeService>());
            Bunit.JSInterop.Mode = JSRuntimeMode.Loose;
        }

        public async Task InitializeAsync()
        {
            Host = await PlaywrightPageHost.GetAsync();
        }

        public Task DisposeAsync()
        {
            Bunit.Dispose();
            return Task.CompletedTask;
        }
    }
}
