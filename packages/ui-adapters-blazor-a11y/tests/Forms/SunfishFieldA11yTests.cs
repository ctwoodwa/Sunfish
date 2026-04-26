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
/// Wave-1 Plan 4 Cluster C — bUnit-axe a11y harness for <c>SunfishField</c>.
/// Renders the field with a labelled input child (the realistic shape for a form
/// field), hosts the markup in the Playwright bridge, and asserts axe-core
/// produces zero moderate+ violations.
/// </summary>
/// <remarks>
/// SunfishField wraps a labelled control; rendering it without a child input would
/// not exercise the a11y contract that matters (label-input association, focus
/// target presence). Render params therefore include a <see cref="RenderFragment"/>
/// child that supplies the bound input with the matching <c>id</c>.
/// </remarks>
public class SunfishFieldA11yTests : IClassFixture<SunfishFieldA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishFieldA11yTests(Ctx ctx) => _ctx = ctx;

    [Fact]
    public async Task SunfishField_LabelledWithBoundInput_ZeroAxeViolations()
    {
        const string fieldId = "field-username";

        var rendered = _ctx.Bunit.Render<SunfishField>(p => p
            .Add(c => c.Text, "Username")
            .Add(c => c.Id, fieldId)
            .AddChildContent(
                $"<input id=\"{fieldId}\" type=\"text\" value=\"jane.doe\" />"));

        var page = await _ctx.Host.NewPageAsync(new CultureInfo("en-US"));
        try
        {
            var result = await AxeRunner.RunAxeAsync(rendered.Markup, page);
            var moderatePlus = result.Violations
                .Where(v => v.Impact >= AxeImpact.Moderate)
                .ToList();

            Assert.True(moderatePlus.Count == 0,
                $"SunfishField surfaced {moderatePlus.Count} moderate+ axe violation(s): " +
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
        }

        public async Task InitializeAsync() => Host = await PlaywrightPageHost.GetAsync();

        public Task DisposeAsync() => Task.CompletedTask;

        public void Dispose() => Bunit.Dispose();
    }
}
