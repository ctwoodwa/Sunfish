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

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.DataDisplay.DataGrid;

/// <summary>
/// Wave-2 cascade extension — bUnit-axe a11y harness for SunfishDataSheet&lt;TItem&gt;.
/// </summary>
public class SunfishDataSheetA11yTests : IClassFixture<SunfishDataSheetA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishDataSheetA11yTests(Ctx ctx) => _ctx = ctx;

    /// <remarks>
    /// SunfishDataSheet requires column definitions plus a typed row binding to render
    /// any meaningful markup. The cascade-extension brief allows this skip; coverage
    /// will land once a DataSheet sample fixture is extracted to tests/Fixtures.
    /// </remarks>
    [Fact(Skip = "Requires complex fixture - tracked: column definitions + typed rows")]
    public Task SunfishDataSheet_HasNoAxeViolations() => Task.CompletedTask;

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
