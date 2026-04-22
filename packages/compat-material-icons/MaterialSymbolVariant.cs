namespace Sunfish.Compat.MaterialIcons;

/// <summary>
/// Material Symbols stylistic variant. Google ships three class-level variants of the
/// Material Symbols variable font, each mapped to a distinct CSS class:
/// <list type="bullet">
///   <item><description><see cref="Outlined"/> → <c>material-symbols-outlined</c> (default)</description></item>
///   <item><description><see cref="Rounded"/> → <c>material-symbols-rounded</c></description></item>
///   <item><description><see cref="Sharp"/> → <c>material-symbols-sharp</c></description></item>
/// </list>
/// Per-axis variation (FILL / wght / GRAD / opsz) is out of scope for Phase 2 and must
/// be applied via <c>style="font-variation-settings:..."</c> on the host element.
/// </summary>
public enum MaterialSymbolVariant
{
    /// <summary>Outlined variant — emits <c>material-symbols-outlined</c>.</summary>
    Outlined,

    /// <summary>Rounded variant — emits <c>material-symbols-rounded</c>.</summary>
    Rounded,

    /// <summary>Sharp variant — emits <c>material-symbols-sharp</c>.</summary>
    Sharp
}
