using System.Threading.Tasks;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Layout.GridLayout;

/// <summary>
/// SunfishGridLayoutItem, SunfishGridLayoutColumn, and SunfishGridLayoutRow are
/// definition-only children of SunfishGridLayout; no isolated DOM.
/// </summary>
public class SunfishGridLayoutItemA11yTests
{
    [Fact(Skip = "Definition-only - configures parent SunfishGridLayout, no isolated DOM")]
    public Task SunfishGridLayoutItem_HasNoIsolatedDom() => Task.CompletedTask;
}
