namespace Sunfish.Compat.FontAwesome;

/// <summary>
/// Starter-set of typed icon identifiers for Font Awesome's <c>Solid</c> style family
/// (<c>fas</c>). Each member is a kebab-case string matching the canonical Font Awesome
/// icon name, suitable for pass-through to <see cref="Sunfish.UICore.Contracts.ISunfishIconProvider.GetIcon(string, Sunfish.Foundation.Enums.IconSize)"/>
/// via the active Sunfish icon provider.
/// </summary>
/// <remarks>
/// This is intentionally a 50-icon starter set covering the icons most migrators reach
/// for first. The full FA catalog exceeds 2,000 icons. Consumers may add additional
/// identifiers by passing a string literal to <see cref="FontAwesomeIcon.Icon"/>
/// directly — e.g. <c>&lt;FontAwesomeIcon Icon="@(&quot;rocket&quot;)" /&gt;</c>.
/// Additions to this class require CODEOWNER sign-off per
/// <c>packages/compat-font-awesome/POLICY.md</c>.
/// </remarks>
public static class FasIcons
{
    // Core UI
    public static readonly string Star = "star";
    public static readonly string Heart = "heart";
    public static readonly string Home = "home";
    public static readonly string User = "user";
    public static readonly string Search = "search";
    public static readonly string Bars = "bars";
    public static readonly string Times = "times";
    public static readonly string Check = "check";
    public static readonly string Plus = "plus";
    public static readonly string Minus = "minus";

    // Navigation arrows
    public static readonly string ArrowUp = "arrow-up";
    public static readonly string ArrowDown = "arrow-down";
    public static readonly string ArrowLeft = "arrow-left";
    public static readonly string ArrowRight = "arrow-right";
    public static readonly string ChevronUp = "chevron-up";
    public static readonly string ChevronDown = "chevron-down";
    public static readonly string ChevronLeft = "chevron-left";
    public static readonly string ChevronRight = "chevron-right";

    // Editing / file actions
    public static readonly string Edit = "edit";
    public static readonly string Trash = "trash";
    public static readonly string Download = "download";
    public static readonly string Upload = "upload";
    public static readonly string Save = "save";
    public static readonly string Copy = "copy";
    public static readonly string Paste = "paste";
    public static readonly string Print = "print";
    public static readonly string Share = "share";
    public static readonly string Link = "link";

    // Settings / state
    public static readonly string Cog = "cog";
    public static readonly string Bell = "bell";
    public static readonly string Envelope = "envelope";
    public static readonly string Phone = "phone";
    public static readonly string Calendar = "calendar";
    public static readonly string Clock = "clock";
    public static readonly string Eye = "eye";
    public static readonly string Lock = "lock";
    public static readonly string Unlock = "unlock";
    public static readonly string Key = "key";

    // Files / media
    public static readonly string File = "file";
    public static readonly string Folder = "folder";
    public static readonly string Image = "image";
    public static readonly string Comment = "comment";
    public static readonly string ThumbsUp = "thumbs-up";

    // Media control
    public static readonly string Play = "play";
    public static readonly string Pause = "pause";
    public static readonly string Stop = "stop";
    public static readonly string Forward = "forward";
    public static readonly string Backward = "backward";

    // Data / status
    public static readonly string ChartBar = "chart-bar";
    public static readonly string ChartLine = "chart-line";
    public static readonly string ChartPie = "chart-pie";
    public static readonly string Sync = "sync";
}
