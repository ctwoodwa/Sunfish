namespace Sunfish.Compat.Infragistics.Enums;

/// <summary>
/// Mirrors Ignite UI's <c>InputType</c> for <c>IgbInput.DisplayType</c>. Rendered as the
/// HTML <c>type</c> attribute on the underlying Sunfish text box.
/// See <c>docs/compat-infragistics-mapping.md</c>.
/// </summary>
public enum InputType
{
    Text = 0,
    Email = 1,
    Password = 2,
    Tel = 3,
    Url = 4,
    Search = 5,
    Number = 6
}
