using System;
using System.Globalization;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NSubstitute;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Sunfish.UIAdapters.Blazor.Components.Layout;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Layout;

/// <summary>
/// SunfishCarousel + SunfishCarouselSlide require a multi-slide ChildContent fixture
/// and JS-interop autoplay/keyboard wiring; deferred to a dedicated carousel pass.
/// </summary>
public class SunfishCarouselA11yTests : IClassFixture<SunfishCarouselA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishCarouselA11yTests(Ctx ctx) => _ctx = ctx;

    [Fact(Skip = "Requires complex fixture - tracked: Carousel needs slide fixture + JS interop")]
    public Task SunfishCarousel_HasNoAxeViolations() => Task.CompletedTask;

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
