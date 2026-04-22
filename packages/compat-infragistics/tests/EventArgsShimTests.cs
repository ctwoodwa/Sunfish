using Xunit;

namespace Sunfish.Compat.Infragistics.Tests;

/// <summary>
/// Locks the shape of the EventArgs shim types that downstream consumer handler signatures
/// compile against. These types intentionally live in the root namespace (not under
/// <c>.EventArgs</c>) to match Ignite UI's flat namespace layout.
/// </summary>
public class EventArgsShimTests
{
    [Theory]
    [InlineData(typeof(Sunfish.Compat.Infragistics.IgbInputChangeEventArgs))]
    [InlineData(typeof(Sunfish.Compat.Infragistics.IgbGridRowClickEventArgs))]
    [InlineData(typeof(Sunfish.Compat.Infragistics.IgbGridCellClickEventArgs))]
    [InlineData(typeof(Sunfish.Compat.Infragistics.IgbGridSelectionEventArgs))]
    [InlineData(typeof(Sunfish.Compat.Infragistics.IgbGridSortingEventArgs))]
    public void EventArgsShims_ArePublicAndInRootNamespace(System.Type t)
    {
        Assert.True(t.IsPublic);
        Assert.Equal("Sunfish.Compat.Infragistics", t.Namespace);
    }

    [Fact]
    public void IgbInputChangeEventArgs_ValueIsObjectPerIgniteUiShape()
    {
        // Ignite UI's WC-bridged change event carries an object value (WC detail payload
        // is type-erased). Consumer cast as needed.
        var args = new Sunfish.Compat.Infragistics.IgbInputChangeEventArgs { Value = "x", Name = "n" };
        Assert.Equal("x", args.Value);
        Assert.Equal("n", args.Name);
    }

    [Fact]
    public void IgbGridRowClickEventArgs_RowDataIsObject()
    {
        var args = new Sunfish.Compat.Infragistics.IgbGridRowClickEventArgs { RowData = 42, Field = "f" };
        Assert.Equal(42, args.RowData);
        Assert.Equal("f", args.Field);
    }

    [Fact]
    public void IgbGridSelectionEventArgs_SupportsCancellation()
    {
        var args = new Sunfish.Compat.Infragistics.IgbGridSelectionEventArgs();
        Assert.False(args.Cancel);
        args.Cancel = true;
        Assert.True(args.Cancel);
    }

    [Fact]
    public void IgbGridSortingEventArgs_SupportsFieldDirectionCancel()
    {
        var args = new Sunfish.Compat.Infragistics.IgbGridSortingEventArgs
        {
            Field = "name",
            Direction = "asc"
        };
        Assert.Equal("name", args.Field);
        Assert.Equal("asc", args.Direction);
        Assert.False(args.Cancel);
    }
}
