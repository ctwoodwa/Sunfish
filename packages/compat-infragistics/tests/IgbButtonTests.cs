using System.Reflection;
using Xunit;

namespace Sunfish.Compat.Infragistics.Tests;

public class IgbButtonTests
{
    [Fact]
    public void IgbButton_ExposesExpectedIgniteUiShapedParameters()
    {
        var props = typeof(Sunfish.Compat.Infragistics.IgbButton).GetProperties(
            BindingFlags.Instance | BindingFlags.Public);

        // Core Igb button surface — must be present for consumer source-shape parity.
        Assert.Contains(props, p => p.Name == "Variant");
        Assert.Contains(props, p => p.Name == "Type");
        Assert.Contains(props, p => p.Name == "Disabled");
        Assert.Contains(props, p => p.Name == "Size");
        Assert.Contains(props, p => p.Name == "Href");
        Assert.Contains(props, p => p.Name == "Click");
    }

    [Fact]
    public void ButtonVariant_EnumExposesFlatContainedOutlinedFab()
    {
        var members = System.Enum.GetNames(typeof(Sunfish.Compat.Infragistics.Enums.ButtonVariant));
        Assert.Contains("Flat", members);
        Assert.Contains("Contained", members);
        Assert.Contains("Outlined", members);
        Assert.Contains("Fab", members);
    }

    [Fact]
    public void ButtonBaseType_EnumExposesButtonSubmitReset()
    {
        // Reset is publicly exposed in the enum but throws at runtime — this test locks the
        // compat enum surface.
        var members = System.Enum.GetNames(typeof(Sunfish.Compat.Infragistics.Enums.ButtonBaseType));
        Assert.Contains("Button", members);
        Assert.Contains("Submit", members);
        Assert.Contains("Reset", members);
    }
}
