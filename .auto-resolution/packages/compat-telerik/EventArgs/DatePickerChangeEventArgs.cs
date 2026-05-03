using System;

namespace Sunfish.Compat.Telerik;

/// <summary>
/// Telerik-shaped date-picker change event arguments. Mirrors
/// <c>Telerik.Blazor.Components.DatePickerChangeEventArgs</c>.
///
/// <para><b>Status:</b> Type shipped so consumer handler signatures compile; functional
/// wiring from <c>TelerikDatePicker</c> to this type is not hooked in this gap-closure —
/// TelerikDatePicker currently forwards through the plain <c>ValueChanged</c> pattern and
/// delegates directly to <c>SunfishDatePicker</c>. See <c>docs/compat-telerik-mapping.md</c>.</para>
/// </summary>
public class DatePickerChangeEventArgs
{
    /// <summary>The new value of the date picker. Nullable per Telerik's shape.</summary>
    public DateTime? Value { get; init; }
}
