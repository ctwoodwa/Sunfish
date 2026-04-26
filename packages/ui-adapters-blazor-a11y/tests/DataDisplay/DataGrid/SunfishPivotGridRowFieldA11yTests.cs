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
/// Wave-2 cascade extension — bUnit-axe a11y harness for SunfishPivotGridRowField.
/// </summary>
public class SunfishPivotGridRowFieldA11yTests : IClassFixture<SunfishPivotGridRowFieldA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishPivotGridRowFieldA11yTests(Ctx ctx) => _ctx = ctx;

    /// <remarks>
    /// Definition-only field component; registers via cascading IPivotGridFieldHost and
    /// renders no DOM in isolation. Coverage flows through SunfishPivotGrid harness.
    /// </remarks>
    [Fact(Skip = "Requires complex fixture - tracked: definition-only, no isolated DOM")]
    public Task SunfishPivotGridRowField_HasNoAxeViolations() => Task.CompletedTask;

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
