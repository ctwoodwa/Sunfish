namespace Sunfish.UIAdapters.Blazor.Shell;

/// <summary>
/// Template context handed to consumers that customize the appearance submenu
/// of the account menu. Exposes the active mode, the available choices, and the
/// callbacks needed to select a mode or unwind the menu.
/// </summary>
/// <param name="CurrentMode">The currently active appearance mode (one of <paramref name="Modes"/>).</param>
/// <param name="Modes">The available appearance modes (for example, "Light", "Dark", "System").</param>
/// <param name="SelectMode">Callback invoked when the user picks a mode; receives the chosen mode name.</param>
/// <param name="GoBack">Callback that returns to the parent menu without closing the popup.</param>
/// <param name="CloseMenu">Callback that closes the entire account menu.</param>
public record AppearanceMenuContext(
    string CurrentMode,
    IReadOnlyList<string> Modes,
    Func<string, Task> SelectMode,
    Func<Task> GoBack,
    Func<Task> CloseMenu);

/// <summary>
/// Template context handed to consumers that customize the language submenu
/// of the account menu.
/// </summary>
/// <param name="CurrentLanguage">The currently active language (one of <paramref name="Languages"/>).</param>
/// <param name="Languages">The available languages.</param>
/// <param name="SelectLanguage">Callback invoked when the user picks a language; receives the chosen language name.</param>
/// <param name="GoBack">Callback that returns to the parent menu without closing the popup.</param>
/// <param name="CloseMenu">Callback that closes the entire account menu.</param>
public record LanguageMenuContext(
    string CurrentLanguage,
    IReadOnlyList<string> Languages,
    Func<string, Task> SelectLanguage,
    Func<Task> GoBack,
    Func<Task> CloseMenu);

/// <summary>
/// Template context handed to consumers that customize the help submenu
/// of the account menu.
/// </summary>
/// <param name="DefaultItems">The default help items the shell would render.</param>
/// <param name="InvokeItem">Callback that invokes a help item's action / navigation.</param>
/// <param name="GoBack">Callback that returns to the parent menu without closing the popup.</param>
/// <param name="CloseMenu">Callback that closes the entire account menu.</param>
public record HelpMenuContext(
    IReadOnlyList<AccountMenuItemModel> DefaultItems,
    Func<AccountMenuItemModel, Task> InvokeItem,
    Func<Task> GoBack,
    Func<Task> CloseMenu);
