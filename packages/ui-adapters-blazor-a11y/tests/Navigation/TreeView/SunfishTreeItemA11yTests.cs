using System.Threading.Tasks;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Navigation.TreeView;

/// <summary>
/// Wave-2 cascade extension — placeholder skip for <c>SunfishTreeItem</c>.
/// SunfishTreeItem is a child of SunfishTreeView and registers itself with the
/// parent on init; it has no isolated DOM. Coverage lands on SunfishTreeView's test.
/// </summary>
public class SunfishTreeItemA11yTests
{
    [Fact(Skip = "Definition-only - requires parent SunfishTreeView, no isolated DOM")]
    public Task SunfishTreeItem_HasNoIsolatedDom() => Task.CompletedTask;
}
