using Xunit;

namespace Sunfish.Compat.Infragistics.Tests;

public class IgbGridTests
{
    [Fact]
    public void IgbGrid_TypeIsPublicGenericAndInRootNamespace()
    {
        var t = typeof(Sunfish.Compat.Infragistics.IgbGrid<>);
        Assert.True(t.IsPublic);
        Assert.True(t.IsGenericTypeDefinition);
        Assert.Single(t.GetGenericArguments());
        Assert.Equal("Sunfish.Compat.Infragistics", t.Namespace);
    }

    [Fact]
    public void IgbColumn_TypeIsPublicGenericAndInRootNamespace()
    {
        var t = typeof(Sunfish.Compat.Infragistics.IgbColumn<>);
        Assert.True(t.IsPublic);
        Assert.True(t.IsGenericTypeDefinition);
        Assert.Single(t.GetGenericArguments());
        Assert.Equal("Sunfish.Compat.Infragistics", t.Namespace);
    }

    [Fact]
    public void IgbColumn_BodyTemplateScript_ThrowsWithMigrationHint()
    {
        // JS-side template scripts have no Blazor analog; the shim throws with a
        // migration hint pointing at Blazor RenderFragment templates.
        var itemType = typeof(Sunfish.Compat.Infragistics.IgbColumn<int>);
        var column = System.Activator.CreateInstance(itemType);
        Assert.NotNull(column);

        var scriptProp = itemType.GetProperty("BodyTemplateScript");
        Assert.NotNull(scriptProp);
        scriptProp!.SetValue(column, "myScriptFn");

        var onParamsSet = itemType.GetMethod(
            "OnParametersSet",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public);
        Assert.NotNull(onParamsSet);

        var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
            onParamsSet!.Invoke(column, null));
        Assert.IsType<System.NotSupportedException>(ex.InnerException);
        Assert.Contains("BodyTemplateScript", ex.InnerException!.Message);
    }
}
