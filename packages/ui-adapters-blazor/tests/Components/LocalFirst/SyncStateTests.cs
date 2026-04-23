using System.Linq;
using Sunfish.UIAdapters.Blazor.Components.LocalFirst;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.Tests.Components.LocalFirst;

public class SyncStateTests
{
    [Fact]
    public void SyncState_HasExpectedValues()
    {
        var expected = new[]
        {
            SyncState.Healthy,
            SyncState.Stale,
            SyncState.Offline,
            SyncState.ConflictPending,
            SyncState.Quarantine,
        };
        foreach (var state in expected)
        {
            Assert.True(System.Enum.IsDefined(typeof(SyncState), state));
        }
    }

    [Fact]
    public void SyncState_Count_IsFive()
    {
        var values = System.Enum.GetValues<SyncState>();
        Assert.Equal(5, values.Length);
    }

    [Fact]
    public void SyncState_IsInLocalFirstNamespace()
    {
        Assert.Equal(
            "Sunfish.UIAdapters.Blazor.Components.LocalFirst",
            typeof(SyncState).Namespace);
    }
}
