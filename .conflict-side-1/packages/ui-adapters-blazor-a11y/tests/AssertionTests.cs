using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Sunfish.UIAdapters.Blazor.A11y;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests;

/// <summary>
/// Tests for <see cref="SunfishA11yAssertions"/>. Uses raw HTML loaded via
/// <see cref="IPage.SetContentAsync"/> to decouple from bUnit / Blazor circuit
/// machinery — these tests assert the assertion methods themselves work, not the
/// component rendering pipeline.
/// </summary>
public class AssertionTests
{
    private static async Task<IPage> NewPageAsync()
    {
        var host = await PlaywrightPageHost.GetAsync();
        return await host.NewPageAsync(new CultureInfo("en-US"));
    }

    [Fact]
    public async Task AssertFocusInitialAsync_PassesWhenSelectorMatchesActiveElement()
    {
        var page = await NewPageAsync();
        try
        {
            await page.SetContentAsync("""
                <!doctype html><html lang="en"><head><meta charset="utf-8"><title>t</title></head>
                <body><main><button id="b">Click</button>
                <script>document.getElementById('b').focus();</script>
                </main></body></html>
                """);

            await SunfishA11yAssertions.AssertFocusInitialAsync(page, "#b");
        }
        finally { await page.CloseAsync(); }
    }

    [Fact]
    public async Task AssertFocusInitialAsync_ThrowsWhenSelectorNotFocused()
    {
        var page = await NewPageAsync();
        try
        {
            await page.SetContentAsync("""
                <!doctype html><html lang="en"><head><meta charset="utf-8"><title>t</title></head>
                <body><main><button id="a">A</button><button id="b">B</button>
                <script>document.getElementById('a').focus();</script>
                </main></body></html>
                """);

            await Assert.ThrowsAsync<A11yAssertionException>(
                () => SunfishA11yAssertions.AssertFocusInitialAsync(page, "#b"));
        }
        finally { await page.CloseAsync(); }
    }

    [Fact]
    public async Task AssertFocusInitialAsync_NoneAcceptsBodyFocus()
    {
        var page = await NewPageAsync();
        try
        {
            await page.SetContentAsync("""
                <!doctype html><html lang="en"><head><meta charset="utf-8"><title>t</title></head>
                <body><main><button>just a button</button></main></body></html>
                """);

            await SunfishA11yAssertions.AssertFocusInitialAsync(page, "none");
        }
        finally { await page.CloseAsync(); }
    }

    [Fact]
    public async Task AssertFocusTrapAsync_PassesWhenFocusCyclesWithinContainer()
    {
        var page = await NewPageAsync();
        try
        {
            // A focus-trap implementation: cycle Tab back to first child on the way out.
            await page.SetContentAsync("""
                <!doctype html><html lang="en"><head><meta charset="utf-8"><title>t</title></head>
                <body><main>
                <div id="trap">
                  <button id="b1">B1</button>
                  <button id="b2">B2</button>
                  <button id="b3">B3</button>
                </div>
                <script>
                  const trap = document.getElementById('trap');
                  const focusables = trap.querySelectorAll('button');
                  trap.addEventListener('keydown', (e) => {
                    if (e.key !== 'Tab') return;
                    const first = focusables[0];
                    const last = focusables[focusables.length - 1];
                    if (e.shiftKey && document.activeElement === first) {
                      e.preventDefault(); last.focus();
                    } else if (!e.shiftKey && document.activeElement === last) {
                      e.preventDefault(); first.focus();
                    }
                  });
                  focusables[0].focus();
                </script>
                </main></body></html>
                """);

            await SunfishA11yAssertions.AssertFocusTrapAsync(page, "#trap");
        }
        finally { await page.CloseAsync(); }
    }

    [Fact]
    public async Task AssertFocusTrapAsync_ThrowsWhenFocusEscapes()
    {
        var page = await NewPageAsync();
        try
        {
            // No trap installed; focus will escape the container after the last Tab.
            await page.SetContentAsync("""
                <!doctype html><html lang="en"><head><meta charset="utf-8"><title>t</title></head>
                <body><main>
                <div id="trap">
                  <button id="b1">B1</button>
                  <button id="b2">B2</button>
                </div>
                <button id="outside">Outside</button>
                <script>document.getElementById('b1').focus();</script>
                </main></body></html>
                """);

            await Assert.ThrowsAsync<A11yAssertionException>(
                () => SunfishA11yAssertions.AssertFocusTrapAsync(page, "#trap"));
        }
        finally { await page.CloseAsync(); }
    }

    [Fact]
    public async Task AssertKeyboardMapAsync_PassesWhenKeyFiresExpectedAction()
    {
        var page = await NewPageAsync();
        try
        {
            await page.SetContentAsync("""
                <!doctype html><html lang="en"><head><meta charset="utf-8"><title>t</title></head>
                <body data-sunfish-fired="">
                <main><button id="host">host</button>
                <script>
                  document.body.addEventListener('keydown', (e) => {
                    if (e.key === 'Enter') {
                      const prior = document.body.getAttribute('data-sunfish-fired') ?? '';
                      document.body.setAttribute('data-sunfish-fired', prior + ' activate');
                    }
                  });
                  document.getElementById('host').focus();
                </script>
                </main></body></html>
                """);

            var bindings = new[]
            {
                new KeyboardBinding { Keys = new[] { "Enter" }, Action = "activate" },
            };

            await SunfishA11yAssertions.AssertKeyboardMapAsync(page, bindings, "body");
        }
        finally { await page.CloseAsync(); }
    }

    [Fact]
    public async Task AssertKeyboardMapAsync_ThrowsWhenActionNotFired()
    {
        var page = await NewPageAsync();
        try
        {
            // Handler exists for Enter but doesn't update data-sunfish-fired.
            await page.SetContentAsync("""
                <!doctype html><html lang="en"><head><meta charset="utf-8"><title>t</title></head>
                <body data-sunfish-fired="">
                <main><button id="host">host</button>
                <script>document.getElementById('host').focus();</script>
                </main></body></html>
                """);

            var bindings = new[]
            {
                new KeyboardBinding { Keys = new[] { "Enter" }, Action = "activate" },
            };

            await Assert.ThrowsAsync<A11yAssertionException>(
                () => SunfishA11yAssertions.AssertKeyboardMapAsync(page, bindings, "body"));
        }
        finally { await page.CloseAsync(); }
    }

    [Fact]
    public async Task AssertDirectionalIconsMirroredAsync_PassesWhenTransformIsNonIdentity()
    {
        var page = await NewPageAsync();
        try
        {
            await page.SetContentAsync("""
                <!doctype html><html lang="ar" dir="rtl"><head><meta charset="utf-8"><title>t</title>
                <style>
                  html[dir="rtl"] .icon-mirror { transform: scaleX(-1); }
                </style>
                </head>
                <body><main>
                <span class="icon-mirror" id="i1">→</span>
                </main></body></html>
                """);

            await SunfishA11yAssertions.AssertDirectionalIconsMirroredAsync(page, new[] { "#i1" });
        }
        finally { await page.CloseAsync(); }
    }

    [Fact]
    public async Task AssertDirectionalIconsMirroredAsync_ThrowsWhenIconHasIdentityTransform()
    {
        var page = await NewPageAsync();
        try
        {
            // No CSS rule applied — icon stays in identity transform.
            await page.SetContentAsync("""
                <!doctype html><html lang="ar" dir="rtl"><head><meta charset="utf-8"><title>t</title></head>
                <body><main>
                <span id="i1">→</span>
                </main></body></html>
                """);

            await Assert.ThrowsAsync<A11yAssertionException>(
                () => SunfishA11yAssertions.AssertDirectionalIconsMirroredAsync(page, new[] { "#i1" }));
        }
        finally { await page.CloseAsync(); }
    }
}
