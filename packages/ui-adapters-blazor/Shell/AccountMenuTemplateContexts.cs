namespace Sunfish.UIAdapters.Blazor.Shell;

public record AppearanceMenuContext(
    string CurrentMode,
    IReadOnlyList<string> Modes,
    Func<string, Task> SelectMode,
    Func<Task> GoBack,
    Func<Task> CloseMenu);

public record LanguageMenuContext(
    string CurrentLanguage,
    IReadOnlyList<string> Languages,
    Func<string, Task> SelectLanguage,
    Func<Task> GoBack,
    Func<Task> CloseMenu);

public record HelpMenuContext(
    IReadOnlyList<AccountMenuItemModel> DefaultItems,
    Func<AccountMenuItemModel, Task> InvokeItem,
    Func<Task> GoBack,
    Func<Task> CloseMenu);
