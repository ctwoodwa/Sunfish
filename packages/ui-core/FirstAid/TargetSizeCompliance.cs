using System.Text.Json.Serialization;

namespace Sunfish.UICore.FirstAid;

/// <summary>
/// WCAG SC 2.5.8 (Target Size — Minimum) + SC 2.5.5 (Target Size —
/// Enhanced) compliance discriminator per ADR 0077 §4. Surfaces declare
/// this as part of their <see cref="IFirstAidContract"/> so the W#46
/// CI a11y gate can audit per-platform conformance.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TargetSizeCompliance
{
    /// <summary>All interactive controls meet ≥24×24 CSS px (web) / ≥44pt (iOS) / ≥48dp (Android).</summary>
    Conforming,

    /// <summary>One or more controls fall below the floor under a documented WCAG SC 2.5.8 exception (e.g., inline link in body text).</summary>
    ExemptByException,

    /// <summary>One or more controls fall below the floor without an exception — surface fails the W#46 a11y CI gate.</summary>
    NonConforming,
}
