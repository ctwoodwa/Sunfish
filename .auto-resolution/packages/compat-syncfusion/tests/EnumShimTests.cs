using Xunit;

namespace Sunfish.Compat.Syncfusion.Tests;

/// <summary>
/// Shape locks for the Syncfusion-shaped enum shims. Adding, renaming, or reordering values
/// is a policy-gated breaking change.
/// </summary>
public class EnumShimTests
{
    [Fact]
    public void IconPosition_HasExpectedMembers()
    {
        AssertHasMembers<Sunfish.Compat.Syncfusion.Enums.IconPosition>("Left", "Right", "Top", "Bottom");
    }

    [Fact]
    public void IconSize_HasSmallMediumLarge()
    {
        AssertHasMembers<Sunfish.Compat.Syncfusion.Enums.IconSize>("Small", "Medium", "Large");
    }

    [Fact]
    public void InputType_HasExpectedMembers()
    {
        AssertHasMembers<Sunfish.Compat.Syncfusion.Enums.InputType>(
            "Text", "Password", "Email", "Number", "Tel", "Url", "Search");
    }

    [Fact]
    public void FilterType_HasExpectedMembers()
    {
        AssertHasMembers<Sunfish.Compat.Syncfusion.Enums.FilterType>(
            "FilterBar", "Excel", "Menu", "CheckBox");
    }

    [Fact]
    public void SelectionMode_HasRowCellBoth()
    {
        AssertHasMembers<Sunfish.Compat.Syncfusion.Enums.SelectionMode>("Row", "Cell", "Both");
    }

    [Fact]
    public void SelectionType_HasSingleMultiple()
    {
        AssertHasMembers<Sunfish.Compat.Syncfusion.Enums.SelectionType>("Single", "Multiple");
    }

    [Fact]
    public void Position_HasTwelveSyncfusionValues()
    {
        var members = System.Enum.GetNames(typeof(Sunfish.Compat.Syncfusion.Enums.Position));
        Assert.Equal(12, members.Length);
        Assert.Contains("TopLeft", members);
        Assert.Contains("BottomRight", members);
        Assert.Contains("LeftCenter", members);
        Assert.Contains("RightCenter", members);
    }

    [Fact]
    public void CalendarView_HasMonthYearDecade()
    {
        AssertHasMembers<Sunfish.Compat.Syncfusion.Enums.CalendarView>("Month", "Year", "Decade");
    }

    [Fact]
    public void ValidationDisplayMode_HasInlineToolTipNone()
    {
        AssertHasMembers<Sunfish.Compat.Syncfusion.Enums.ValidationDisplayMode>(
            "Inline", "ToolTip", "None");
    }

    private static void AssertHasMembers<TEnum>(params string[] expected) where TEnum : struct, System.Enum
    {
        var members = System.Enum.GetNames(typeof(TEnum));
        foreach (var e in expected)
        {
            Assert.Contains(e, members);
        }
    }
}
