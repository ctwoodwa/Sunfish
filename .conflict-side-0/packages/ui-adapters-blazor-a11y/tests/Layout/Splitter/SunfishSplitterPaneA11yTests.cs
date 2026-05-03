using System.Threading.Tasks;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Layout.Splitter;

/// <summary>
/// SunfishSplitterPane and SunfishSplitterPanes are pane wrappers that register
/// with parent SunfishSplitter; no isolated DOM.
/// </summary>
public class SunfishSplitterPaneA11yTests
{
    [Fact(Skip = "Definition-only - requires parent SunfishSplitter, no isolated DOM")]
    public Task SunfishSplitterPane_HasNoIsolatedDom() => Task.CompletedTask;
}
