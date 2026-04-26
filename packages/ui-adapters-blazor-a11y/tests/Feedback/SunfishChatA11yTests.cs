using System;
using System.Globalization;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NSubstitute;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Sunfish.UIAdapters.Blazor.Components.Feedback;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Feedback;

/// <summary>
/// Wave-2 cascade extension — bUnit-axe a11y harness for the
/// <c>SunfishChat</c> in the Feedback namespace (distinct from the AI namespace
/// SunfishChat already covered in tests/AI/SunfishChatA11yTests.cs). The Feedback
/// chat shape requires a non-trivial fixture to test with messages.
/// </summary>
public class SunfishChatA11yTests : IClassFixture<SunfishChatA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishChatA11yTests(Ctx ctx) => _ctx = ctx;

    [Fact(Skip = "Requires complex fixture - tracked: Feedback/SunfishChat needs ChatMessage list shape")]
    public Task SunfishChatFeedback_HasNoAxeViolations() => Task.CompletedTask;

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
