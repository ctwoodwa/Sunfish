namespace Sunfish.Compat.MaterialIcons;

/// <summary>
/// Starter-set of typed identifier constants for Google Material Icons / Material Symbols.
/// Each member is a lowercase_snake_case string matching the canonical Material ligature
/// identifier, suitable for pass-through to <see cref="MaterialIcon.Name"/> or
/// <see cref="MaterialSymbol.Name"/>.
/// </summary>
/// <remarks>
/// This is intentionally a 50-icon starter set covering the icons most migrators reach
/// for first. The full Material catalog exceeds 2,500 icons. Consumers may pass any
/// valid Material ligature identifier as a string literal to
/// <see cref="MaterialIcon.Name"/> / <see cref="MaterialSymbol.Name"/> directly — e.g.
/// <c>&lt;MaterialIcon Name="rocket_launch" /&gt;</c>. Additions to this class require
/// CODEOWNER sign-off per <c>packages/compat-material-icons/POLICY.md</c>.
/// </remarks>
public static class MaterialIconName
{
    // --- Navigation & chrome ---
    public const string Home = "home";
    public const string Search = "search";
    public const string Settings = "settings";
    public const string Person = "person";
    public const string Menu = "menu";
    public const string Close = "close";
    public const string Check = "check";

    // --- Arrows ---
    public const string ArrowBack = "arrow_back";
    public const string ArrowForward = "arrow_forward";
    public const string ArrowUpward = "arrow_upward";
    public const string ArrowDownward = "arrow_downward";
    public const string ExpandMore = "expand_more";
    public const string ExpandLess = "expand_less";

    // --- Communication ---
    public const string Email = "email";
    public const string Phone = "phone";
    public const string Chat = "chat";

    // --- Scheduling ---
    public const string CalendarToday = "calendar_today";
    public const string Schedule = "schedule";

    // --- Content types ---
    public const string Folder = "folder";
    public const string Description = "description";
    public const string Image = "image";
    public const string Videocam = "videocam";
    public const string MusicNote = "music_note";

    // --- Actions ---
    public const string Save = "save";
    public const string Edit = "edit";
    public const string Delete = "delete";
    public const string Add = "add";
    public const string Favorite = "favorite";
    public const string Bookmark = "bookmark";
    public const string Share = "share";
    public const string ContentCopy = "content_copy";
    public const string Print = "print";
    public const string Download = "download";
    public const string Upload = "upload";

    // --- Status ---
    public const string Info = "info";
    public const string Warning = "warning";
    public const string ErrorOutline = "error_outline";
    public const string CheckCircle = "check_circle";

    // --- Data display ---
    public const string Dashboard = "dashboard";
    public const string GridView = "grid_view";
    public const string ViewList = "view_list";
    public const string FilterList = "filter_list";
    public const string Sort = "sort";

    // --- Media playback ---
    public const string PlayArrow = "play_arrow";
    public const string Pause = "pause";
    public const string Stop = "stop";
    public const string VolumeUp = "volume_up";

    // --- Visibility & security ---
    public const string Visibility = "visibility";
    public const string VisibilityOff = "visibility_off";
    public const string Lock = "lock";
}
