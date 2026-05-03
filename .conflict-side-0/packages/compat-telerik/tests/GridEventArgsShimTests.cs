using Xunit;

namespace Sunfish.Compat.Telerik.Tests;

public class GridEventArgsShimTests
{
    [Fact]
    public void GridRowClickEventArgs_TypeIsPublicAndInRootNamespace()
    {
        var type = typeof(Sunfish.Compat.Telerik.GridRowClickEventArgs);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.Compat.Telerik", type.Namespace);
    }

    [Fact]
    public void GridCommandEventArgs_TypeIsPublicAndInRootNamespace()
    {
        var type = typeof(Sunfish.Compat.Telerik.GridCommandEventArgs);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.Compat.Telerik", type.Namespace);
    }

    [Fact]
    public void GridReadEventArgs_TypeIsPublicAndInRootNamespace()
    {
        var type = typeof(Sunfish.Compat.Telerik.GridReadEventArgs);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.Compat.Telerik", type.Namespace);
    }

    [Fact]
    public void DatePickerChangeEventArgs_TypeIsPublicAndInRootNamespace()
    {
        var type = typeof(Sunfish.Compat.Telerik.DatePickerChangeEventArgs);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.Compat.Telerik", type.Namespace);
    }

    [Fact]
    public void GridRowClickEventArgs_ItemIsObjectPerTelerikShape()
    {
        // Telerik erases the row type to `object`; confirm the shim matches that shape.
        var args = new Sunfish.Compat.Telerik.GridRowClickEventArgs { Item = "x", Field = "f" };
        Assert.Equal("x", args.Item);
        Assert.Equal("f", args.Field);
    }

    [Fact]
    public void GridCommandEventArgs_SupportsCancellation()
    {
        var args = new Sunfish.Compat.Telerik.GridCommandEventArgs { Command = "Delete" };
        Assert.False(args.IsCancelled);
        args.IsCancelled = true;
        Assert.True(args.IsCancelled);
    }
}
