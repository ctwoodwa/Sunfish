using Sunfish.Foundation.Enums;

namespace Sunfish.Compat.MaterialIcons;

/// <summary>
/// Internal helper that maps Sunfish <see cref="IconSize"/> values to the Sunfish-prefixed
/// size-modifier class emitted by <see cref="MaterialIcon"/> / <see cref="MaterialSymbol"/>.
/// Only the <c>sf-material-size-*</c> modifier is Sunfish-owned; the native
/// <c>material-icons</c> / <c>material-symbols-*</c> base class is left untouched so the
/// consumer's Google Fonts pipeline continues to render glyphs.
/// </summary>
internal static class MaterialSizeClass
{
    /// <summary>
    /// Returns the Sunfish size-modifier class for the given <paramref name="size"/>,
    /// or <c>string.Empty</c> when no size was specified.
    /// </summary>
    public static string ToClass(IconSize? size) => size switch
    {
        null => string.Empty,
        IconSize.Small => "sf-material-size-sm",
        IconSize.Medium => "sf-material-size-md",
        IconSize.Large => "sf-material-size-lg",
        IconSize.ExtraLarge => "sf-material-size-xl",
        _ => string.Empty
    };

    /// <summary>
    /// Returns the CSS class for a <see cref="MaterialSymbolVariant"/>. Defaults to
    /// <c>material-symbols-outlined</c> for <see cref="MaterialSymbolVariant.Outlined"/>.
    /// </summary>
    public static string ToSymbolClass(MaterialSymbolVariant variant) => variant switch
    {
        MaterialSymbolVariant.Rounded => "material-symbols-rounded",
        MaterialSymbolVariant.Sharp => "material-symbols-sharp",
        _ => "material-symbols-outlined"
    };
}
