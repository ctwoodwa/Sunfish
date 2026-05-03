using System.Reflection;
using Sunfish.UICore.Contracts;
using Xunit;

namespace Sunfish.UICore.Tests;

public class IconProviderContractTests
{
    private static readonly Type ContractType = typeof(ISunfishIconProvider);

    [Fact]
    public void ISunfishIconProvider_GetIcon_ReturnsString_NotMarkupString()
    {
        var method = ContractType.GetMethod("GetIcon");
        Assert.NotNull(method);
        Assert.Equal(typeof(string), method.ReturnType);
    }

    [Fact]
    public void ISunfishIconProvider_HasGetIconSpriteUrl()
    {
        var method = ContractType.GetMethod("GetIconSpriteUrl");
        Assert.NotNull(method);
        Assert.Equal(typeof(string), method.ReturnType);
    }

    [Fact]
    public void ISunfishIconProvider_HasNoBlazorDependency()
    {
        var assembly = ContractType.Assembly;
        var refs = assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(refs, r => r.Name?.Contains("AspNetCore.Components") == true);
    }
}
