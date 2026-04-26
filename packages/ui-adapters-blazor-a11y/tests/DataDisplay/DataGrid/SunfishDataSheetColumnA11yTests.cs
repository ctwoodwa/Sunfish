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
    /// TRIAGE 2026-04-26: KEEP-SKIPPED (definition-only). Registers with parent
    /// SunfishDataSheet via cascading IColumnHost. Renders no DOM in isolation.
    /// Unblocker: N/A — definition-only by design. Coverage flows through
    /// SunfishDataSheetA11yTests once parent fixture lands.
    /// See waves/cleanup/2026-04-26-followup-debt-audit.md §1c + §8.5.
    /// </remarks>
    [Fact(Skip = "KEEP-SKIPPED (definition-only): no isolated DOM by design. " +
        "Coverage flows through SunfishDataSheetA11yTests once parent fixture lands.")]
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
