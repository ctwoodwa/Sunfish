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
using Sunfish.UIAdapters.Blazor.Components.DataDisplay;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.DataDisplay.Gantt;

/// <summary>
/// Wave-2 cascade extension — bUnit-axe a11y harness for <see cref="SunfishGantt{TItem}"/>.
/// </summary>
public class SunfishGanttA11yTests : IClassFixture<SunfishGanttA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishGanttA11yTests(Ctx ctx) => _ctx = ctx;

    /// <remarks>
    /// SunfishGantt has a non-trivial generic + accessor + view contract surface
    /// (GanttView, GanttFieldAccessor, IGanttViewHost, dependency cascading). Building
    /// a minimal-but-realistic fixture for axe coverage requires a sample task graph,
    /// view configuration, and field-binding plumbing that lives in the Sunfish kitchen
    /// sink. The cascade-extension brief explicitly allows this skip; coverage will be
    /// completed once a Gantt fixture is extracted into tests/Fixtures.
    /// </remarks>
    [Fact(Skip = "Requires complex fixture - tracked: Gantt needs typed task graph + view")]
    public Task SunfishGantt_HasNoAxeViolations() => Task.CompletedTask;

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
