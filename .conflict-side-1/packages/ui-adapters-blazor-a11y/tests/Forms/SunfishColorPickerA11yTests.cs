using System.Threading.Tasks;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Forms;

/// <summary>
/// Wave 4 cascade extension — bUnit-axe a11y harness for SunfishColorPicker.
/// Currently skipped: composite picker that hosts the gradient/palette views and
/// performs JS interop for popup positioning + format detection; the trigger
/// surface alone is not stable across the closed-button-only state. Tracked for
/// follow-up — coverage lands on SunfishFlatColorPicker (palette-only render) and
/// SunfishColorPalette already.
/// </summary>
public class SunfishColorPickerA11yTests
{
    [Fact(Skip = "Requires complex fixture - tracked: SunfishColorPicker is a composite popup picker with deep JS interop; coverage lands on FlatColorPicker + ColorPalette tests")]
    public Task SunfishColorPicker_HasNoModeratePlusAxeViolations() => Task.CompletedTask;
}
