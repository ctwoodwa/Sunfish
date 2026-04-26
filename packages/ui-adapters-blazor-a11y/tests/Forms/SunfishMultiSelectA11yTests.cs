using System.Threading.Tasks;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Forms;

/// <summary>
/// Wave 4 cascade extension — bUnit-axe a11y harness for SunfishMultiSelect.
/// Currently skipped: SunfishMultiSelect is generic over <c>TItem</c> + <c>TValue</c>
/// and binds a data source — the harness skeleton requires a typed fixture before
/// axe coverage is meaningful. Tracked for follow-up.
/// </summary>
public class SunfishMultiSelectA11yTests
{
    [Fact(Skip = "Requires complex fixture - tracked: SunfishMultiSelect is generic (@typeparam TItem, TValue) and needs a typed data source fixture")]
    public Task SunfishMultiSelect_HasNoModeratePlusAxeViolations() => Task.CompletedTask;
}
