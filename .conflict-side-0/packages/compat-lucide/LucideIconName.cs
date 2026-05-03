namespace Sunfish.Compat.Lucide;

/// <summary>
/// Starter-set of typed Lucide icon identifiers. Each member maps to its canonical
/// kebab-case Lucide slug (e.g. <see cref="Home"/> → <c>"home"</c>,
/// <see cref="ChevronUp"/> → <c>"chevron-up"</c>), which is emitted as the
/// <c>lucide-*</c> CSS class on the rendered <c>&lt;i&gt;</c> element — matching the
/// Lucide Blazor wrapper convention (<c>&lt;i class="lucide lucide-home"&gt;&lt;/i&gt;</c>).
/// </summary>
/// <remarks>
/// This is intentionally a 50-icon starter set covering the icons most migrators reach
/// for first. Lucide's upstream catalog exceeds 1,400 icons. Consumers may render icons
/// outside the starter set via the <c>NameString</c> parameter on <see cref="LucideIcon"/>
/// — e.g. <c>&lt;LucideIcon NameString="rocket" /&gt;</c>.
/// Additions to this enum require CODEOWNER sign-off per
/// <c>packages/compat-lucide/POLICY.md</c>.
/// </remarks>
public enum LucideIconName
{
    // Core UI / navigation chrome
    Home,
    Search,
    Settings,
    User,
    Menu,
    X,

    // Checkmarks / direction
    Check,
    ArrowLeft,
    ArrowRight,
    ArrowUp,
    ArrowDown,
    ChevronUp,
    ChevronDown,
    ChevronLeft,
    ChevronRight,

    // Communication
    Mail,
    Phone,
    Calendar,
    Clock,
    MessageCircle,

    // Files / media
    Folder,
    FileText,
    Image,
    Video,
    Music,

    // Editing actions
    Save,
    Edit,
    Trash2,
    Plus,
    Minus,

    // Social / sharing
    Heart,
    Bookmark,
    Share2,
    Copy,
    Printer,

    // Transfer
    Download,
    Upload,

    // Status / alerts
    Info,
    AlertTriangle,
    AlertCircle,
    CheckCircle,

    // Data / layout
    LayoutGrid,
    List,
    Filter,
    ArrowDownUp,

    // Media control
    Play,
    Pause,
    Square,
    Eye,
    Lock
}
