namespace Sunfish.Compat.Octicons;

/// <summary>
/// Starter-set of typed Octicons icon identifiers. Each member maps to its canonical
/// kebab-case Octicons slug (e.g. <see cref="MarkGithub"/> → <c>"mark-github"</c>,
/// <see cref="IssueOpened"/> → <c>"issue-opened"</c>), which is emitted as the
/// <c>octicon-*</c> CSS class on the rendered <c>&lt;i&gt;</c> element — matching the
/// GitHub Primer Octicons convention (<c>&lt;i class="octicon octicon-mark-github"&gt;&lt;/i&gt;</c>).
/// </summary>
/// <remarks>
/// This is intentionally a 50-icon starter set covering the icons most migrators reach
/// for first. Octicons' upstream catalog contains ~270 icons. Consumers may render icons
/// outside the starter set via the <c>NameString</c> parameter on <see cref="Octicon"/>
/// — e.g. <c>&lt;Octicon NameString="rocket" /&gt;</c>.
/// Additions to this enum require CODEOWNER sign-off per
/// <c>packages/compat-octicons/POLICY.md</c>.
/// </remarks>
public enum OcticonName
{
    // GitHub-branded core (the reason most migrators pick Octicons)
    MarkGithub,
    Repo,
    GitBranch,
    GitCommit,
    GitMerge,
    GitPullRequest,
    IssueOpened,
    IssueClosed,

    // Checkmarks / direction
    Check,
    X,
    ChevronUp,
    ChevronDown,
    ChevronLeft,
    ChevronRight,
    ArrowUp,
    ArrowDown,
    ArrowLeft,
    ArrowRight,

    // Core UI / navigation chrome
    Home,
    Gear,
    Person,
    People,
    Organization,

    // Security / access
    Key,
    Lock,
    Unlock,
    Eye,
    EyeClosed,

    // Social / bookmarking
    Heart,
    Star,
    StarFill,
    Bookmark,
    BookmarkFill,

    // Data / layout
    Search,
    Filter,
    Sort,

    // Transfer
    Download,
    Upload,

    // Editing actions
    Pencil,
    Trash,
    Plus,
    PlusCircle,
    Dash,

    // Communication / notification
    Comment,
    Mail,
    Bell,

    // Status / alerts
    Info,
    Alert,
    Stop,
    CheckCircle
}
