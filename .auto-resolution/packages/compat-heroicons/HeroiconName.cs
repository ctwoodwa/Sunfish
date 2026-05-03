namespace Sunfish.Compat.Heroicons;

/// <summary>
/// Starter-set of typed Heroicons identifiers. Each member maps to its canonical
/// kebab-case Heroicons slug (e.g. <see cref="Home"/> → <c>"home"</c>,
/// <see cref="MagnifyingGlass"/> → <c>"magnifying-glass"</c>), which is emitted as
/// the <c>heroicon-*</c> CSS class on the rendered <c>&lt;i&gt;</c> element.
/// </summary>
/// <remarks>
/// This is intentionally a 50-icon starter set covering the icons most migrators
/// reach for first. The full Heroicons catalog exceeds 300 icons across all three
/// variants. Consumers may render icons outside the starter set via the
/// <c>NameString</c> parameter on <see cref="Heroicon"/> — e.g.
/// <c>&lt;Heroicon NameString="academic-cap" /&gt;</c>.
/// Additions to this enum require CODEOWNER sign-off per
/// <c>packages/compat-heroicons/POLICY.md</c>.
/// </remarks>
public enum HeroiconName
{
    // --- Navigation & chrome ---
    Home,
    MagnifyingGlass,
    Cog6Tooth,
    User,
    Bars3,
    XMark,
    Check,

    // --- Arrows ---
    ArrowLeft,
    ArrowRight,
    ArrowUp,
    ArrowDown,
    ChevronUp,
    ChevronDown,
    ChevronLeft,
    ChevronRight,

    // --- Communication ---
    Envelope,
    Phone,
    Calendar,
    Clock,
    ChatBubbleLeft,

    // --- Content types ---
    Folder,
    Document,
    Photo,
    Film,
    MusicalNote,
    BookmarkSquare,

    // --- Editing actions ---
    Pencil,
    Trash,
    Plus,
    Minus,

    // --- Social / sharing ---
    Heart,
    Bookmark,
    Share,
    DocumentDuplicate,
    Printer,

    // --- Transfer ---
    ArrowDownTray,
    ArrowUpTray,

    // --- Status / alerts ---
    InformationCircle,
    ExclamationTriangle,
    XCircle,
    CheckCircle,

    // --- Data / layout ---
    Squares2x2,
    ListBullet,
    Funnel,
    ArrowsUpDown,

    // --- Media control ---
    Play,
    Pause,
    Stop,

    // --- Visibility & security ---
    Eye,
    LockClosed
}
