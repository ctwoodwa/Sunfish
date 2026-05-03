using System.Reflection;
using Xunit;

namespace Sunfish.Compat.Infragistics.Tests;

public class IgbIconTests
{
    [Fact]
    public void IgbIcon_ExposesRegisterIconFromTextStaticMethod()
    {
        // Ignite UI-specific: registry is a static method. compat-infragistics reimplements
        // it as a process-local dictionary to avoid requiring the WC runtime.
        var method = typeof(Sunfish.Compat.Infragistics.IgbIcon)
            .GetMethod("RegisterIconFromText", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
    }

    [Fact]
    public void IgbIcon_ExposesRegisterIconStaticMethod()
    {
        var method = typeof(Sunfish.Compat.Infragistics.IgbIcon)
            .GetMethod("RegisterIcon", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
    }

    [Fact]
    public void IgbIcon_ExposesIconNameAndCollectionParameters()
    {
        var props = typeof(Sunfish.Compat.Infragistics.IgbIcon).GetProperties(
            BindingFlags.Instance | BindingFlags.Public);
        Assert.Contains(props, p => p.Name == "IconName");
        Assert.Contains(props, p => p.Name == "Collection");
    }
}
