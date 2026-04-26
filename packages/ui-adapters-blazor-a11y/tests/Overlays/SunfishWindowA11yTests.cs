using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NSubstitute;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Sunfish.UIAdapters.Blazor.Components.Overlays;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Overlays;

/// <summary>
/// Wave-1 Plan 4 Cluster C — bUnit-axe a11y harness for <c>SunfishWindow</c>.
/// Renders the window in its visible non-modal shape with a title and explicit
/// content, hosts the markup in the Playwright bridge, and asserts axe-core surfaces
/// zero moderate+ violations.
/// </summary>
/// <remarks>
/// Per the brief, Overlays are tested in their <i>opened</i> state — for the window
/// that is <c>Visible=true</c>. Loose JS interop mode is required because
/// SunfishWindow initialises drag/resize JS in <c>OnAfterRenderAsync</c>; loose mode
/// auto-satisfies those calls so the synchronous markup pass completes intact.
/// Drag/resize and measurement service stubs are registered to satisfy the
/// component's <c>[Inject]</c> requirements without simulating real behaviour.
/// </remarks>
public class SunfishWindowA11yTests : IClassFixture<SunfishWindowA11yTests.Ctx>
{
    private readonly Ctx _ctx;

    public SunfishWindowA11yTests(Ctx ctx) => _ctx = ctx;

    [Fact]
    public async Task SunfishWindow_VisibleWithTitleAndContent_ZeroAxeViolations()
    {
        var rendered = _ctx.Bunit.Render<SunfishWindow>(p => p
            .Add(c => c.Visible, true)
            .Add(c => c.Modal, false)
            .Add(c => c.Draggable, false)
            .Add(c => c.Resizable, false)
            .Add(c => c.Title, "Sample Window")
            .Add(c => c.ContentTemplate, builder =>
            {
                builder.AddMarkupContent(0, "<p>Window body content for the a11y harness.</p>");
            }));

        var page = await _ctx.Host.NewPageAsync(new CultureInfo("en-US"));
        try
        {
            var result = await AxeRunner.RunAxeAsync(rendered.Markup, page);
            var moderatePlus = result.Violations
                .Where(v => v.Impact >= AxeImpact.Moderate)
                .ToList();

            Assert.True(moderatePlus.Count == 0,
                $"SunfishWindow surfaced {moderatePlus.Count} moderate+ axe violation(s): " +
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

            // SunfishWindow's drag/resize/measurement interop services are declared
            // internal to ui-adapters-blazor; this test assembly does not have an
            // InternalsVisibleTo entry, so we resolve the interface types through
            // reflection on the host assembly and register NSubstitute proxies
            // (DynamicProxyGenAssembly2 has IVT on ui-adapters-blazor, so the proxy
            // can implement the internal interface). Without these, Blazor's
            // DefaultComponentPropertyActivator throws on the [Inject] properties
            // even though they are nullable.
            RegisterInternalInteropService(
                "Sunfish.UIAdapters.Blazor.Internal.Interop.IElementMeasurementService");
            RegisterInternalInteropService(
                "Sunfish.UIAdapters.Blazor.Internal.Interop.IDragService");
            RegisterInternalInteropService(
                "Sunfish.UIAdapters.Blazor.Internal.Interop.IResizeInteractionService");

            // Loose JS interop satisfies the drag/resize module initialisation in
            // OnAfterRenderAsync so the markup pass completes; we test rendered DOM.
            Bunit.JSInterop.Mode = JSRuntimeMode.Loose;
        }

        private void RegisterInternalInteropService(string fullTypeName)
        {
            var serviceType = typeof(SunfishWindow).Assembly.GetType(fullTypeName, throwOnError: true)!;
            // NSubstitute.Substitute.For(Type[], object[]) exists for non-generic mocking.
            var substitute = NSubstitute.Substitute.For(new[] { serviceType }, Array.Empty<object>());
            Bunit.Services.AddSingleton(serviceType, substitute);
        }

        public async Task InitializeAsync() => Host = await PlaywrightPageHost.GetAsync();

        public Task DisposeAsync() => Task.CompletedTask;

        public void Dispose() => Bunit.Dispose();
    }
}
