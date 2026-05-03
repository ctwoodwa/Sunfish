using System.Threading.Tasks;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Forms;

/// <summary>
/// Wave 4 cascade extension — bUnit-axe a11y harness for SunfishDateRangePicker.
/// Currently skipped: SunfishDateRangePicker [Inject]s the internal
/// <c>ISunfishJsModuleLoader</c>; the internal interop service can't be substituted
/// from this test assembly without an InternalsVisibleTo entry. Same blocker as
/// SunfishWindow / SunfishEditor (Editors). Tracked for follow-up.
/// </summary>
public class SunfishDateRangePickerA11yTests
{
    [Fact(Skip = "Requires complex fixture - tracked: SunfishDateRangePicker injects internal ISunfishJsModuleLoader (same blocker as SunfishWindow/SunfishEditor)")]
    public Task SunfishDateRangePicker_HasNoModeratePlusAxeViolations() => Task.CompletedTask;
}
