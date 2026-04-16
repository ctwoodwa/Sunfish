using System.Reflection;
using Sunfish.UICore.Contracts;
using Xunit;

namespace Sunfish.UICore.Tests;

public class JsInteropContractTests
{
    private static readonly Type ContractType = typeof(ISunfishJsInterop);

    [Fact]
    public void ISunfishJsInterop_HasInitializeAsync()
    {
        var method = ContractType.GetMethod("InitializeAsync");
        Assert.NotNull(method);
    }

    [Fact]
    public void ISunfishJsInterop_HasShowModalAsync()
    {
        var method = ContractType.GetMethod("ShowModalAsync", [typeof(string)]);
        Assert.NotNull(method);
    }

    [Fact]
    public void ISunfishJsInterop_GetElementBoundsAsync_TakesStringNotElementReference()
    {
        var method = ContractType.GetMethod("GetElementBoundsAsync", [typeof(string)]);
        Assert.NotNull(method);
    }

    [Fact]
    public void ISunfishJsInterop_HasNoBlazorDependency()
    {
        var assembly = ContractType.Assembly;
        var refs = assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(refs, r => r.Name?.Contains("AspNetCore.Components") == true);
        Assert.DoesNotContain(refs, r => r.Name?.Contains("JSInterop") == true);
    }

    [Fact]
    public void BoundingBox_IsFrameworkAgnosticRecord()
    {
        var type = typeof(BoundingBox);
        Assert.True(type.IsValueType || (type.IsClass && type.GetProperties().Length > 0));
        Assert.Null(type.GetProperty("ElementReference"));
    }
}
