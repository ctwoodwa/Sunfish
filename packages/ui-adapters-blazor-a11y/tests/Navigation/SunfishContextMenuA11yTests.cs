using System;
using System.Globalization;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NSubstitute;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Sunfish.UIAdapters.Blazor.Components.Navigation;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Navigation;

/// <summary>
/// Wave-2 cascade extension — bUnit-axe a11y harness for <see cref="SunfishContextMenu"/>.
/// SunfishContextMenu wires JS interop to a host element via a CSS selector; the menu
/// itself is portal-rendered after JS calls, so isolated bUnit DOM is empty until JS
/// runs. Coverage will land via a dedicated portal-aware fixture.
/// </summary>
public class SunfishContextMenuA11yTests : IClassFixture<SunfishContextMenuA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishContextMenuA11yTests(Ctx ctx) => _ctx = ctx;

    [Fact(Skip = "Requires complex fixture - tracked: ContextMenu portal-renders via JS interop")]
    public Task SunfishContextMenu_HasNoAxeViolations() => Task.CompletedTask;

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
