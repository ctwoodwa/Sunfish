using System.Threading.Tasks;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Forms;

/// <summary>
/// Wave 4 cascade extension — bUnit-axe a11y harness for SunfishValidationMessage.
/// Currently skipped: generic over <c>TValue</c> AND requires a cascading
/// <c>EditContext</c> from a parent <c>SunfishForm</c>/<c>EditForm</c>; throws on
/// isolated render. Tracked for follow-up.
/// </summary>
public class SunfishValidationMessageA11yTests
{
    [Fact(Skip = "Requires complex fixture - tracked: SunfishValidationMessage is generic (@typeparam TValue) and needs cascading EditContext from a parent SunfishForm/EditForm")]
    public Task SunfishValidationMessage_HasNoModeratePlusAxeViolations() => Task.CompletedTask;
}
