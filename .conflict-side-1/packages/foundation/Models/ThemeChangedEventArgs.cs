using Sunfish.Foundation.Configuration;

namespace Sunfish.Foundation.Models;

public class ThemeChangedEventArgs : EventArgs
{
    public required SunfishTheme OldTheme { get; init; }
    public required SunfishTheme NewTheme { get; init; }
    public bool IsDarkMode { get; init; }
}
