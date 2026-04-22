namespace Sunfish.Compat.SimpleIcons;

/// <summary>
/// Starter-set of typed Simple Icons brand slugs. Each member maps to its canonical
/// lowercase-alphanumeric Simple Icons slug (e.g. <see cref="Github"/> → <c>"github"</c>,
/// <see cref="Html5"/> → <c>"html5"</c>, <see cref="Dotnet"/> → <c>"dotnet"</c>), which
/// is emitted as the <c>si-*</c> CSS class on the rendered <c>&lt;i&gt;</c> element —
/// matching upstream Simple Icons markup (<c>&lt;i class="si si-github"&gt;&lt;/i&gt;</c>).
/// </summary>
/// <remarks>
/// <para>
/// This is intentionally a 50-slug starter set covering the most-recognized brands
/// migrators reach for first (Git forges, social networks, payment providers, dev
/// tools, front-end frameworks). Simple Icons' upstream catalog exceeds 2,800 brand
/// icons — only a tiny fraction is covered here. Consumers may render icons outside
/// the starter set via the <c>SlugString</c> parameter on <see cref="SimpleIcon"/> —
/// e.g. <c>&lt;SimpleIcon SlugString="opentelemetry" /&gt;</c>. Because Simple Icons
/// is a brand-logo catalog, the <c>SlugString</c> escape hatch is expected to see
/// heavier traffic than on the other compat-icon packages.
/// </para>
/// <para>
/// Naming the enum <c>SimpleIconSlug</c> rather than a per-brand lattice is a
/// deliberate simplification — brands aren't categorizable like Bootstrap or Font
/// Awesome icons are, so a flat enum mirrors Simple Icons' own flat slug directory.
/// Additions to this enum require CODEOWNER sign-off per
/// <c>packages/compat-simple-icons/POLICY.md</c>.
/// </para>
/// <para>
/// <b>Trademark note:</b> Simple Icons' CC0-1.0 license covers the SVG artwork and
/// the slug catalog itself, but the depicted brands' trademarks remain with their
/// owners. Consumers using these icons in a product context should review each
/// brand's trademark policy (typically "acceptable for indicating integration /
/// compatibility").
/// </para>
/// </remarks>
public enum SimpleIconSlug
{
    // Git forges
    Github,
    Gitlab,
    Bitbucket,

    // Social networks
    Twitter,
    Facebook,
    Instagram,
    Linkedin,
    Youtube,
    Tiktok,

    // Messaging / collaboration
    Discord,
    Slack,
    Telegram,
    Whatsapp,

    // Big tech
    Microsoft,
    Google,
    Apple,
    Amazon,
    Meta,

    // Media / streaming
    Netflix,
    Spotify,

    // Storage / content / community
    Dropbox,
    Pinterest,
    Reddit,
    Stackoverflow,
    Medium,

    // Commerce / payments
    Wordpress,
    Shopify,
    Stripe,
    Paypal,
    Visa,
    Mastercard,

    // Design tools
    Figma,
    Adobe,
    Sketch,

    // DevOps / infrastructure
    Docker,
    Kubernetes,

    // Runtimes / languages
    Nodejs,
    Python,
    Rust,
    Go,
    Dotnet,

    // Front-end frameworks
    React,
    Vuejs,
    Angular,

    // Languages / platforms
    Typescript,
    Javascript,
    Html5,
    Css3,

    // CSS frameworks
    Tailwindcss,
    Bootstrap,
}
