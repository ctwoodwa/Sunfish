namespace Sunfish.Foundation.ShipsOffice;

/// <summary>
/// Outcome of an <see cref="IContentEditorSurface.EditAsync"/> session
/// per ADR 0083 §3. Phase 1 ships the contract; Phase 2 lands a
/// full markdown editor; per-adapter renderers (Blazor/React/MAUI) live
/// in <c>ui-adapters-*</c>.
/// </summary>
/// <param name="WasSaved">True when the editor session ended with a saved change; false on cancel / no-op.</param>
/// <param name="NewVersionLabel">
/// Non-null when <paramref name="WasSaved"/> is true and the document
/// kind supports versioning; null otherwise. Per W#55 P1 pre-merge
/// council 2026-05-06 (Minor SI-3): callers SHOULD treat
/// <c>(WasSaved=true, NewVersionLabel=null)</c> as "kind does not
/// version" rather than "implementation forgot to populate" — Phase 2
/// follow-up: introduce factory methods (<c>Saved(string?)</c> /
/// <c>Cancelled()</c>) so illegal states are unrepresentable.
/// </param>
public sealed record ContentEditorResult(
    bool WasSaved,
    string? NewVersionLabel);
