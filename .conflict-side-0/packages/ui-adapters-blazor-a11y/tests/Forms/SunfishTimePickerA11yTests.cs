using System.Threading.Tasks;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Forms;

/// <summary>
/// Wave 4 cascade extension — bUnit-axe a11y harness for SunfishTimePicker.
/// Currently skipped: generic over <c>TValue</c> — requires a closed-type fixture.
/// Tracked for follow-up.
/// </summary>
public class SunfishTimePickerA11yTests
{
    [Fact(Skip = "Requires complex fixture - tracked: SunfishTimePicker is generic (@typeparam TValue) and needs a closed-type fixture")]
    public Task SunfishTimePicker_HasNoModeratePlusAxeViolations() => Task.CompletedTask;
}
