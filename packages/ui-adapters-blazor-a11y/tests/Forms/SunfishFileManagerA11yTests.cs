using System.Threading.Tasks;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Forms;

/// <summary>
/// Wave 4 cascade extension — bUnit-axe a11y harness for SunfishFileManager.
/// Currently skipped: generic over <c>TItem</c> and binds a hierarchical file
/// model — requires a closed-type fixture with a populated tree before axe
/// coverage is meaningful. Tracked for follow-up.
/// </summary>
public class SunfishFileManagerA11yTests
{
    [Fact(Skip = "Requires complex fixture - tracked: SunfishFileManager is generic (@typeparam TItem) and needs a typed tree-data fixture")]
    public Task SunfishFileManager_HasNoModeratePlusAxeViolations() => Task.CompletedTask;
}
