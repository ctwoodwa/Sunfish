using Microsoft.AspNetCore.Components;
using Xunit;

namespace Sunfish.Compat.Infragistics.Tests;

public class IgbSelectTests
{
    [Fact]
    public void IgbSelect_IsGenericOverTValue()
    {
        var t = typeof(Sunfish.Compat.Infragistics.IgbSelect<>);
        Assert.True(t.IsGenericTypeDefinition);
        Assert.Single(t.GetGenericArguments());
    }

    [Fact]
    public void IgbSelectItem_IsGenericAndChildOfIgbSelect()
    {
        var t = typeof(Sunfish.Compat.Infragistics.IgbSelectItem<>);
        Assert.True(t.IsGenericTypeDefinition);

        // Verify it extends ComponentBase (via CompatChildComponent).
        Assert.True(typeof(ComponentBase).IsAssignableFrom(t));
    }

    [Fact]
    public void IgbSelectItem_ThrowsWhenRenderedOutsideIgbSelect()
    {
        // Construct concrete IgbSelectItem<int> via reflection and invoke OnParametersSet
        // through the protected RequireParent chain — with no parent, it throws.
        var itemType = typeof(Sunfish.Compat.Infragistics.IgbSelectItem<int>);
        var item = System.Activator.CreateInstance(itemType);
        Assert.NotNull(item);

        var onParamsSet = itemType.GetMethod(
            "OnParametersSet",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public);
        Assert.NotNull(onParamsSet);

        var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
            onParamsSet!.Invoke(item, null));
        Assert.IsType<System.InvalidOperationException>(ex.InnerException);
        Assert.Contains("IgbSelectItem", ex.InnerException!.Message);
        Assert.Contains("IgbSelect", ex.InnerException.Message);
    }
}
