namespace Sunfish.UICore.Conformance;

/// <summary>
/// One EN 301 549 v3.2.1 chapter citation per ADR 0077 §7. The EN
/// chapters typically map 1:1 to WCAG SC ids (e.g., EN 9.1.4.3 ↔
/// WCAG 1.4.3) but the EN baseline carries additional non-WCAG chapters
/// for hardware + closed-captioning that the conformance declaration
/// cites separately.
/// </summary>
/// <param name="Id">Canonical EN chapter identifier (e.g., <c>"9.1.4.3"</c>).</param>
/// <param name="Title">Canonical EN chapter title (e.g., <c>"Contrast (Minimum)"</c>).</param>
public sealed record En301549Chapter(string Id, string Title);
