namespace Sunfish.Components.Blazor.Shell;

public class AccountMenuItemModel
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Icon { get; set; } = "";
    public string? SecondaryText { get; set; }
    public string? ShortcutText { get; set; }
    public string? RightValueText { get; set; }
    public string? Badge { get; set; }
    public string? Href { get; set; }
    public Func<Task>? Action { get; set; }
    public List<AccountMenuItemModel>? Children { get; set; }
    public bool IsVisible { get; set; } = true;
    public bool IsDisabled { get; set; }
    public bool IsDestructive { get; set; }
    public bool IsSelected { get; set; }
    public bool IsSubmenu { get; set; }
    public int SortOrder { get; set; }
    public string? TestId { get; set; }
}
