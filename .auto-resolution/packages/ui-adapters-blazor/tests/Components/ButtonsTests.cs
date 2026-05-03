using Sunfish.UIAdapters.Blazor.Components.Buttons;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.Tests.Components;

public class ButtonsTests
{
    [Fact]
    public void SunfishButton_TypeIsPublicAndInNamespace()
    {
        var type = typeof(SunfishButton);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.UIAdapters.Blazor.Components.Buttons", type.Namespace);
    }

    [Fact]
    public void AllExpectedButtonTypes_AreInNamespace()
    {
        var expected = new[]
        {
            typeof(SunfishButton), typeof(SunfishButtonGroup), typeof(SunfishChip),
            typeof(SunfishChipSet<>), typeof(SunfishFab), typeof(SunfishIconButton),
            typeof(SunfishSegmentedControl), typeof(SunfishSplitButton),
            typeof(SunfishToggleButton),
        };
        foreach (var t in expected)
        {
            Assert.Equal("Sunfish.UIAdapters.Blazor.Components.Buttons", t.Namespace);
        }
        // SunfishSmartPasteButton and SunfishSpeechToTextButton intentionally live
        // in Components.AI, not Components.Buttons — they are asserted elsewhere.
    }
}
