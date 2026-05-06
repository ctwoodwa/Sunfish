using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.ShipsOffice;

/// <summary>
/// Adapter contract for editing a Ship's Office document per ADR 0083 §3.
/// Framework-specific implementations live in <c>ui-adapters-blazor</c>,
/// <c>ui-adapters-react</c>, and <c>ui-adapters-maui</c>; consumers
/// depend on this interface, not the concrete editor.
/// </summary>
/// <remarks>
/// Phase 2 ships a <c>NoopContentEditorSurface</c> read-only stub per
/// Open Q2 deferral; the full markdown editor lands in a Phase 2
/// follow-up once the per-adapter editor surface is wired.
/// </remarks>
public interface IContentEditorSurface
{
    /// <summary>
    /// Open the editor for <paramref name="id"/>. Pre-condition: caller
    /// has verified <c>ShipAction.EditShipsOfficeDocument</c>. Returns
    /// the result of the editor session (saved or cancelled).
    /// </summary>
    Task<ContentEditorResult> EditAsync(
        TenantId tenant,
        ShipsOfficeDocumentId id,
        CancellationToken ct = default);
}
