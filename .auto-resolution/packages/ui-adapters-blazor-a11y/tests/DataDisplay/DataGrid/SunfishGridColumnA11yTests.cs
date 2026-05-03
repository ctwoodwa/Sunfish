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
/// Wave-2 cascade extension — bUnit-axe a11y harness for SunfishGridColumn&lt;TItem&gt;.
/// </summary>
public class SunfishGridColumnA11yTests : IClassFixture<SunfishGridColumnA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishGridColumnA11yTests(Ctx ctx) => _ctx = ctx;

    /// <remarks>
    /// TRIAGE 2026-04-26: KEEP-SKIPPED (definition-only). Component registers itself with
    /// the parent SunfishDataGrid via cascading IColumnHost — it cannot render DOM in
    /// isolation. Real coverage will flow through SunfishDataGridA11yTests once that
    /// fixture lands; this placeholder exists for symmetric component inventory.
    /// Unblocker: N/A — definition-only by design. The placeholder may be removed entirely
    /// once parent harness covers the cascaded markup.
    /// See waves/cleanup/2026-04-26-followup-debt-audit.md §1c + §8.5.
    /// </remarks>
    [Fact(Skip = "KEEP-SKIPPED (definition-only): no isolated DOM by design. " +
        "Coverage flows through SunfishDataGridA11yTests once parent fixture lands.")]
    public Task SunfishGridColumn_HasNoAxeViolations() => Task.CompletedTask;

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
