using System.Reflection;
using Xunit;

namespace Sunfish.Compat.Infragistics.Tests;

public class IgbDialogTests
{
    [Fact]
    public void IgbDialog_UsesOpenNotVisible()
    {
        // Ignite UI divergence: uses `Open` (not `Visible`) for dialog-visibility state.
        var props = typeof(Sunfish.Compat.Infragistics.IgbDialog).GetProperties(
            BindingFlags.Instance | BindingFlags.Public);
        Assert.Contains(props, p => p.Name == "Open");
        Assert.Contains(props, p => p.Name == "OpenChanged");
    }

    [Fact]
    public void IgbDialog_ExposesKeepOpenOnEscape_PolarityFlipFromTelerik()
    {
        // Ignite UI polarity: KeepOpenOnEscape=true means "Escape does NOT close".
        // Telerik uses the inverse: CloseOnEscape=false means "Escape does NOT close".
        // This test locks the Ignite UI polarity on the shim surface.
        var props = typeof(Sunfish.Compat.Infragistics.IgbDialog).GetProperties(
            BindingFlags.Instance | BindingFlags.Public);
        Assert.Contains(props, p => p.Name == "KeepOpenOnEscape");
        Assert.DoesNotContain(props, p => p.Name == "CloseOnEscape");
    }

    [Fact]
    public void IgbDialog_ExposesShowHideToggleMethods()
    {
        var type = typeof(Sunfish.Compat.Infragistics.IgbDialog);
        Assert.NotNull(type.GetMethod("ShowAsync"));
        Assert.NotNull(type.GetMethod("HideAsync"));
        Assert.NotNull(type.GetMethod("ToggleAsync"));
    }
}
