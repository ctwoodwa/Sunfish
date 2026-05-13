using System.Threading;
using System.Threading.Tasks;
using Sunfish.UICore.Primitives;

namespace Sunfish.UIAdapters.Blazor.Maui;

/// <summary>
/// MAUI adapter for <see cref="IFocusTrap"/> per ADR 0077 §4 + WCAG 2.2
/// SC 2.4.3 (Focus Order) + SC 2.1.2 (No Keyboard Trap).
/// Windows: <c>UIElement.Focus(FocusState.Keyboard)</c> with prior-focus capture via
/// <c>FocusManager.GetFocusedElement</c> and restore on <see cref="ExitAsync"/>.
/// MacCatalyst: <c>UIResponder.BecomeFirstResponder</c> with <c>FirstResponder</c> capture.
/// In Anchor (MAUI Blazor Hybrid), focus management for in-WebView surfaces
/// is handled by <c>BlazorFocusTrap</c>; this class handles native
/// MAUI surface focus boundaries outside the BlazorWebView.
/// </summary>
/// <remarks>
/// <para><b>Escape route per WCAG SC 2.1.2:</b> native MAUI does not have a universal
/// Escape key on all form factors. The consuming MAUI page/view MUST call
/// <see cref="ExitAsync"/> in response to its platform-specific escape gesture
/// (e.g., back-navigation on Android, Escape key binding on Windows, swipe-down on iOS).</para>
/// <para><see cref="Container"/> is typed as <see cref="object"/> so this class
/// compiles cleanly on the plain <c>net11.0</c> TFM. Callers on MAUI TFMs assign a
/// <c>Microsoft.Maui.Controls.View</c> or platform-native view instance.</para>
/// </remarks>
public sealed class MauiFocusTrap : IFocusTrap
{
    /// <summary>
    /// The native MAUI view to trap focus within. Assign a
    /// <c>Microsoft.Maui.Controls.View</c> before calling
    /// <see cref="EnterAsync"/>. When null, the trap is a no-op.
    /// </summary>
    public object? Container { get; set; }

    private bool _isActive;

    // Platform-specific prior-focus references for restoration on ExitAsync (WCAG SC 2.4.3).
#if WINDOWS
    private Microsoft.UI.Xaml.UIElement? _priorFocusedEl;
#elif MACCATALYST || IOS
    private UIKit.UIResponder? _priorFirstResponder;
#endif

    /// <inheritdoc/>
    public ValueTask EnterAsync(CancellationToken ct = default)
    {
        if (_isActive || Container is null) return ValueTask.CompletedTask;
        _isActive = true;
        CapturePriorFocus();
        FocusContainer();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Restores the focus element that was active before <see cref="EnterAsync"/>
    /// was called, per WCAG SC 2.4.3. Consumers are responsible for calling
    /// <see cref="ExitAsync"/> in response to their platform Escape gesture.
    /// </remarks>
    public ValueTask ExitAsync(CancellationToken ct = default)
    {
        if (!_isActive) return ValueTask.CompletedTask;
        _isActive = false;
        RestorePriorFocus();
        return ValueTask.CompletedTask;
    }

    private void CapturePriorFocus()
    {
#if WINDOWS
        var root = (Microsoft.Maui.MauiWinUIApplication.Current?.MainWindow?.Content
            as Microsoft.UI.Xaml.FrameworkElement)?.XamlRoot;
        _priorFocusedEl = root is not null
            ? Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(root) as Microsoft.UI.Xaml.UIElement
            : null;
#elif MACCATALYST || IOS
        _priorFirstResponder = UIKit.UIApplication.SharedApplication.KeyWindow?.FirstResponder;
#endif
    }

    private void RestorePriorFocus()
    {
#if WINDOWS
        if (_priorFocusedEl is not null)
        {
            _priorFocusedEl.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            _priorFocusedEl = null;
        }
#elif MACCATALYST || IOS
        if (_priorFirstResponder is not null)
        {
            _priorFirstResponder.BecomeFirstResponder();
            _priorFirstResponder = null;
        }
#endif
    }

    private void FocusContainer()
    {
#if WINDOWS
        if (Container is Microsoft.UI.Xaml.UIElement winEl)
            winEl.Focus(Microsoft.UI.Xaml.FocusState.Keyboard);
#elif MACCATALYST || IOS
        if (Container is UIKit.UIView iosView)
            iosView.BecomeFirstResponder();
#else
        // Plain net11.0 / Android: no platform focus API; Anchor uses BlazorFocusTrap.
        _ = Container;
#endif
    }
}
