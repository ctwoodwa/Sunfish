using System;

namespace Sunfish.Compat.SimpleIcons;

/// <summary>
/// Translation helpers for <see cref="SimpleIconSlug"/> → the canonical
/// lowercase-alphanumeric Simple Icons slug used as the <c>si-*</c> CSS class on the
/// rendered element.
/// </summary>
public static class SimpleIconSlugExtensions
{
    /// <summary>
    /// Returns the canonical lowercase-alphanumeric Simple Icons slug for this enum value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Simple Icons slugs are lowercase ASCII with no hyphens, no spaces, no
    /// underscores — brand names are normalized into flat tokens (<c>github</c>,
    /// <c>stackoverflow</c>, <c>html5</c>, <c>dotnet</c>). This differs from Bootstrap
    /// Icons' kebab-case convention. The map is hand-authored rather than derived
    /// from <c>ToString()</c> because brand-name normalizations (<c>node.js</c> →
    /// <c>nodejs</c>, <c>.NET</c> → <c>dotnet</c>, <c>Vue.js</c> → <c>vuejs</c>) would
    /// not survive a naive lowercase transform.
    /// </para>
    /// <para>
    /// <b>Twitter / X alias:</b> Simple Icons now publishes the former Twitter brand
    /// under the slug <c>x</c>, but the legacy <c>twitter</c> slug is preserved
    /// upstream for backwards compatibility. This compat package maps
    /// <see cref="SimpleIconSlug.Twitter"/> → <c>"twitter"</c> to match the pre-rebrand
    /// source-shape most migrators ship today. Consumers who want the new-brand slug
    /// can use <c>&lt;SimpleIcon SlugString="x" /&gt;</c>.
    /// </para>
    /// </remarks>
    public static string ToSlug(this SimpleIconSlug slug) => slug switch
    {
        // Git forges
        SimpleIconSlug.Github => "github",
        SimpleIconSlug.Gitlab => "gitlab",
        SimpleIconSlug.Bitbucket => "bitbucket",

        // Social networks — note: Twitter → "twitter" preserves pre-rebrand slug;
        // upstream also publishes "x" for the post-rebrand mark.
        SimpleIconSlug.Twitter => "twitter",
        SimpleIconSlug.Facebook => "facebook",
        SimpleIconSlug.Instagram => "instagram",
        SimpleIconSlug.Linkedin => "linkedin",
        SimpleIconSlug.Youtube => "youtube",
        SimpleIconSlug.Tiktok => "tiktok",

        // Messaging / collaboration
        SimpleIconSlug.Discord => "discord",
        SimpleIconSlug.Slack => "slack",
        SimpleIconSlug.Telegram => "telegram",
        SimpleIconSlug.Whatsapp => "whatsapp",

        // Big tech
        SimpleIconSlug.Microsoft => "microsoft",
        SimpleIconSlug.Google => "google",
        SimpleIconSlug.Apple => "apple",
        SimpleIconSlug.Amazon => "amazon",
        SimpleIconSlug.Meta => "meta",

        // Media / streaming
        SimpleIconSlug.Netflix => "netflix",
        SimpleIconSlug.Spotify => "spotify",

        // Storage / content / community
        SimpleIconSlug.Dropbox => "dropbox",
        SimpleIconSlug.Pinterest => "pinterest",
        SimpleIconSlug.Reddit => "reddit",
        SimpleIconSlug.Stackoverflow => "stackoverflow",
        SimpleIconSlug.Medium => "medium",

        // Commerce / payments
        SimpleIconSlug.Wordpress => "wordpress",
        SimpleIconSlug.Shopify => "shopify",
        SimpleIconSlug.Stripe => "stripe",
        SimpleIconSlug.Paypal => "paypal",
        SimpleIconSlug.Visa => "visa",
        SimpleIconSlug.Mastercard => "mastercard",

        // Design tools
        SimpleIconSlug.Figma => "figma",
        SimpleIconSlug.Adobe => "adobe",
        SimpleIconSlug.Sketch => "sketch",

        // DevOps / infrastructure
        SimpleIconSlug.Docker => "docker",
        SimpleIconSlug.Kubernetes => "kubernetes",

        // Runtimes / languages — note: "nodejs" (no dot), "dotnet" (no dot / no hash)
        SimpleIconSlug.Nodejs => "nodejs",
        SimpleIconSlug.Python => "python",
        SimpleIconSlug.Rust => "rust",
        SimpleIconSlug.Go => "go",
        SimpleIconSlug.Dotnet => "dotnet",

        // Front-end frameworks — note: "vuejs" (no dot)
        SimpleIconSlug.React => "react",
        SimpleIconSlug.Vuejs => "vuejs",
        SimpleIconSlug.Angular => "angular",

        // Languages / platforms
        SimpleIconSlug.Typescript => "typescript",
        SimpleIconSlug.Javascript => "javascript",
        SimpleIconSlug.Html5 => "html5",
        SimpleIconSlug.Css3 => "css3",

        // CSS frameworks
        SimpleIconSlug.Tailwindcss => "tailwindcss",
        SimpleIconSlug.Bootstrap => "bootstrap",

        _ => throw new ArgumentOutOfRangeException(
            nameof(slug), slug,
            "SimpleIconSlug value has no slug mapping. Add an entry to SimpleIconSlugExtensions.ToSlug under policy-gated review."),
    };
}
