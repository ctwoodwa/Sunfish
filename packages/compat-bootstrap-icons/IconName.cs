namespace Sunfish.Compat.BootstrapIcons;

/// <summary>
/// Starter-set of typed Bootstrap Icons identifiers. Each member maps to its canonical
/// kebab-case Bootstrap Icons slug (e.g. <see cref="House"/> → <c>"house"</c>,
/// <see cref="ArrowUp"/> → <c>"arrow-up"</c>), which is emitted as the <c>bi-*</c> CSS
/// class on the rendered <c>&lt;i&gt;</c> element — matching upstream Bootstrap Icons
/// markup (<c>&lt;i class="bi bi-house"&gt;&lt;/i&gt;</c>).
/// </summary>
/// <remarks>
/// This is intentionally a 50-icon starter set covering the icons most migrators reach
/// for first. Bootstrap Icons' upstream catalog exceeds 2,000 icons. Consumers may
/// render icons outside the starter set via the <c>NameString</c> parameter on
/// <see cref="BootstrapIcon"/> — e.g.
/// <c>&lt;BootstrapIcon NameString="rocket-takeoff" /&gt;</c>.
/// Additions to this enum require CODEOWNER sign-off per
/// <c>packages/compat-bootstrap-icons/POLICY.md</c>.
/// </remarks>
public enum IconName
{
    // Core UI / navigation chrome
    House,
    Search,
    Gear,
    Person,
    List,
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
    Envelope,
    Telephone,
    Calendar,
    Clock,
    Chat,

    // Files / media
    Folder,
    FileText,
    Image,
    CameraVideo,
    MusicNote,

    // Editing actions
    Save,
    Pencil,
    Trash,
    Plus,
    Dash,

    // Social / sharing
    Heart,
    Bookmark,
    Share,
    Clipboard,
    Printer,

    // Transfer
    Download,
    Upload,

    // Status / alerts
    InfoCircle,
    ExclamationTriangle,
    XCircle,
    CheckCircle,

    // Data / layout
    Grid,
    BarChart,
    Filter,
    SortAlphaDown,

    // Media control
    PlayFill,
    PauseFill,
    StopFill,
    VolumeUp,
    Eye
}
