namespace Sunfish.Compat.Telerik.Enums;

/// <summary>
/// Mirrors Telerik.Blazor.WindowState. Maximized/Minimized throw
/// <see cref="System.NotSupportedException"/> — Sunfish's SunfishWindow does not yet model
/// maximized/minimized states. See docs/compat-telerik-mapping.md.
/// </summary>
public enum WindowState
{
    Default = 0,
    Maximized = 1,
    Minimized = 2
}
