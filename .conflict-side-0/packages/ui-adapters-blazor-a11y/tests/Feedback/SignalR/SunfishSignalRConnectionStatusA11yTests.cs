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

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Feedback.SignalR;

/// <summary>
/// SunfishSignalRConnectionStatus and the SignalRConnectionPopup require a running
/// Microsoft.AspNetCore.SignalR.Client.HubConnection (or stub) plus realistic
/// connection-state events. Deferred to a dedicated SignalR fixture.
/// </summary>
public class SunfishSignalRConnectionStatusA11yTests : IClassFixture<SunfishSignalRConnectionStatusA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishSignalRConnectionStatusA11yTests(Ctx ctx) => _ctx = ctx;

    [Fact(Skip = "Requires complex fixture - tracked: needs HubConnection stub + state events")]
    public Task SunfishSignalRConnectionStatus_HasNoAxeViolations() => Task.CompletedTask;

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
