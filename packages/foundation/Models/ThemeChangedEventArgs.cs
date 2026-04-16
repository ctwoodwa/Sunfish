// TODO(Task 5): Restore once Sunfish.Foundation.Configuration is migrated.
// using Sunfish.Foundation.Configuration;
using Sunfish.Foundation.Models.ForwardRefs;

namespace Sunfish.Foundation.Models;

public class ThemeChangedEventArgs : EventArgs
{
    public required SunfishTheme OldTheme { get; init; }
    public required SunfishTheme NewTheme { get; init; }
    public bool IsDarkMode { get; init; }
}
