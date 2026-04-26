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

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Scheduling;

/// <summary>
/// Wave-2 cascade extension — bUnit-axe a11y harness for the
/// <c>SunfishGantt&lt;TItem&gt;</c> in the Scheduling namespace (distinct from the
/// DataDisplay/Gantt SunfishGantt covered in PR #113). The Scheduling Gantt is a
/// typed generic with non-trivial item fixtures — deferred.
/// </summary>
public class SunfishGanttA11yTests : IClassFixture<SunfishGanttA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishGanttA11yTests(Ctx ctx) => _ctx = ctx;

    [Fact(Skip = "Requires complex fixture - tracked: Scheduling/SunfishGantt<TItem> needs typed item shape")]
    public Task SunfishGanttScheduling_HasNoAxeViolations() => Task.CompletedTask;

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
