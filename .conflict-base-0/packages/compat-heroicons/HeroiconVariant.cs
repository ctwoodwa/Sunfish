namespace Sunfish.Compat.Heroicons;

/// <summary>
/// Heroicons stylistic variant. Heroicons ships three distinct forms of every icon,
/// each with different geometry/sizing:
/// <list type="bullet">
///   <item><description><see cref="Outline"/> — 24×24 stroke-based (default). Emits <c>heroicon-outline</c>.</description></item>
///   <item><description><see cref="Solid"/> — 24×24 filled. Emits <c>heroicon-solid</c>.</description></item>
///   <item><description><see cref="Mini"/> — 20×20 filled, tuned for small UIs. Emits <c>heroicon-mini</c>.</description></item>
/// </list>
/// The variant is selected via the <c>Variant</c> parameter on <see cref="Heroicon"/>.
/// Defaults to <see cref="Outline"/>, matching Heroicons' own documented default.
/// </summary>
public enum HeroiconVariant
{
    /// <summary>24×24 stroke-based variant (default) — emits <c>heroicon-outline</c>.</summary>
    Outline,

    /// <summary>24×24 filled variant — emits <c>heroicon-solid</c>.</summary>
    Solid,

    /// <summary>20×20 filled variant tuned for small UIs — emits <c>heroicon-mini</c>.</summary>
    Mini
}
