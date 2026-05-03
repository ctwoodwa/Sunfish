using Sunfish.Foundation.Configuration;
using Sunfish.Foundation.Models;

namespace Sunfish.Foundation.Services;

/// <summary>
/// Manages the active theme at runtime, including dark-mode toggling and RTL support.
/// Components observe <see cref="ThemeChanged"/> to re-render when the theme updates.
/// </summary>
public interface ISunfishThemeService
{
    /// <summary>
    /// Gets the currently active theme configuration.
    /// </summary>
    SunfishTheme CurrentTheme { get; }

    /// <summary>
    /// Gets a value indicating whether dark mode is currently enabled.
    /// </summary>
    bool IsDarkMode { get; }

    /// <summary>
    /// Gets a value indicating whether the current theme uses right-to-left layout direction.
    /// </summary>
    bool IsRtl { get; }

    /// <summary>
    /// Raised whenever the active theme changes, allowing components to re-render.
    /// </summary>
    event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    /// <summary>
    /// Replaces the active theme with the specified <paramref name="theme"/> and notifies subscribers.
    /// </summary>
    /// <param name="theme">The new theme to apply.</param>
    Task SetThemeAsync(SunfishTheme theme);

    /// <summary>
    /// Toggles between light and dark mode and notifies subscribers.
    /// </summary>
    Task ToggleDarkModeAsync();

    /// <summary>
    /// Sets dark mode to the specified value and notifies subscribers.
    /// Unlike <see cref="ToggleDarkModeAsync"/>, this is idempotent — safe to call
    /// when the current state is unknown (e.g., after page reload).
    /// </summary>
    Task SetDarkModeAsync(bool dark);

    /// <summary>
    /// Performs one-time initialization of the theme service (e.g., loading persisted preferences).
    /// </summary>
    Task InitializeAsync();
}
