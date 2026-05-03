using System.Reflection;
using Xunit;

namespace Sunfish.Compat.Infragistics.Tests;

public class IgbDatePickerTests
{
    [Fact]
    public void IgbDatePicker_TypeIsPublicAndInRootNamespace()
    {
        var type = typeof(Sunfish.Compat.Infragistics.IgbDatePicker);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.Compat.Infragistics", type.Namespace);
    }

    [Fact]
    public void IgbDatePicker_ExposesValueMinMaxDisabled()
    {
        var props = typeof(Sunfish.Compat.Infragistics.IgbDatePicker).GetProperties(
            BindingFlags.Instance | BindingFlags.Public);
        Assert.Contains(props, p => p.Name == "Value");
        Assert.Contains(props, p => p.Name == "Min");
        Assert.Contains(props, p => p.Name == "Max");
        Assert.Contains(props, p => p.Name == "Disabled");
        Assert.Contains(props, p => p.Name == "DisplayFormat");
    }

    [Fact]
    public void PickerMode_EnumExposesDialogDropDown()
    {
        var members = System.Enum.GetNames(typeof(Sunfish.Compat.Infragistics.Enums.PickerMode));
        Assert.Contains("Dialog", members);
        Assert.Contains("DropDown", members);
    }
}
