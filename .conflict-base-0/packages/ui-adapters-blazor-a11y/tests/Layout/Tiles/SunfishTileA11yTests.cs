using System.Threading.Tasks;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.A11y.Tests.Layout.Tiles;

/// <summary>
/// SunfishTile is a child of SunfishTileLayout that registers via cascading parameter.
/// </summary>
public class SunfishTileA11yTests
{
    [Fact(Skip = "Definition-only - requires parent SunfishTileLayout, no isolated DOM")]
    public Task SunfishTile_HasNoIsolatedDom() => Task.CompletedTask;
}
