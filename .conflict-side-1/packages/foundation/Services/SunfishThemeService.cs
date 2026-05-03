using Sunfish.Foundation.Configuration;
using Sunfish.Foundation.Models;

namespace Sunfish.Foundation.Services;

/// <summary>
/// Framework-agnostic, in-memory implementation of <see cref="ISunfishThemeService"/>.
/// Holds theme state and fires change events. Does not persist state across page loads.
/// The Blazor adapter (ui-adapters-blazor) provides a JS-backed implementation that
/// persists dark-mode preference to localStorage.
/// </summary>
public class SunfishThemeService : ISunfishThemeService
{
    private SunfishTheme _currentTheme;

    /// <summary>
    /// Initializes the service with the supplied options. If no theme is configured,
    /// a default <see cref="SunfishTheme"/> is used.
    /// </summary>
    public SunfishThemeService(SunfishOptions options)
    {
        _currentTheme = options.Theme ?? new SunfishTheme();
    }

    /// <inheritdoc />
    public SunfishTheme CurrentTheme => _currentTheme;

    /// <inheritdoc />
    public bool IsDarkMode { get; private set; }

    /// <inheritdoc />
    public bool IsRtl => _currentTheme.IsRtl;

    /// <inheritdoc />
    public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    /// <inheritdoc />
    public Task InitializeAsync() => Task.CompletedTask;

    /// <inheritdoc />
    public Task SetThemeAsync(SunfishTheme theme)
    {
        var oldTheme = _currentTheme;
        _currentTheme = theme;
        ThemeChanged?.Invoke(this, new ThemeChangedEventArgs
        {
            OldTheme = oldTheme,
            NewTheme = theme,
            IsDarkMode = IsDarkMode
        });
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ToggleDarkModeAsync() => SetDarkModeAsync(!IsDarkMode);

    /// <inheritdoc />
    public Task SetDarkModeAsync(bool dark)
    {
        IsDarkMode = dark;
        ThemeChanged?.Invoke(this, new ThemeChangedEventArgs
        {
            OldTheme = _currentTheme,
            NewTheme = _currentTheme,
            IsDarkMode = IsDarkMode
        });
        return Task.CompletedTask;
    }
}
