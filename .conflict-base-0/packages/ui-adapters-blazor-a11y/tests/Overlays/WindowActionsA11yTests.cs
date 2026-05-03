using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NSubstitute;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Sunfish.UIAdapters.Blazor.Components.Overlays;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Overlays;

/// <summary>
/// Wave-1 Plan 4 Cluster C — bUnit-axe a11y harness for <c>WindowActions</c>.
/// <c>WindowActions</c> is a registration-only child of <see cref="SunfishWindow"/>
/// (it throws when rendered without a parent), so the harness composes it inside a
/// host window with two action buttons — the realistic usage shape — and asserts
/// axe-core surfaces zero moderate+ violations against the resulting markup.
/// </summary>
/// <remarks>
/// The test exercises the <see cref="WindowActions"/> component-under-test by
/// validating the title-bar action region it produces inside the host window.
/// Loose JS interop accommodates the host window's drag/resize JS init.
/// </remarks>
public class WindowActionsA11yTests : IClassFixture<WindowActionsA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public WindowActionsA11yTests(Ctx ctx) => _ctx = ctx;

    [Fact]
    public async Task WindowActions_InsideVisibleWindow_ZeroAxeViolations()
    {
        // Compose host SunfishWindow with the WindowActions child + two action buttons.
        // The component-under-test (WindowActions) gates whether the parent renders
        // child-supplied action buttons vs. the enum-driven default set.
        var rendered = _ctx.Bunit.Render<SunfishWindow>(p => p
            .Add(c => c.Visible, true)
            .Add(c => c.Modal, false)
            .Add(c => c.Draggable, false)
            .Add(c => c.Resizable, false)
            .Add(c => c.Title, "Window with Custom Actions")
            .AddChildContent<WindowActions>(actions => actions
                .AddChildContent<WindowActionButton>(btn => btn
                    .Add(b => b.Name, "Close")
                    .Add(b => b.Title, "Close window"))
                .AddChildContent<WindowActionButton>(btn => btn
                    .Add(b => b.Name, "Minimize")
                    .Add(b => b.Title, "Minimize window"))));

        var page = await _ctx.Host.NewPageAsync(new CultureInfo("en-US"));
        try
        {
            var result = await AxeRunner.RunAxeAsync(rendered.Markup, page);
            var moderatePlus = result.Violations
                .Where(v => v.Impact >= AxeImpact.Moderate)
                .ToList();

            Assert.True(moderatePlus.Count == 0,
                $"WindowActions surfaced {moderatePlus.Count} moderate+ axe violation(s): " +
                string.Join(", ", moderatePlus.Select(v => v.Id)));
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    public sealed class Ctx : IAsyncLifetime, IDisposable
    {
        public BunitContext Bunit { get; } = new();
        public PlaywrightPageHost Host { get; private set; } = null!;

        public Ctx()
        {
            Bunit.Services.AddSingleton(Substitute.For<ISunfishCssProvider>());
            Bunit.Services.AddSingleton(Substitute.For<ISunfishIconProvider>());
            Bunit.Services.AddSingleton(Substitute.For<ISunfishThemeService>());

            // The host SunfishWindow injects internal drag/resize/measurement services;
            // resolve their interface types through reflection on ui-adapters-blazor and
            // register NSubstitute proxies (DynamicProxyGenAssembly2 has IVT on the host
            // assembly, so the proxy can implement the internal interface). Without
            // these registrations Blazor's activator throws on the [Inject] properties.
            RegisterInternalInteropService(
                "Sunfish.UIAdapters.Blazor.Internal.Interop.IElementMeasurementService");
            RegisterInternalInteropService(
                "Sunfish.UIAdapters.Blazor.Internal.Interop.IDragService");
            RegisterInternalInteropService(
                "Sunfish.UIAdapters.Blazor.Internal.Interop.IResizeInteractionService");

            Bunit.JSInterop.Mode = JSRuntimeMode.Loose;
        }

        private void RegisterInternalInteropService(string fullTypeName)
        {
            var serviceType = typeof(SunfishWindow).Assembly.GetType(fullTypeName, throwOnError: true)!;
            var substitute = NSubstitute.Substitute.For(new[] { serviceType }, Array.Empty<object>());
            Bunit.Services.AddSingleton(serviceType, substitute);
        }

        public async Task InitializeAsync() => Host = await PlaywrightPageHost.GetAsync();

        public Task DisposeAsync() => Task.CompletedTask;

        public void Dispose() => Bunit.Dispose();
    }
}
