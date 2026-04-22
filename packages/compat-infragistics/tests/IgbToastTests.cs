using System.Reflection;
using Xunit;

namespace Sunfish.Compat.Infragistics.Tests;

public class IgbToastTests
{
    [Fact]
    public void IgbToast_ExposesOpenDisplayTimeKeepOpenPosition()
    {
        var props = typeof(Sunfish.Compat.Infragistics.IgbToast).GetProperties(
            BindingFlags.Instance | BindingFlags.Public);
        Assert.Contains(props, p => p.Name == "Open");
        Assert.Contains(props, p => p.Name == "DisplayTime");
        Assert.Contains(props, p => p.Name == "KeepOpen");
        Assert.Contains(props, p => p.Name == "Position");
    }

    [Fact]
    public void ToastPosition_EnumExposesBottomMiddleTop()
    {
        var members = System.Enum.GetNames(typeof(Sunfish.Compat.Infragistics.Enums.ToastPosition));
        Assert.Contains("Bottom", members);
        Assert.Contains("Middle", members);
        Assert.Contains("Top", members);
    }
}
