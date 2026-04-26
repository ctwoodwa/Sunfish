using System;
using System.Globalization;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.DataDisplay.Gantt;

/// <summary>
/// Wave-2 cascade extension — bUnit-axe a11y harness for SunfishGanttDependencies&lt;TItem&gt;.
/// </summary>
public class SunfishGanttDependenciesA11yTests : IClassFixture<SunfishGanttDependenciesA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishGanttDependenciesA11yTests(Ctx ctx) => _ctx = ctx;

    /// <remarks>
    /// TRIAGE 2026-04-26: KEEP-SKIPPED (definition-only). SunfishGanttDependencies is a
    /// definition component that registers via cascading IGanttViewHost; cannot be rendered
    /// in isolation.
    /// Unblocker: N/A — definition-only by design. Coverage will flow through SunfishGantt
    /// harness once GanttFixture lands (parent test will cover dep markup too).
    /// See waves/cleanup/2026-04-26-followup-debt-audit.md §1c + §8.5.
    /// </remarks>
    [Fact(Skip = "KEEP-SKIPPED (definition-only): no isolated DOM by design (needs parent SunfishGantt host). " +
        "Coverage flows through SunfishGanttA11yTests once GanttFixture lands.")]
    public Task SunfishGanttDependencies_HasNoAxeViolations() => Task.CompletedTask;

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
