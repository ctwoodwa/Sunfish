using Sunfish.UICore.Contracts;
using Sunfish.Foundation.Enums;

namespace Sunfish.Providers.Bootstrap;

public class BootstrapIconProvider : ISunfishIconProvider
{
    // Placeholder — the Sunfish.Icons package will be introduced in a later phase.
    private const string SpriteUrl = "_content/Sunfish.Icons/icons/sprite.svg";

    /// <inheritdoc />
    public IconRenderMode RenderMode => IconRenderMode.SvgSprite;

    /// <inheritdoc />
    public string LibraryName => "Bootstrap";

    public string GetIcon(string name, IconSize size = IconSize.Medium)
    {
        var px = size switch
        {
            IconSize.Small => "16",
            IconSize.Medium => "20",
            IconSize.Large => "24",
            IconSize.ExtraLarge => "32",
            _ => "20"
        };
        // Sunfish Icons sprite uses "sf-" prefix for all icon IDs.
        var iconId = name.StartsWith("sf-") ? name : $"sf-{name}";
        return $"""<svg class="sf-icon sf-icon--{size.ToString().ToLower()}" width="{px}" height="{px}" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><use href="{SpriteUrl}#{iconId}"></use></svg>""";
    }

    public string GetIconSpriteUrl() => SpriteUrl;
}
