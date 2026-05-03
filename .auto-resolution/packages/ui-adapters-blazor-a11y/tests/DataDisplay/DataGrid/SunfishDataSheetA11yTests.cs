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
    /// TRIAGE 2026-04-26: FIX-LATER (tracked-fixture). SunfishDataSheet requires column
    /// definitions plus a typed row binding to render meaningful markup.
    /// Unblocker: tests/Fixtures/DataSheetFixture.cs with sample columns + typed rows
    /// (sibling to DataGridFixture; same workstream).
    /// Owner: DataGrid block team. ETA: post-Wave-2 cascade landing.
    /// See waves/cleanup/2026-04-26-followup-debt-audit.md §1c + §8.5.
    /// </remarks>
    [Fact(Skip = "FIX-LATER (tracked-fixture): needs DataSheetFixture (columns + typed rows). " +
        "See waves/cleanup/2026-04-26-followup-debt-audit.md §1c + §8.5.")]
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
