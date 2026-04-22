namespace Sunfish.Compat.TablerIcons;

/// <summary>
/// Starter-set of typed Tabler Icons identifiers. Each member maps to its canonical
/// kebab-case Tabler slug (e.g. <see cref="Home"/> → <c>"home"</c>,
/// <see cref="InfoCircle"/> → <c>"info-circle"</c>,
/// <see cref="Menu2"/> → <c>"menu-2"</c>), which is emitted as the
/// <c>tabler-*</c> CSS class on the rendered <c>&lt;i&gt;</c> element — matching the
/// Tabler Icons Blazor wrapper convention (<c>&lt;i class="tabler tabler-home"&gt;&lt;/i&gt;</c>).
/// </summary>
/// <remarks>
/// This is intentionally a 50-icon starter set covering the icons most migrators reach
/// for first. Tabler's upstream catalog exceeds 5,000 icons. Consumers may render icons
/// outside the starter set via the <c>NameString</c> parameter on <see cref="TablerIcon"/>
/// — e.g. <c>&lt;TablerIcon NameString="rocket" /&gt;</c>.
/// Additions to this enum require CODEOWNER sign-off per
/// <c>packages/compat-tabler-icons/POLICY.md</c>.
/// </remarks>
public enum TablerIconName
{
    // Core UI / navigation chrome
    Home,
    Search,
    Settings,
    User,
    Menu2,
    X,

    // Checkmarks / direction
    Check,
    ChevronUp,
    ChevronDown,
    ChevronLeft,
    ChevronRight,
    ArrowUp,
    ArrowDown,
    ArrowLeft,
    ArrowRight,

    // Communication
    Mail,
    Phone,
    Calendar,
    Clock,
    MessageCircle,

    // Files / media
    Folder,
    FileText,
    Photo,
    Video,
    Music,

    // Editing actions
    DeviceFloppy,
    Pencil,
    Trash,
    Plus,
    Minus,

    // Social / sharing
    Heart,
    Bookmark,
    Share,
    Copy,
    Printer,

    // Transfer
    Download,
    Upload,

    // Status / alerts
    InfoCircle,
    AlertTriangle,
    AlertCircle,
    CircleCheck,

    // Data / layout
    LayoutGrid,
    List,
    Filter,
    ArrowsSort,

    // Media control
    PlayerPlay,
    PlayerPause,
    PlayerStop,
    Eye,
    EyeOff,
    Lock,
}
