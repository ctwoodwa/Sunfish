using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Sunfish.UIAdapters.Blazor.Components.Forms.Containers;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Forms;

/// <summary>
/// Wave 4 cascade extension — bUnit-axe a11y harness for <see cref="SunfishForm"/>.
/// Renders the form bound to a simple model with a labelled child input so the
/// rendered <c>&lt;form&gt;</c> has interactive content for axe to evaluate; asserts
/// axe surfaces zero moderate+ violations.
/// </summary>
public class SunfishFormA11yTests : IClassFixture<SunfishFormA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishFormA11yTests(Ctx ctx) => _ctx = ctx;

    [Fact]
    public async Task SunfishForm_BoundModelWithLabelledInput_HasNoModeratePlusAxeViolations()
    {
        var model = new SampleModel { Name = "Jane" };

        var rendered = _ctx.Bunit.Render<SunfishForm>(p => p
            .Add(c => c.Model, model)
            .Add(c => c.Id, "sample-form")
            .AddChildContent(
                "<label for=\"name\">Name</label><input id=\"name\" type=\"text\" value=\"Jane\" />"));

        var page = await _ctx.NewPageAsync();
        try
        {
            var result = await AxeRunner.RunAxeAsync(rendered.Markup, page);
            var moderatePlus = result.Violations
                .Where(v => v.Impact >= AxeImpact.Moderate)
                .ToList();
            Assert.True(moderatePlus.Count == 0,
                $"SunfishForm surfaced {moderatePlus.Count} moderate+ axe violation(s): " +
                string.Join(", ", moderatePlus.Select(v => v.Id)));
        }
        finally { await page.CloseAsync(); }
    }

    private sealed class SampleModel
    {
        public string Name { get; set; } = string.Empty;
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
