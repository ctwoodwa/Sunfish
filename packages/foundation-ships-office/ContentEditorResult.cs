namespace Sunfish.Foundation.ShipsOffice;

/// <summary>
/// Outcome of an <see cref="IContentEditorSurface.EditAsync"/> session
/// per ADR 0083 §3. Phase 1 ships the contract; Phase 2 lands a
/// full markdown editor; per-adapter renderers (Blazor/React/MAUI) live
/// in <c>ui-adapters-*</c>.
/// </summary>
/// <param name="WasSaved">True when the editor session ended with a saved change; false on cancel / no-op.</param>
/// <param name="NewVersionLabel">Non-null when <paramref name="WasSaved"/> is true and the document kind supports versioning; null otherwise.</param>
public sealed record ContentEditorResult(
    bool WasSaved,
    string? NewVersionLabel);
