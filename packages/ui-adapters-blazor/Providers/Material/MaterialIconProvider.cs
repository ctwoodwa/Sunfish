using Sunfish.UICore.Contracts;
using Sunfish.Foundation.Enums;

namespace Sunfish.Providers.Material;

public class MaterialIconProvider : ISunfishIconProvider
{
    // Material reuses the FluentUI icon sprite via a cross-package _content/ path.
    // A dedicated Material icon set can be introduced in a future phase.
    private const string SpriteUrl = "_content/Sunfish.Providers.FluentUI/icons/fluent-icons.svg";

    /// <inheritdoc />
    public IconRenderMode RenderMode => IconRenderMode.SvgSprite;

    /// <inheritdoc />
    public string LibraryName => "Material";

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
