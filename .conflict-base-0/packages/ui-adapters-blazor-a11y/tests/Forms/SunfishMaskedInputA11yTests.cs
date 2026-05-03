using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Sunfish.UIAdapters.Blazor.Components.Forms.Inputs;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Forms;

/// <summary>
/// Wave 4 cascade extension — bUnit-axe a11y harness for <see cref="SunfishMaskedInput"/>.
/// Renders the masked text input with a phone-number mask and accessible name supplied
/// via <c>aria-label</c>; asserts axe surfaces zero moderate+ violations.
/// </summary>
public class SunfishMaskedInputA11yTests : IClassFixture<SunfishMaskedInputA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishMaskedInputA11yTests(Ctx ctx) => _ctx = ctx;

    [Fact]
    public async Task SunfishMaskedInput_PhoneMaskWithAriaLabel_HasNoModeratePlusAxeViolations()
    {
        var rendered = _ctx.Bunit.Render<SunfishMaskedInput>(p => p
            .Add(c => c.Mask, "(000) 000-0000")
            .Add(c => c.Value, "5551234567")
            .Add(c => c.Placeholder, "Phone")
            .AddUnmatched("aria-label", "Phone number"));

        var page = await _ctx.NewPageAsync();
        try
        {
            var result = await AxeRunner.RunAxeAsync(rendered.Markup, page);
            var moderatePlus = result.Violations
                .Where(v => v.Impact >= AxeImpact.Moderate)
                .ToList();
            Assert.True(moderatePlus.Count == 0,
                $"SunfishMaskedInput surfaced {moderatePlus.Count} moderate+ axe violation(s): " +
                string.Join(", ", moderatePlus.Select(v => v.Id)));
        }
        finally { await page.CloseAsync(); }
    }

    public sealed class Ctx : IDisposable
    {
        public BunitContext Bunit { get; }

        public Ctx()
        {
            Bunit = new BunitContext();
            Bunit.Services.AddSingleton(Substitute.For<ISunfishCssProvider>());
            Bunit.Services.AddSingleton(Substitute.For<ISunfishIconProvider>());
            Bunit.Services.AddSingleton(Substitute.For<ISunfishThemeService>());
        }

        public async Task<Microsoft.Playwright.IPage> NewPageAsync()
        {
            var host = await PlaywrightPageHost.GetAsync();
            return await host.NewPageAsync(new CultureInfo("en-US"));
        }

        public void Dispose() => Bunit.Dispose();
    }
}
