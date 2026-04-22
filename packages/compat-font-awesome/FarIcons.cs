namespace Sunfish.Compat.FontAwesome;

/// <summary>
/// Starter-set of typed icon identifiers for Font Awesome's <c>Regular</c> style family
/// (<c>far</c>). Each member is a kebab-case string matching the canonical Font Awesome
/// icon name.
/// </summary>
/// <remarks>
/// Regular-style FA Free has a smaller catalog than Solid (most Regular icons are
/// Pro-only). This class ships the 50 most commonly used Regular-style names; several
/// overlap with the Solid names — they're surfaced in both classes so consumer markup
/// keeps its source shape after the using swap. Additions require CODEOWNER sign-off
/// per <c>packages/compat-font-awesome/POLICY.md</c>.
/// </remarks>
public static class FarIcons
{
    // Core UI
    public static readonly string Star = "star";
    public static readonly string Heart = "heart";
    public static readonly string User = "user";
    public static readonly string Bell = "bell";
    public static readonly string Circle = "circle";
    public static readonly string Square = "square";
    public static readonly string CheckCircle = "check-circle";
    public static readonly string TimesCircle = "times-circle";
    public static readonly string PlusSquare = "plus-square";
    public static readonly string MinusSquare = "minus-square";

    // Files / documents
    public static readonly string File = "file";
    public static readonly string FileAlt = "file-alt";
    public static readonly string Folder = "folder";
    public static readonly string FolderOpen = "folder-open";
    public static readonly string Image = "image";
    public static readonly string Images = "images";
    public static readonly string Clipboard = "clipboard";
    public static readonly string Bookmark = "bookmark";
    public static readonly string Copy = "copy";

    // Calendar
    public static readonly string Calendar = "calendar";
    public static readonly string CalendarAlt = "calendar-alt";
    public static readonly string CalendarCheck = "calendar-check";
    public static readonly string CalendarPlus = "calendar-plus";
    public static readonly string CalendarMinus = "calendar-minus";
    public static readonly string CalendarTimes = "calendar-times";
    public static readonly string Clock = "clock";

    // State / control
    public static readonly string Eye = "eye";
    public static readonly string EyeSlash = "eye-slash";
    public static readonly string Edit = "edit";
    public static readonly string TrashAlt = "trash-alt";
    public static readonly string SaveAlt = "save";
    public static readonly string Comment = "comment";
    public static readonly string Comments = "comments";
    public static readonly string Envelope = "envelope";
    public static readonly string EnvelopeOpen = "envelope-open";
    public static readonly string Paperplane = "paper-plane";

    // Emoji-ish
    public static readonly string SmileFace = "smile";
    public static readonly string FrownFace = "frown";
    public static readonly string MehFace = "meh";
    public static readonly string LaughFace = "laugh";
    public static readonly string AngryFace = "angry";

    // Misc
    public static readonly string ThumbsUp = "thumbs-up";
    public static readonly string ThumbsDown = "thumbs-down";
    public static readonly string Handshake = "handshake";
    public static readonly string Lightbulb = "lightbulb";
    public static readonly string Moon = "moon";
    public static readonly string Sun = "sun";
    public static readonly string Flag = "flag";
    public static readonly string Address_Book = "address-book";
    public static readonly string AddressCard = "address-card";
    public static readonly string IdBadge = "id-badge";
}
