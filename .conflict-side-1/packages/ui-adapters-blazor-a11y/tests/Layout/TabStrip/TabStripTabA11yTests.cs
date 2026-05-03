using System.Threading.Tasks;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Layout.TabStrip;

/// <summary>
/// TabStripTab is a definition-only child of SunfishTabStrip; no isolated DOM.
/// </summary>
public class TabStripTabA11yTests
{
    [Fact(Skip = "Definition-only - configures parent SunfishTabStrip, no isolated DOM")]
    public Task TabStripTab_HasNoIsolatedDom() => Task.CompletedTask;
}
