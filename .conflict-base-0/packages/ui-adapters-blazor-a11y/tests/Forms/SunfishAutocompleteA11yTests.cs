using System.Threading.Tasks;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Forms;

/// <summary>
/// Wave 4 cascade extension — bUnit-axe a11y harness for SunfishAutocomplete.
/// Currently skipped: SunfishAutocomplete is generic over <c>TItem</c> and binds a
/// data source — the harness skeleton requires a typed fixture (closed generic +
/// realistic data) before axe coverage is meaningful. Tracked for follow-up.
/// </summary>
public class SunfishAutocompleteA11yTests
{
    [Fact(Skip = "Requires complex fixture - tracked: SunfishAutocomplete is generic (@typeparam TItem) and needs a typed data source fixture")]
    public Task SunfishAutocomplete_HasNoModeratePlusAxeViolations() => Task.CompletedTask;
}
