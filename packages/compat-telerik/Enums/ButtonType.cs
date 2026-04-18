namespace Sunfish.Compat.Telerik.Enums;

/// <summary>
/// Mirrors Telerik.Blazor.Enums.ButtonType. NOTE: <see cref="Reset"/> throws
/// <see cref="System.NotSupportedException"/> when used with TelerikButton — Sunfish has no
/// Reset semantics. See docs/compat-telerik-mapping.md.
/// </summary>
public enum ButtonType
{
    Button = 0,
    Submit = 1,
    Reset = 2
}
