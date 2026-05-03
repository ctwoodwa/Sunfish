using System.Reflection;
using Xunit;

namespace Sunfish.Compat.Infragistics.Tests;

public class IgbInputTests
{
    [Fact]
    public void IgbInput_ExposesTextPasswordEmailInputSurface()
    {
        var props = typeof(Sunfish.Compat.Infragistics.IgbInput).GetProperties(
            BindingFlags.Instance | BindingFlags.Public);
        Assert.Contains(props, p => p.Name == "Value");
        Assert.Contains(props, p => p.Name == "DisplayType");
        Assert.Contains(props, p => p.Name == "Label");
        Assert.Contains(props, p => p.Name == "Placeholder");
        Assert.Contains(props, p => p.Name == "Required");
        Assert.Contains(props, p => p.Name == "Disabled");
        Assert.Contains(props, p => p.Name == "Readonly");
    }

    [Fact]
    public void InputType_EnumExposesTextEmailPasswordNumber()
    {
        var members = System.Enum.GetNames(typeof(Sunfish.Compat.Infragistics.Enums.InputType));
        Assert.Contains("Text", members);
        Assert.Contains("Email", members);
        Assert.Contains("Password", members);
        Assert.Contains("Number", members);
    }
}
