using Sunfish.Foundation.Enums;
using Sunfish.UICore.Contracts;

namespace Sunfish.Icons.Legacy;

/// <summary>Legacy sprite-based icon provider. Prefer SunfishTablerIconProvider.</summary>
[Obsolete("Use Sunfish.Icons.Tabler. Legacy icon set retained for backward compatibility only.")]
public sealed class SunfishLegacyIconProvider : ISunfishIconProvider
{
    private const string SpriteUrl = "_content/Sunfish.Icons.Legacy/icons/sprite.svg";

    public IconRenderMode RenderMode => IconRenderMode.SvgSprite;
    public string LibraryName => "Legacy";

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
        var iconId = name.StartsWith("sf-", StringComparison.Ordinal) ? name : $"sf-{name}";
        return $"""<svg class="sf-icon sf-icon--{size.ToString().ToLower()}" width="{px}" height="{px}" aria-hidden="true" focusable="false"><use href="{SpriteUrl}#{iconId}"></use></svg>""";
    }

    public string GetIconSpriteUrl() => SpriteUrl;
}
