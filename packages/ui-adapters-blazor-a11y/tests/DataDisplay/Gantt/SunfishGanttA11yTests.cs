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
    /// TRIAGE 2026-04-26: FIX-LATER (tracked-fixture). SunfishGantt has a non-trivial
    /// generic + accessor + view contract surface (GanttView, GanttFieldAccessor,
    /// IGanttViewHost, dependency cascading). A minimal-but-realistic axe fixture requires
    /// a sample task graph, view configuration, and field-binding plumbing.
    /// Unblocker: extract tests/Fixtures/GanttFixture.cs from kitchen-sink demo (one PR;
    /// also unblocks SunfishGanttDependenciesA11yTests).
    /// Owner: Scheduling block team. ETA: post-Wave-2 cascade landing.
    /// See waves/cleanup/2026-04-26-followup-debt-audit.md §1c + §8.5.
    /// </remarks>
    [Fact(Skip = "FIX-LATER (tracked-fixture): needs GanttFixture (typed task graph + view). " +
        "See waves/cleanup/2026-04-26-followup-debt-audit.md §1c + §8.5.")]
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
