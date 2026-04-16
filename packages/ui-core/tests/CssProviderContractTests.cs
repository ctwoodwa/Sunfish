using System.Reflection;
using Sunfish.Foundation.Enums;
using Sunfish.UICore.Contracts;
using Xunit;

namespace Sunfish.UICore.Tests;

/// <summary>
/// Verifies the ISunfishCssProvider interface shape.
/// These tests protect against accidental method deletions during the migration.
/// </summary>
public class CssProviderContractTests
{
    private static readonly Type ContractType = typeof(ISunfishCssProvider);

    [Fact]
    public void ISunfishCssProvider_HasExpectedMethodCount()
    {
        var methods = ContractType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        Assert.True(methods.Length >= 80, $"Expected at least 80 methods, got {methods.Length}");
    }

    [Fact]
    public void ISunfishCssProvider_HasButtonClass()
    {
        var method = ContractType.GetMethod("ButtonClass", [typeof(ButtonVariant), typeof(ButtonSize), typeof(bool), typeof(bool)]);
        Assert.NotNull(method);
        Assert.Equal(typeof(string), method.ReturnType);
    }

    [Fact]
    public void ISunfishCssProvider_HasDataGridClass()
    {
        var method = ContractType.GetMethod("DataGridClass");
        Assert.NotNull(method);
        Assert.Equal(typeof(string), method.ReturnType);
    }

    [Fact]
    public void ISunfishCssProvider_HasAllocationSchedulerMethods()
    {
        var method = ContractType.GetMethod("AllocationSchedulerClass");
        Assert.NotNull(method);
    }

    [Fact]
    public void ISunfishCssProvider_HasResizableContainerHandleClass_WithResizeEdgesParam()
    {
        // Verifies MariloResizeEdges was correctly renamed to ResizeEdges
        var method = ContractType.GetMethod("ResizableContainerHandleClass");
        Assert.NotNull(method);
        var param = method.GetParameters().FirstOrDefault(p => p.Name == "edge");
        Assert.NotNull(param);
        Assert.Equal(typeof(ResizeEdges), param.ParameterType);
    }
}
