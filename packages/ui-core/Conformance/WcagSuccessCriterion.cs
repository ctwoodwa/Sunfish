namespace Sunfish.UICore.Conformance;

/// <summary>
/// One WCAG success criterion citation per ADR 0077 §7. Use the W3C
/// canonical <c>"N.M.K"</c> form for <see cref="Id"/> (e.g.,
/// <c>"1.4.3"</c>) and the canonical title for <see cref="Title"/>
/// (e.g., <c>"Contrast (Minimum)"</c>).
/// </summary>
/// <param name="Id">Canonical W3C SC identifier (e.g., <c>"1.4.3"</c>).</param>
/// <param name="Title">Canonical W3C SC title (e.g., <c>"Contrast (Minimum)"</c>).</param>
public sealed record WcagSuccessCriterion(string Id, string Title);
