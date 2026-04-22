namespace Sunfish.Compat.FontAwesome;

/// <summary>
/// Starter-set of typed icon identifiers for Font Awesome's <c>Brands</c> style family
/// (<c>fab</c>). Each member is a kebab-case string matching the canonical Font Awesome
/// brand icon name.
/// </summary>
/// <remarks>
/// Brand marks remain the trademark of their respective owners — this class only stores
/// the kebab-case icon-name string so the active Sunfish icon provider can resolve it.
/// No brand assets are bundled. Additions require CODEOWNER sign-off per
/// <c>packages/compat-font-awesome/POLICY.md</c>.
/// </remarks>
public static class FabIcons
{
    // Developer / social
    public static readonly string Github = "github";
    public static readonly string Gitlab = "gitlab";
    public static readonly string Bitbucket = "bitbucket";
    public static readonly string Git = "git";
    public static readonly string Stackoverflow = "stack-overflow";
    public static readonly string Npm = "npm";
    public static readonly string Nodejs = "node-js";
    public static readonly string Docker = "docker";

    // Major social
    public static readonly string Twitter = "twitter";
    public static readonly string XTwitter = "x-twitter";
    public static readonly string Facebook = "facebook";
    public static readonly string FacebookF = "facebook-f";
    public static readonly string Instagram = "instagram";
    public static readonly string Linkedin = "linkedin";
    public static readonly string LinkedinIn = "linkedin-in";
    public static readonly string Youtube = "youtube";
    public static readonly string Tiktok = "tiktok";
    public static readonly string Pinterest = "pinterest";
    public static readonly string Reddit = "reddit";
    public static readonly string Tumblr = "tumblr";

    // Messaging / collaboration
    public static readonly string Discord = "discord";
    public static readonly string Slack = "slack";
    public static readonly string Telegram = "telegram";
    public static readonly string Whatsapp = "whatsapp";
    public static readonly string Skype = "skype";
    public static readonly string Signal = "signal-messenger";
    public static readonly string Teams = "microsoft";

    // Big tech
    public static readonly string Microsoft = "microsoft";
    public static readonly string Google = "google";
    public static readonly string Apple = "apple";
    public static readonly string Amazon = "amazon";
    public static readonly string Aws = "aws";
    public static readonly string GoogleDrive = "google-drive";
    public static readonly string Windows = "windows";

    // Storage / files
    public static readonly string Dropbox = "dropbox";

    // Streaming / entertainment
    public static readonly string Twitch = "twitch";
    public static readonly string Spotify = "spotify";
    public static readonly string Youtube_Alt = "youtube-square";
    public static readonly string Steam = "steam";
    public static readonly string Playstation = "playstation";
    public static readonly string Xbox = "xbox";

    // Payment / commerce
    public static readonly string Paypal = "paypal";
    public static readonly string Stripe = "stripe";
    public static readonly string CcVisa = "cc-visa";
    public static readonly string CcMastercard = "cc-mastercard";
    public static readonly string CcAmex = "cc-amex";
    public static readonly string CcDiscover = "cc-discover";
    public static readonly string CcPaypal = "cc-paypal";

    // OSS / Linux
    public static readonly string Linux = "linux";
    public static readonly string Ubuntu = "ubuntu";
    public static readonly string Redhat = "redhat";
    public static readonly string Fedora = "fedora";
}
