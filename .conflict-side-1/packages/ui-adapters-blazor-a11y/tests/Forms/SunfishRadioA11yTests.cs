using System.Threading.Tasks;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Forms;

/// <summary>
/// Wave 4 cascade extension — bUnit-axe a11y harness for SunfishRadio.
/// Currently skipped: generic over <c>TValue</c> — requires a closed-type fixture
/// (e.g. <c>SunfishRadio&lt;string&gt;</c>) and a parent radiogroup. Tracked for
/// follow-up.
/// </summary>
public class SunfishRadioA11yTests
{
    [Fact(Skip = "Requires complex fixture - tracked: SunfishRadio is generic (@typeparam TValue) and needs a closed-type fixture inside a SunfishRadioGroup")]
    public Task SunfishRadio_HasNoModeratePlusAxeViolations() => Task.CompletedTask;
}
