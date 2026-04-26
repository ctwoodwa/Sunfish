using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NSubstitute;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Sunfish.UIAdapters.Blazor.Components.DataDisplay;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.DataDisplay.DataGrid;

/// <summary>
/// Wave-2 cascade extension — bUnit-axe a11y harness for <see cref="SunfishDataGrid{TItem}"/>.
/// Renders the grid with an empty data set and the canonical aria-busy/role=grid surface
/// so axe can validate the structural ARIA pattern without requiring full dataset fixtures.
/// </summary>
public class SunfishDataGridA11yTests : IClassFixture<SunfishDataGridA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishDataGridA11yTests(Ctx ctx) => _ctx = ctx;

    /// <remarks>
    /// SunfishDataGrid has an [Inject] dependency on the internal IDownloadService
    /// (export-to-CSV/XLSX plumbing). bUnit can't resolve that service from the
    /// public ui-core/foundation surface; coverage will land once a dedicated
    /// DataGrid fixture (with the required interop services) is extracted.
    /// </remarks>
    [Fact(Skip = "Requires complex fixture - tracked: DataGrid needs IDownloadService injection")]
    public Task SunfishDataGrid_EmptyData_HasNoAxeViolations() => Task.CompletedTask;

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
