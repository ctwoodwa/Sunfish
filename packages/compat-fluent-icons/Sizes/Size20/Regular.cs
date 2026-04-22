namespace Sunfish.Compat.FluentIcons.Size20;

/// <summary>
/// Starter-set of typed icon identifiers for Fluent UI System Icons at 20px in the
/// <c>Regular</c> (outline) variant. Mirrors Fluent's
/// <c>Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size20.*</c> shape so
/// consumers can flip their <c>using</c> directives and keep <c>new Size20.Regular.Home()</c>
/// construction expressions intact.
/// </summary>
/// <remarks>
/// Each nested class exposes a <c>Name</c> property carrying the kebab-case Fluent icon
/// name. The flagship <c>FluentIcon</c> wrapper reflects over <c>Name</c> and passes it
/// through to the active <c>ISunfishIconProvider</c> via <c>SunfishIcon.Name</c>.
///
/// This is intentionally a 50-icon starter set covering the icons most migrators reach
/// for first. Fluent's full lattice (Size × Variant × Name) exceeds 60,000 types; see
/// <c>docs/compat-fluent-icons-mapping.md</c> for the rationale. Consumers may pass a
/// plain string to <see cref="FluentIcon.Value"/> directly for icons outside the starter
/// set.
///
/// Additions to this class require CODEOWNER sign-off per
/// <c>packages/compat-fluent-icons/POLICY.md</c>.
/// </remarks>
public static class Regular
{
    // Core UI / navigation
    public class Home { public string Name => "home"; }
    public class Search { public string Name => "search"; }
    public class Settings { public string Name => "settings"; }
    public class Person { public string Name => "person"; }
    public class Navigation { public string Name => "navigation"; }
    public class Grid { public string Name => "grid"; }
    public class List { public string Name => "list"; }
    public class Filter { public string Name => "filter"; }
    public class Sort { public string Name => "sort"; }

    // Communication
    public class Mail { public string Name => "mail"; }
    public class Chat { public string Name => "chat"; }
    public class Phone { public string Name => "phone"; }

    // Time
    public class Calendar { public string Name => "calendar"; }
    public class Clock { public string Name => "clock"; }

    // Files / media
    public class Folder { public string Name => "folder"; }
    public class Document { public string Name => "document"; }
    public class Image { public string Name => "image"; }
    public class Video { public string Name => "video"; }
    public class Music { public string Name => "music"; }

    // File actions
    public class Save { public string Name => "save"; }
    public class Edit { public string Name => "edit"; }
    public class Delete { public string Name => "delete"; }
    public class Add { public string Name => "add"; }
    public class Dismiss { public string Name => "dismiss"; }
    public class Copy { public string Name => "copy"; }
    public class Print { public string Name => "print"; }
    public class Download { public string Name => "download"; }
    public class Upload { public string Name => "upload"; }
    public class Share { public string Name => "share"; }

    // Arrows
    public class ArrowUp { public string Name => "arrow-up"; }
    public class ArrowDown { public string Name => "arrow-down"; }
    public class ArrowLeft { public string Name => "arrow-left"; }
    public class ArrowRight { public string Name => "arrow-right"; }
    public class ChevronUp { public string Name => "chevron-up"; }
    public class ChevronDown { public string Name => "chevron-down"; }

    // Favorites / social
    public class Heart { public string Name => "heart"; }
    public class Bookmark { public string Name => "bookmark"; }

    // Status / feedback
    public class Info { public string Name => "info"; }
    public class Warning { public string Name => "warning"; }
    public class ErrorCircle { public string Name => "error-circle"; }
    public class CheckmarkCircle { public string Name => "checkmark-circle"; }

    // Media control
    public class Play { public string Name => "play"; }
    public class Pause { public string Name => "pause"; }
    public class Stop { public string Name => "stop"; }
    public class VolumeUp { public string Name => "volume-up"; }

    // Visibility / security
    public class Eye { public string Name => "eye"; }
    public class EyeOff { public string Name => "eye-off"; }
    public class Lock { public string Name => "lock"; }
    public class Unlock { public string Name => "unlock"; }
    public class Key { public string Name => "key"; }
}
