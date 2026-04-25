using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Sunfish.UIAdapters.Blazor.A11y;

/// <summary>
/// Strongly-typed projection of the <c>parameters.a11y.sunfish</c> contract block declared
/// per-component in <c>*.stories.ts</c>. Authored once on the Storybook side; consumed by
/// the Blazor bridge via <c>dist/a11y-contracts.json</c> (Plan 4 Task 1.6).
/// </summary>
/// <remarks>
/// Mirror of ADR 0034's contract shape. Property names match the JS-side keys exactly so
/// JsonSerializer can deserialize without converters; the JS export script writes plain
/// camelCase JSON that maps cleanly to these C# properties via <see cref="JsonPropertyNameAttribute"/>.
/// </remarks>
public sealed class SunfishA11yContract
{
    /// <summary>Accessible name the component exposes (string match) — drives axe label asserts.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>ARIA role the component exposes (e.g. "button", "dialog", "alert").</summary>
    [JsonPropertyName("role")]
    public string? Role { get; init; }

    /// <summary>WAI-ARIA Authoring Practices Guide URL the component conforms to.</summary>
    [JsonPropertyName("ariaPattern")]
    public string? AriaPattern { get; init; }

    /// <summary>Keyboard interactions: each entry's keys (e.g. ["Enter"]) trigger the named action.</summary>
    [JsonPropertyName("keyboardMap")]
    public IReadOnlyList<KeyboardBinding> KeyboardMap { get; init; } = new List<KeyboardBinding>();

    /// <summary>Focus-management contract: where focus starts, whether trap is in effect, where to restore on close.</summary>
    [JsonPropertyName("focus")]
    public FocusContract Focus { get; init; } = new();

    /// <summary>Live-region politeness if the component announces state changes.</summary>
    [JsonPropertyName("liveRegion")]
    public LiveRegionPoliteness? LiveRegion { get; init; }

    /// <summary>Whether the component honours <c>prefers-reduced-motion: reduce</c> ("respects" or "n/a").</summary>
    [JsonPropertyName("reducedMotion")]
    public string? ReducedMotion { get; init; }

    /// <summary>RTL icon-mirror policy ("mirrors" or "non-directional"). Per-icon overrides via <see cref="DirectionalIcons"/>.</summary>
    [JsonPropertyName("rtlIconMirror")]
    public string? RtlIconMirror { get; init; }

    /// <summary>Selector list of directional icons that MUST mirror under RTL (e.g. arrow-back).</summary>
    [JsonPropertyName("directionalIcons")]
    public IReadOnlyList<string> DirectionalIcons { get; init; } = new List<string>();

    /// <summary>Composed-of list (e.g. dialog contains buttons). Informational; not enforced.</summary>
    [JsonPropertyName("composedOf")]
    public IReadOnlyList<string> ComposedOf { get; init; } = new List<string>();

    /// <summary>WCAG 2.2 success criteria the component intends to satisfy (e.g. ["1.3.1", "2.1.1"]).</summary>
    [JsonPropertyName("wcag22Conformant")]
    public IReadOnlyList<string> Wcag22Conformant { get; init; } = new List<string>();
}

/// <summary>One keyboard binding: <c>{ keys: ["Enter"], action: "activate" }</c>.</summary>
public sealed class KeyboardBinding
{
    [JsonPropertyName("keys")]
    public IReadOnlyList<string> Keys { get; init; } = new List<string>();

    [JsonPropertyName("action")]
    public string Action { get; init; } = string.Empty;
}

/// <summary>Focus-management contract for one component.</summary>
public sealed class FocusContract
{
    /// <summary>Where focus starts: "self", "first-focusable-child", "none", or a specific selector.</summary>
    [JsonPropertyName("initial")]
    public string Initial { get; init; } = "self";

    /// <summary>Whether focus is trapped within the component (e.g. modal dialogs).</summary>
    [JsonPropertyName("trap")]
    public bool Trap { get; init; }

    /// <summary>Where to restore focus when the component closes/unmounts. <c>null</c> if no trap or restore is needed.</summary>
    [JsonPropertyName("restore")]
    public string? Restore { get; init; }
}

/// <summary>ARIA live-region politeness levels.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LiveRegionPoliteness
{
    Off,
    Polite,
    Assertive,
}
