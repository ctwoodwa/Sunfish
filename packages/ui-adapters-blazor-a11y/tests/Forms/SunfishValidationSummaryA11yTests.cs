using System.Threading.Tasks;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Forms;

/// <summary>
/// Wave 4 cascade extension — bUnit-axe a11y harness for SunfishValidationSummary.
/// Currently skipped: requires a cascading <c>EditContext</c> from a parent
/// <c>SunfishForm</c> / <c>EditForm</c>; throws on isolated render. Coverage lands
/// on the parent form harness once that fixture exists.
/// </summary>
public class SunfishValidationSummaryA11yTests
{
    [Fact(Skip = "Requires complex fixture - tracked: SunfishValidationSummary needs cascading EditContext from a parent SunfishForm/EditForm")]
    public Task SunfishValidationSummary_HasNoModeratePlusAxeViolations() => Task.CompletedTask;
}
