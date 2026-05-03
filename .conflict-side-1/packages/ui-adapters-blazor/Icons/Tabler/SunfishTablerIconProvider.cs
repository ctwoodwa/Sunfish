using Sunfish.Foundation.Enums;
using Sunfish.UICore.Contracts;

namespace Sunfish.Icons.Tabler;

/// <summary>Icon provider rendering Tabler Icons (MIT — https://tabler.io/icons) from an SVG sprite.</summary>
public sealed class SunfishTablerIconProvider : ISunfishIconProvider
{
    private const string SpriteUrl = "_content/Sunfish.Icons.Tabler/icons/tabler-sprite.svg";

    public IconRenderMode RenderMode => IconRenderMode.SvgSprite;
    public string LibraryName => "Tabler";

    public string GetIcon(string name, IconSize size = IconSize.Medium)
    {
        var px = size switch
        {
            IconSize.Small      => "16",
            IconSize.Medium     => "20",
            IconSize.Large      => "24",
            IconSize.ExtraLarge => "32",
            _                   => "20"
        };
        var iconId = name.StartsWith("tabler-", StringComparison.Ordinal) ? name : $"tabler-{name}";
        return $"""<svg class="sf-icon sf-icon--{size.ToString().ToLower()}" width="{px}" height="{px}" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false"><use href="{SpriteUrl}#{iconId}"></use></svg>""";
    }

    public string GetIconSpriteUrl() => SpriteUrl;
}
