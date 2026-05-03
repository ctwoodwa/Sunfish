using System.Reflection;
using Xunit;

namespace Sunfish.Compat.Infragistics.Tests;

public class IgbCheckboxTests
{
    [Fact]
    public void IgbCheckbox_UsesCheckedNotValue()
    {
        // Ignite UI divergence: uses `Checked` (not `Value`) for the boolean state.
        var props = typeof(Sunfish.Compat.Infragistics.IgbCheckbox).GetProperties(
            BindingFlags.Instance | BindingFlags.Public);
        Assert.Contains(props, p => p.Name == "Checked");
        Assert.Contains(props, p => p.Name == "CheckedChanged");
        Assert.Contains(props, p => p.Name == "Indeterminate");
        Assert.Contains(props, p => p.Name == "Disabled");
    }

    [Fact]
    public void LabelPosition_EnumExposesBeforeAfter()
    {
        var members = System.Enum.GetNames(typeof(Sunfish.Compat.Infragistics.Enums.LabelPosition));
        Assert.Contains("Before", members);
        Assert.Contains("After", members);
    }
}
