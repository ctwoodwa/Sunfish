using System.Threading;
using System.Threading.Tasks;
using Sunfish.UICore.Primitives;

namespace Sunfish.UIAdapters.Blazor.Maui;

/// <summary>
/// MAUI adapter for <see cref="IFocusTrap"/> per ADR 0077 §4 + WCAG 2.2
/// SC 2.4.3 (Focus Order) + SC 2.1.2 (No Keyboard Trap).
/// Windows: <c>UIElement.Focus(FocusState.Keyboard)</c>.
/// MacCatalyst: <c>UIResponder.BecomeFirstResponder()</c>.
/// In Anchor (MAUI Blazor Hybrid), focus management for in-WebView surfaces
/// is handled by <c>BlazorFocusTrap</c>; this class handles native
/// MAUI surface focus boundaries outside the BlazorWebView.
/// </summary>
/// <remarks>
/// <see cref="Container"/> is typed as <see cref="object"/> so this class
/// compiles cleanly on the plain <c>net11.0</c> TFM used by the Blazor
/// adapter. Callers on MAUI TFMs assign a
/// <c>Microsoft.Maui.Controls.View</c> instance; the platform blocks
/// below cast accordingly.
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

    /// <inheritdoc/>
    public ValueTask EnterAsync(CancellationToken ct = default)
    {
        if (_isActive || Container is null) return ValueTask.CompletedTask;
        _isActive = true;
        FocusContainer();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask ExitAsync(CancellationToken ct = default)
    {
        _isActive = false;
        return ValueTask.CompletedTask;
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
        // Plain net11.0 / Android: no platform focus API available.
        // Anchor uses BlazorFocusTrap for in-WebView focus management.
        _ = Container;
#endif
    }
}
