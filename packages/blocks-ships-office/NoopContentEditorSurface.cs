using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.ShipsOffice;

namespace Sunfish.Blocks.ShipsOffice;

/// <summary>
/// Read-only stub <see cref="IContentEditorSurface"/> per
/// ADR 0083 §3 + W#55 Phase 2a. Returns
/// <c>(WasSaved=false, NewVersionLabel=null)</c> for every call —
/// the full markdown editor adapter wiring is deferred to a Phase 5
/// follow-up per Open Q2.
/// </summary>
internal sealed class NoopContentEditorSurface : IContentEditorSurface
{
    /// <inheritdoc />
    public Task<ContentEditorResult> EditAsync(
        TenantId tenant,
        ShipsOfficeDocumentId id,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new ContentEditorResult(
            WasSaved: false,
            NewVersionLabel: null));
    }
}
