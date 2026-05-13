using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.ShipsOffice;
using Sunfish.UICore.Primitives;

namespace Sunfish.Blocks.ShipsOffice;

/// <summary>
/// Computes an accessible diff between two versions of a Ship's Office document.
/// Per ADR 0083 §3 tier-discipline: declared in <c>blocks-ships-office</c> (NOT
/// <c>foundation-ships-office</c>) because it returns <see cref="IDiffPreview"/>
/// from <c>Sunfish.UICore</c>. This keeps the foundation tier free of ui-core
/// dependencies (council B-1 finding, ADR 0083 pre-merge).
/// </summary>
public interface IDocumentDiffService
{
    /// <summary>
    /// Computes a field-level diff between two Ship's Office documents.
    /// </summary>
    /// <param name="tenant">Tenant scope — documents MUST belong to this tenant.</param>
    /// <param name="baseId">Identifier of the "before" document version.</param>
    /// <param name="compareId">Identifier of the "after" document version.</param>
    /// <param name="view">Rendering hint passed to the Blazor diff panel.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IDiffPreview> ComputeDiffAsync(
        TenantId tenant,
        ShipsOfficeDocumentId baseId,
        ShipsOfficeDocumentId compareId,
        DiffPreviewView view = DiffPreviewView.Compact,
        CancellationToken ct = default);
}

/// <summary>
/// Phase 2 stub implementation. Returns a no-op diff ("Diff not yet available")
/// until H2 (ADR 0077 Phase 3 DiffPreviewPanel) clears and a real implementation
/// is wired in Phase 3. Registered via <see cref="ShipsOfficeServiceCollectionExtensions"/>.
/// </summary>
internal sealed class DocumentDiffService : IDocumentDiffService
{
    public Task<IDiffPreview> ComputeDiffAsync(
        TenantId tenant,
        ShipsOfficeDocumentId baseId,
        ShipsOfficeDocumentId compareId,
        DiffPreviewView view = DiffPreviewView.Compact,
        CancellationToken ct = default)
        => Task.FromResult<IDiffPreview>(StubPreview.Instance);

    private sealed class StubPreview : IDiffPreview
    {
        public static readonly StubPreview Instance = new();
        public IReadOnlyList<DiffEntry> Entries { get; } = [];
        public string Summary => "Diff not yet available";
    }
}
