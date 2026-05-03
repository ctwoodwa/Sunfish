using System.Threading.Tasks;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Forms;

/// <summary>
/// Wave 4 cascade extension — bUnit-axe a11y harness for SunfishListBox.
/// Currently skipped: SunfishListBox is generic over <c>TItem</c> and binds a data
/// source — the harness skeleton requires a typed fixture before axe coverage is
/// meaningful. Tracked for follow-up.
/// </summary>
public class SunfishListBoxA11yTests
{
    [Fact(Skip = "Requires complex fixture - tracked: SunfishListBox is generic (@typeparam TItem) and needs a typed data source fixture")]
    public Task SunfishListBox_HasNoModeratePlusAxeViolations() => Task.CompletedTask;
}
