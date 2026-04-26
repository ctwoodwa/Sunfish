using System;
using System.Globalization;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NSubstitute;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Utility;

/// <summary>
/// SunfishDiagram requires DiagramNode + DiagramConnection fixtures and JS interop;
/// deferred to a dedicated diagram fixture pass.
/// </summary>
public class SunfishDiagramA11yTests : IClassFixture<SunfishDiagramA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishDiagramA11yTests(Ctx ctx) => _ctx = ctx;

    [Fact(Skip = "Requires complex fixture - tracked: Diagram needs node+connection fixtures + JS interop")]
    public Task SunfishDiagram_HasNoAxeViolations() => Task.CompletedTask;

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
