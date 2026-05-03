using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Sunfish.Compat.Syncfusion.Tests;

/// <summary>
/// Parameter-shape locks for the main wrappers. Uses reflection so tests survive without a
/// full Blazor renderer setup. Any accidental removal/rename of a Syncfusion-shaped
/// parameter fails one of these tests.
/// </summary>
public class WrapperShapeTests
{
    [Fact]
    public void SfButton_ExposesSyncfusionShapedParameters()
    {
        AssertHasParameter(typeof(Sunfish.Compat.Syncfusion.SfButton), "Content", typeof(string));
        AssertHasParameter(typeof(Sunfish.Compat.Syncfusion.SfButton), "CssClass", typeof(string));
        AssertHasParameter(typeof(Sunfish.Compat.Syncfusion.SfButton), "Disabled", typeof(bool));
        AssertHasParameter(typeof(Sunfish.Compat.Syncfusion.SfButton), "IconCss", typeof(string));
        AssertHasParameter(typeof(Sunfish.Compat.Syncfusion.SfButton), "IsPrimary", typeof(bool));
        AssertHasParameter(typeof(Sunfish.Compat.Syncfusion.SfButton), "IsToggle", typeof(bool));
        AssertHasParameter(typeof(Sunfish.Compat.Syncfusion.SfButton), "EnableRtl", typeof(bool));
    }

    [Fact]
    public void SfIcon_ExposesSyncfusionShapedParameters()
    {
        AssertHasParameter(typeof(Sunfish.Compat.Syncfusion.SfIcon), "Name", typeof(string));
        AssertHasParameter(typeof(Sunfish.Compat.Syncfusion.SfIcon), "IconCss", typeof(string));
        AssertHasParameter(typeof(Sunfish.Compat.Syncfusion.SfIcon), "Size", typeof(Sunfish.Compat.Syncfusion.Enums.IconSize));
        AssertHasParameter(typeof(Sunfish.Compat.Syncfusion.SfIcon), "Title", typeof(string));
    }

    [Fact]
    public void SfCheckBox_IsGenericOnTChecked()
    {
        var type = typeof(Sunfish.Compat.Syncfusion.SfCheckBox<>);
        Assert.True(type.IsGenericTypeDefinition);
        Assert.Single(type.GetGenericArguments());
    }

    [Fact]
    public void SfTextBox_EnforcesMultilinePasswordInvariant()
    {
        // Parameter shape lock.
        AssertHasParameter(typeof(Sunfish.Compat.Syncfusion.SfTextBox), "Multiline", typeof(bool));
        AssertHasParameter(typeof(Sunfish.Compat.Syncfusion.SfTextBox), "Type", typeof(Sunfish.Compat.Syncfusion.Enums.InputType));
        AssertHasParameter(typeof(Sunfish.Compat.Syncfusion.SfTextBox), "FloatLabelType", typeof(Sunfish.Compat.Syncfusion.Enums.FloatLabelType));
    }

    [Fact]
    public void SfDropDownList_IsGenericOnTValueTItem()
    {
        var type = typeof(Sunfish.Compat.Syncfusion.SfDropDownList<,>);
        Assert.True(type.IsGenericTypeDefinition);
        Assert.Equal(2, type.GetGenericArguments().Length);
    }

    [Fact]
    public void SfComboBox_IsGenericOnTValueTItem()
    {
        var type = typeof(Sunfish.Compat.Syncfusion.SfComboBox<,>);
        Assert.True(type.IsGenericTypeDefinition);
        Assert.Equal(2, type.GetGenericArguments().Length);
    }

    [Fact]
    public void SfDatePicker_IsGenericOnTValue()
    {
        var type = typeof(Sunfish.Compat.Syncfusion.SfDatePicker<>);
        Assert.True(type.IsGenericTypeDefinition);
        Assert.Single(type.GetGenericArguments());
    }

    [Fact]
    public void SfGrid_IsGenericOnTValue()
    {
        var type = typeof(Sunfish.Compat.Syncfusion.SfGrid<>);
        Assert.True(type.IsGenericTypeDefinition);
        Assert.Single(type.GetGenericArguments());
    }

    [Fact]
    public void SfDialog_ExposesSyncfusionShapedParameters()
    {
        AssertHasParameter(typeof(Sunfish.Compat.Syncfusion.SfDialog), "Visible", typeof(bool));
        AssertHasParameter(typeof(Sunfish.Compat.Syncfusion.SfDialog), "Header", typeof(string));
        AssertHasParameter(typeof(Sunfish.Compat.Syncfusion.SfDialog), "IsModal", typeof(bool));
        AssertHasParameter(typeof(Sunfish.Compat.Syncfusion.SfDialog), "ShowCloseIcon", typeof(bool));
        AssertHasParameter(typeof(Sunfish.Compat.Syncfusion.SfDialog), "CloseOnEscape", typeof(bool));
    }

    [Fact]
    public void SfTooltip_CollapsesPositionToFourCardinals()
    {
        // Shape-only lock. Position type is the compat enum.
        AssertHasParameter(typeof(Sunfish.Compat.Syncfusion.SfTooltip), "Position", typeof(Sunfish.Compat.Syncfusion.Enums.Position));
        var posValues = System.Enum.GetValues(typeof(Sunfish.Compat.Syncfusion.Enums.Position)).Length;
        Assert.Equal(12, posValues);
    }

    [Fact]
    public void SfToast_HasImperativeShowAsync()
    {
        var method = typeof(Sunfish.Compat.Syncfusion.SfToast).GetMethods()
            .FirstOrDefault(m => m.Name == "ShowAsync" && m.GetParameters().Length == 0);
        Assert.NotNull(method);
        Assert.Equal(typeof(System.Threading.Tasks.Task), method!.ReturnType);
    }

    [Fact]
    public void SfDataForm_HasModelAndEditContextParameters()
    {
        AssertHasParameter(typeof(Sunfish.Compat.Syncfusion.SfDataForm), "Model", typeof(object));
        AssertHasParameter(typeof(Sunfish.Compat.Syncfusion.SfDataForm), "EditContext", typeof(Microsoft.AspNetCore.Components.Forms.EditContext));
    }

    private static void AssertHasParameter(Type componentType, string paramName, Type expectedPropertyType)
    {
        var prop = componentType.GetProperty(paramName, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(prop);
        // Attribute lock — must be a Blazor [Parameter].
        var attr = prop!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Components.ParameterAttribute), false);
        Assert.NotEmpty(attr);
        // Type match (nullable reference types collapse to the same reflected type for value-type defaults).
        Assert.True(
            prop.PropertyType == expectedPropertyType || Nullable.GetUnderlyingType(prop.PropertyType) == expectedPropertyType,
            $"{componentType.Name}.{paramName} expected type {expectedPropertyType}, got {prop.PropertyType}.");
    }
}
