using System.Threading.Tasks;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Forms;

/// <summary>
/// Wave 4 cascade extension — bUnit-axe a11y harness for SunfishRadioGroup.
/// Currently skipped: generic over <c>TValue</c> — requires a closed-type fixture
/// (e.g. <c>SunfishRadioGroup&lt;string&gt;</c>). Tracked for follow-up.
/// </summary>
public class SunfishRadioGroupA11yTests
{
    [Fact(Skip = "Requires complex fixture - tracked: SunfishRadioGroup is generic (@typeparam TValue) and needs a closed-type fixture")]
    public Task SunfishRadioGroup_HasNoModeratePlusAxeViolations() => Task.CompletedTask;
}
