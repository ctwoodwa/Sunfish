namespace Sunfish.Compat.Infragistics.Enums;

/// <summary>
/// Mirrors Ignite UI's <c>IgniteUI.Blazor.Controls.ButtonBaseType</c>. <see cref="Reset"/>
/// throws <see cref="System.NotSupportedException"/> when used with IgbButton — Sunfish has
/// no Reset semantics. See <c>docs/compat-infragistics-mapping.md</c>.
/// </summary>
public enum ButtonBaseType
{
    Button = 0,
    Reset = 1,
    Submit = 2
}
