using Sunfish.UICore.Contracts;
using Sunfish.Foundation.Enums;

namespace Sunfish.Providers.FluentUI;

public class FluentUIIconProvider : ISunfishIconProvider
{
    private const string SpriteUrl = "_content/Sunfish.Providers.FluentUI/icons/fluent-icons.svg";

    /// <inheritdoc />
    public IconRenderMode RenderMode => IconRenderMode.SvgSprite;

    /// <inheritdoc />
    public string LibraryName => "FluentUI";

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
        return $"""<svg class="sf-icon sf-icon--{size.ToString().ToLower()}" width="{px}" height="{px}" aria-hidden="true"><use href="{SpriteUrl}#{name}"></use></svg>""";
    }

    public string GetIconSpriteUrl() => SpriteUrl;
}
