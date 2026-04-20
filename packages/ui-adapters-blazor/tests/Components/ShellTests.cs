using Sunfish.UIAdapters.Blazor.Shell;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.Tests.Components;

public class ShellTests
{
    [Fact]
    public void SunfishAppShell_TypeIsPublicAndInShellNamespace()
    {
        var type = typeof(SunfishAppShell);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.UIAdapters.Blazor.Shell", type.Namespace);
    }

    [Fact]
    public void AllExpectedShellTypes_AreInShellNamespace()
    {
        var expected = new[]
        {
            typeof(SunfishAppShell),
            typeof(SunfishAppShellNavGroup),
            typeof(SunfishAppShellNavLink),
            typeof(SunfishAppShellSlideOver),
            typeof(SunfishAccountMenu),
            typeof(SunfishUserMenu),
            typeof(SunfishNotificationBell),
            typeof(AccountMenuItemModel),
            typeof(AccountMenuItemOptions),
            typeof(AccountMenuOptions),
            typeof(AppearanceMenuContext),
            typeof(LanguageMenuContext),
            typeof(HelpMenuContext),
            typeof(PopupMenuItem),
            typeof(NotificationItem),
        };
        foreach (var t in expected)
        {
            Assert.Equal("Sunfish.UIAdapters.Blazor.Shell", t.Namespace);
        }
    }
}
