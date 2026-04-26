using System;
using System.Globalization;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.DataDisplay.DataGrid;

/// <summary>
/// Wave-2 cascade extension — bUnit-axe a11y harness for SunfishDataSheetColumn&lt;TItem&gt;.
/// </summary>
public class SunfishDataSheetColumnA11yTests : IClassFixture<SunfishDataSheetColumnA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishDataSheetColumnA11yTests(Ctx ctx) => _ctx = ctx;

    /// <remarks>
    /// Definition-only component — registers itself with the parent SunfishDataSheet via
    /// cascading IColumnHost. Renders no DOM in isolation; coverage flows through the
    /// parent SunfishDataSheet harness once that fixture lands.
    /// </remarks>
    [Fact(Skip = "Requires complex fixture - tracked: definition-only, no isolated DOM")]
    public Task SunfishDataSheetColumn_HasNoAxeViolations() => Task.CompletedTask;

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
