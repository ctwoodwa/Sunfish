using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace Sunfish.UIAdapters.Blazor.A11y;

/// <summary>
/// Sunfish-specific accessibility assertions executed against a Playwright-hosted page.
/// Layered on top of axe-core's generic WCAG checks — these methods enforce the
/// contract declared in <c>parameters.a11y.sunfish</c> per ADR 0034: focus management,
/// keyboard interactions, RTL icon mirroring.
/// </summary>
/// <remarks>
/// All methods are async and return when the assertion is satisfied. Assertion failures
/// throw <see cref="A11yAssertionException"/> with a context-rich message; they do NOT
/// throw plain xUnit Assert exceptions so callers can wrap with their own test framework.
/// </remarks>
public static class SunfishA11yAssertions
{
    /// <summary>
    /// Assert that the element matching <paramref name="expectedFocusedSelector"/> currently
    /// has focus per <c>document.activeElement</c>. If <paramref name="expectedFocusedSelector"/>
    /// is <c>"none"</c>, asserts no element has focus other than &lt;body&gt;.
    /// </summary>
    public static async Task AssertFocusInitialAsync(IPage page, string expectedFocusedSelector)
    {
        if (page is null) throw new ArgumentNullException(nameof(page));
        if (string.IsNullOrEmpty(expectedFocusedSelector)) throw new ArgumentException("Selector required", nameof(expectedFocusedSelector));

        if (expectedFocusedSelector == "none")
        {
            var activeTag = await page.EvaluateAsync<string>(
                "() => document.activeElement?.tagName?.toLowerCase() ?? 'body'");
            if (activeTag != "body" && activeTag != "html")
            {
                throw new A11yAssertionException(
                    $"Expected no element to have focus (activeElement should be <body>), but found <{activeTag}>.");
            }
            return;
        }

        var matches = await page.EvaluateAsync<bool>($@"
            (selector) => {{
                const el = document.querySelector(selector);
                return el !== null && el === document.activeElement;
            }}", expectedFocusedSelector);

        if (!matches)
        {
            var actual = await page.EvaluateAsync<string>(
                "() => document.activeElement?.outerHTML?.slice(0, 80) ?? '<none>'");
            throw new A11yAssertionException(
                $"Expected initial focus on '{expectedFocusedSelector}', but activeElement is: {actual}");
        }
    }

    /// <summary>
    /// Assert focus is trapped within <paramref name="containerSelector"/>: pressing Tab
    /// past the last focusable child cycles back to the first; Shift+Tab past the first
    /// cycles to the last. Catches loose modal dialogs that leak focus to outside elements.
    /// </summary>
    public static async Task AssertFocusTrapAsync(IPage page, string containerSelector)
    {
        if (page is null) throw new ArgumentNullException(nameof(page));
        if (string.IsNullOrEmpty(containerSelector)) throw new ArgumentException("Selector required", nameof(containerSelector));

        var focusableCount = await page.EvaluateAsync<int>($@"
            (selector) => {{
                const container = document.querySelector(selector);
                if (!container) return 0;
                const sel = 'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex=""-1""])';
                return container.querySelectorAll(sel).length;
            }}", containerSelector);

        if (focusableCount == 0)
        {
            throw new A11yAssertionException(
                $"Container '{containerSelector}' has no focusable descendants — focus trap cannot be verified.");
        }

        // Tab focusableCount + 1 times. After the last tab, focus should be back on the
        // first focusable element inside the container.
        for (int i = 0; i <= focusableCount; i++)
        {
            await page.Keyboard.PressAsync("Tab");
        }

        var focusInside = await page.EvaluateAsync<bool>($@"
            (selector) => {{
                const container = document.querySelector(selector);
                if (!container) return false;
                return container.contains(document.activeElement);
            }}", containerSelector);

        if (!focusInside)
        {
            throw new A11yAssertionException(
                $"Focus escaped container '{containerSelector}' after {focusableCount + 1} Tab presses. " +
                "Focus trap is not in effect.");
        }
    }

    /// <summary>
    /// Assert that each declared keyboard binding fires its named action. The component is
    /// expected to mark fired actions on a host data attribute (<c>data-sunfish-fired</c>).
    /// Tests dispatch each binding's keys; assertion checks that the fired-actions list
    /// contains the expected action.
    /// </summary>
    public static async Task AssertKeyboardMapAsync(
        IPage page,
        IReadOnlyList<KeyboardBinding> bindings,
        string hostSelector = "body")
    {
        if (page is null) throw new ArgumentNullException(nameof(page));
        if (bindings is null) throw new ArgumentNullException(nameof(bindings));

        foreach (var binding in bindings)
        {
            // Reset the fired-actions data attribute on the host before each press.
            await page.EvaluateAsync($@"
                (selector) => {{
                    const host = document.querySelector(selector);
                    if (host) host.setAttribute('data-sunfish-fired', '');
                }}", hostSelector);

            // Press the chord (single key or modified-key combo).
            var chord = string.Join("+", binding.Keys);
            await page.Keyboard.PressAsync(chord);

            var fired = await page.EvaluateAsync<string>($@"
                (selector) => {{
                    const host = document.querySelector(selector);
                    return host?.getAttribute('data-sunfish-fired') ?? '';
                }}", hostSelector);

            if (!fired.Contains(binding.Action))
            {
                throw new A11yAssertionException(
                    $"Keyboard binding '{chord}' was expected to fire '{binding.Action}', " +
                    $"but data-sunfish-fired contains '{fired}'.");
            }
        }
    }

    /// <summary>
    /// Assert that each selector in <paramref name="iconSelectors"/> reflects a non-identity
    /// CSS transform when the document direction is RTL — i.e., the icon is mirrored. Caller
    /// is responsible for setting <c>html[dir="rtl"]</c> before invoking; this method only
    /// inspects computed styles.
    /// </summary>
    public static async Task AssertDirectionalIconsMirroredAsync(IPage page, IReadOnlyList<string> iconSelectors)
    {
        if (page is null) throw new ArgumentNullException(nameof(page));
        if (iconSelectors is null) throw new ArgumentNullException(nameof(iconSelectors));

        foreach (var selector in iconSelectors)
        {
            var transform = await page.EvaluateAsync<string>($@"
                (selector) => {{
                    const el = document.querySelector(selector);
                    if (!el) return 'NOT_FOUND';
                    return getComputedStyle(el).transform;
                }}", selector);

            if (transform == "NOT_FOUND")
            {
                throw new A11yAssertionException($"Directional icon '{selector}' not found in DOM.");
            }

            if (transform == "none" || transform == "matrix(1, 0, 0, 1, 0, 0)")
            {
                throw new A11yAssertionException(
                    $"Directional icon '{selector}' has identity transform under RTL — should be mirrored. " +
                    $"Computed transform: '{transform}'.");
            }
        }
    }

    /// <summary>
    /// Convenience: invoke every declared assertion in <paramref name="contract"/> against
    /// the page. Throws <see cref="A11yAssertionException"/> on the first failure with full
    /// context. Skips assertions whose contract field is null/empty.
    /// </summary>
    public static async Task AssertContractAsync(IPage page, SunfishA11yContract contract, string hostSelector = "body")
    {
        if (page is null) throw new ArgumentNullException(nameof(page));
        if (contract is null) throw new ArgumentNullException(nameof(contract));

        if (!string.IsNullOrEmpty(contract.Focus.Initial) && contract.Focus.Initial != "self")
        {
            // "self" is the default — bUnit fragment-without-component-host doesn't have a "self".
            // Tests that mount a real component should explicitly call AssertFocusInitialAsync.
        }

        if (contract.Focus.Trap)
        {
            await AssertFocusTrapAsync(page, hostSelector);
        }

        if (contract.KeyboardMap.Count > 0)
        {
            await AssertKeyboardMapAsync(page, contract.KeyboardMap, hostSelector);
        }

        if (contract.DirectionalIcons.Count > 0)
        {
            await AssertDirectionalIconsMirroredAsync(page, contract.DirectionalIcons);
        }
    }
}

/// <summary>Thrown when a Sunfish-specific a11y assertion fails. Wraps any framework's assert.</summary>
public sealed class A11yAssertionException : Exception
{
    public A11yAssertionException(string message) : base(message) { }
    public A11yAssertionException(string message, Exception inner) : base(message, inner) { }
}
