using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sunfish.Foundation.Enums;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Sunfish.UIAdapters.Blazor.Components.Forms.Containers;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Forms;

/// <summary>
/// Wave 4 cascade extension — bUnit-axe a11y harness for <see cref="SunfishValidation"/>.
/// Renders the simple message wrapper with an Error severity and a sample message;
/// asserts axe surfaces zero moderate+ violations.
/// </summary>
public class SunfishValidationA11yTests : IClassFixture<SunfishValidationA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishValidationA11yTests(Ctx ctx) => _ctx = ctx;

    [Theory]
    [InlineData(ValidationSeverity.Error)]
    [InlineData(ValidationSeverity.Warning)]
    [InlineData(ValidationSeverity.Info)]
    public async Task SunfishValidation_HasNoModeratePlusAxeViolations(ValidationSeverity severity)
    {
        var rendered = _ctx.Bunit.Render<SunfishValidation>(p => p
            .Add(c => c.Severity, severity)
            .Add(c => c.Message, "Email address is required."));

        var page = await _ctx.NewPageAsync();
        try
        {
            var result = await AxeRunner.RunAxeAsync(rendered.Markup, page);
            var moderatePlus = result.Violations
                .Where(v => v.Impact >= AxeImpact.Moderate)
                .ToList();
            Assert.True(moderatePlus.Count == 0,
                $"SunfishValidation surfaced {moderatePlus.Count} moderate+ axe violation(s): " +
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
