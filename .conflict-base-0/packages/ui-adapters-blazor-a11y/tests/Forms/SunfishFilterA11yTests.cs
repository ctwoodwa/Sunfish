using System.Threading.Tasks;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Forms;

/// <summary>
/// Wave 4 cascade extension — bUnit-axe a11y harness for SunfishFilter.
/// Currently skipped: generic over <c>TItem</c> with a typed filter expression
/// model — requires a closed-type fixture before axe coverage is meaningful.
/// Tracked for follow-up.
/// </summary>
public class SunfishFilterA11yTests
{
    [Fact(Skip = "Requires complex fixture - tracked: SunfishFilter is generic (@typeparam TItem) and needs a typed filter-expression fixture")]
    public Task SunfishFilter_HasNoModeratePlusAxeViolations() => Task.CompletedTask;
}
