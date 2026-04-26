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
using Sunfish.UIAdapters.Blazor.Components.Feedback;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Feedback.Progress;

public class SunfishChunkProgressBarA11yTests : IClassFixture<SunfishChunkProgressBarA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishChunkProgressBarA11yTests(Ctx ctx) => _ctx = ctx;

    [Fact]
    public async Task SunfishChunkProgressBar_TwoChunks_HasNoModeratePlusAxeViolations()
    {
        var rendered = _ctx.Bunit.Render<SunfishChunkProgressBar>(p => p
            .Add(c => c.Value, 40.0)
            .Add(c => c.ChunkCount, 5));

        var page = await _ctx.NewPageAsync();
        try
        {
            var result = await AxeRunner.RunAxeAsync(rendered.Markup, page);
            var moderatePlus = result.Violations
                .Where(v => v.Impact >= AxeImpact.Moderate)
                .ToList();
            Assert.True(moderatePlus.Count == 0,
                $"SunfishChunkProgressBar surfaced {moderatePlus.Count} moderate+ axe violation(s): " +
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
            Bunit.JSInterop.Mode = JSRuntimeMode.Loose;
        }

        public async Task<Microsoft.Playwright.IPage> NewPageAsync()
        {
            var host = await PlaywrightPageHost.GetAsync();
            return await host.NewPageAsync(new CultureInfo("en-US"));
        }

        public void Dispose() => Bunit.Dispose();
    }
}
