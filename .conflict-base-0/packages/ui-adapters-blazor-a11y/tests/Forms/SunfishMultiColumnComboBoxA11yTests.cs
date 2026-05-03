using System.Threading.Tasks;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Forms;

/// <summary>
/// Wave 4 cascade extension — bUnit-axe a11y harness for SunfishMultiColumnComboBox.
/// Currently skipped: generic over <c>TItem</c> + <c>TValue</c> with a column model;
/// the harness skeleton requires a typed fixture before axe coverage is meaningful.
/// Tracked for follow-up.
/// </summary>
public class SunfishMultiColumnComboBoxA11yTests
{
    [Fact(Skip = "Requires complex fixture - tracked: SunfishMultiColumnComboBox is generic (@typeparam TItem, TValue) and needs a typed data + column fixture")]
    public Task SunfishMultiColumnComboBox_HasNoModeratePlusAxeViolations() => Task.CompletedTask;
}
