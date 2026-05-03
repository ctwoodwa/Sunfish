using Xunit;

namespace Sunfish.Compat.Syncfusion.Tests;

/// <summary>
/// Shape locks for the EventArgs shims under <c>compat-syncfusion/EventArgs/</c>.
/// Consumer handler signatures depend on these types existing in the root namespace with
/// Syncfusion-matching property names.
/// </summary>
public class EventArgsShimTests
{
    [Fact]
    public void ChangeEventArgs_IsGenericAndHasCheckedProperty()
    {
        var type = typeof(Sunfish.Compat.Syncfusion.ChangeEventArgs<>);
        Assert.True(type.IsGenericTypeDefinition);
        Assert.Single(type.GetGenericArguments());
        var closed = typeof(Sunfish.Compat.Syncfusion.ChangeEventArgs<bool>);
        Assert.NotNull(closed.GetProperty("Checked"));
    }

    [Fact]
    public void DropDownChangeEventArgs_HasValueAndItemData()
    {
        var type = typeof(Sunfish.Compat.Syncfusion.DropDownChangeEventArgs<int, string>);
        Assert.NotNull(type.GetProperty("Value"));
        Assert.NotNull(type.GetProperty("ItemData"));
        Assert.NotNull(type.GetProperty("IsInteracted"));
    }

    [Fact]
    public void FilteringEventArgs_SupportsCancellation()
    {
        var args = new Sunfish.Compat.Syncfusion.FilteringEventArgs { Text = "hello" };
        Assert.False(args.Cancel);
        args.Cancel = true;
        Assert.True(args.Cancel);
        Assert.Equal("hello", args.Text);
    }

    [Fact]
    public void RowSelectEventArgs_IsGenericAndInRootNamespace()
    {
        var type = typeof(Sunfish.Compat.Syncfusion.RowSelectEventArgs<>);
        Assert.True(type.IsGenericTypeDefinition);
        Assert.Equal("Sunfish.Compat.Syncfusion", type.Namespace);
    }

    [Fact]
    public void RowSelectingEventArgs_SupportsCancellation()
    {
        var args = new Sunfish.Compat.Syncfusion.RowSelectingEventArgs<string> { Data = "row", RowIndex = 2 };
        Assert.False(args.Cancel);
        args.Cancel = true;
        Assert.True(args.Cancel);
        Assert.Equal("row", args.Data);
        Assert.Equal(2, args.RowIndex);
    }

    [Fact]
    public void ActionBeginArgs_HasRequestTypeAndCancel()
    {
        var args = new Sunfish.Compat.Syncfusion.ActionBeginArgs { RequestType = "paging" };
        Assert.Equal("paging", args.RequestType);
        Assert.False(args.Cancel);
        args.Cancel = true;
        Assert.True(args.Cancel);
    }

    [Fact]
    public void BeforeOpenEventArgs_HasMaxHeightAndCancel()
    {
        var args = new Sunfish.Compat.Syncfusion.BeforeOpenEventArgs { MaxHeight = 400 };
        Assert.Equal(400, args.MaxHeight);
        Assert.False(args.Cancel);
    }

    [Fact]
    public void BeforeCloseEventArgs_HasIsInteractedAndCancel()
    {
        var args = new Sunfish.Compat.Syncfusion.BeforeCloseEventArgs { IsInteracted = true };
        Assert.True(args.IsInteracted);
        Assert.False(args.Cancel);
    }

    [Fact]
    public void TooltipEventArgs_HasTargetAndType()
    {
        var args = new Sunfish.Compat.Syncfusion.TooltipEventArgs { Type = "ToolTip" };
        Assert.Equal("ToolTip", args.Type);
        Assert.False(args.Cancel);
    }

    [Fact]
    public void ToastEventArgs_TypesAreInRootNamespace()
    {
        Assert.Equal("Sunfish.Compat.Syncfusion", typeof(Sunfish.Compat.Syncfusion.ToastBeforeOpenArgs).Namespace);
        Assert.Equal("Sunfish.Compat.Syncfusion", typeof(Sunfish.Compat.Syncfusion.ToastOpenArgs).Namespace);
        Assert.Equal("Sunfish.Compat.Syncfusion", typeof(Sunfish.Compat.Syncfusion.ToastCloseArgs).Namespace);
        Assert.Equal("Sunfish.Compat.Syncfusion", typeof(Sunfish.Compat.Syncfusion.ToastClickEventArgs).Namespace);
    }

    [Fact]
    public void InputEventArgs_HasValueAndPreviousValue()
    {
        var args = new Sunfish.Compat.Syncfusion.InputEventArgs { Value = "new", PreviousValue = "old" };
        Assert.Equal("new", args.Value);
        Assert.Equal("old", args.PreviousValue);
    }

    [Fact]
    public void FilterEventArgs_HasColumnNameAndValue()
    {
        var args = new Sunfish.Compat.Syncfusion.FilterEventArgs
        {
            ColumnName = "Name",
            Operator = "equal",
            Value = "Sam"
        };
        Assert.Equal("Name", args.ColumnName);
        Assert.Equal("equal", args.Operator);
        Assert.Equal("Sam", args.Value);
        Assert.False(args.Cancel);
    }

    [Fact]
    public void SortEventArgs_HasColumnNameAndDirection()
    {
        var args = new Sunfish.Compat.Syncfusion.SortEventArgs { ColumnName = "Id", Direction = "Ascending" };
        Assert.Equal("Id", args.ColumnName);
        Assert.Equal("Ascending", args.Direction);
    }
}
