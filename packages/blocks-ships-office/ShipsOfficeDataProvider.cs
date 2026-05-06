using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.ShipsOffice;

namespace Sunfish.Blocks.ShipsOffice;

/// <summary>
/// Reference <see cref="IShipsOfficeDataProvider"/> stub per
/// W#55 Phase 2a. Returns empty snapshots / search results / change
/// streams — the cross-package integration to
/// <c>BundleCatalog</c> / <c>ILeaseDocumentVersionLog</c> /
/// <c>IW9DocumentService</c> / <c>IMissionEnvelopeObserver</c> is
/// deferred to Phase 2b (per the cob-question filed alongside this
/// PR).
/// </summary>
/// <remarks>
/// <para>
/// <b>H4 invariant (load-bearing per ADR 0083 §Trust):</b> this
/// implementation MUST NOT depend on
/// <c>Sunfish.Foundation.Recovery.IFieldDecryptor</c>. Phase 2b's full
/// implementation will integrate with <c>IW9DocumentService.GetAsync</c>
/// (NEVER <c>GetWithDecryptedTinAsync</c>); the
/// <see cref="ShipsOfficeDocumentView"/> record has no TIN field and the
/// browse view EXCLUDES it entirely.
/// </para>
/// <para>
/// <b>Phase 2b deferral:</b>
/// <list type="bullet">
/// <item><description>BundleManifest projection from <c>BundleCatalog.GetAllAsync</c></description></item>
/// <item><description>LeaseDocument projection from <c>ILeaseDocumentVersionLog.ListAsync</c></description></item>
/// <item><description>VendorW9 projection from <c>IW9DocumentService</c> (TIN excluded)</description></item>
/// <item><description><c>IMissionEnvelopeObserver</c> wiring for change-driven snapshot invalidation</description></item>
/// <item><description><c>SearchAsync</c> in-memory linear scan over the projection with
/// <c>KindFilter</c> / <c>StatusFilter</c> / <c>TextQuery</c> handling</description></item>
/// <item><description><c>ShipsOfficeCommandService</c> + <c>IDocumentDiffService</c> + the
/// <c>SUNFISH_SHIPSOFFICE_PERM001</c> Roslyn analyzer</description></item>
/// </list>
/// </para>
/// </remarks>
internal sealed class ShipsOfficeDataProvider : IShipsOfficeDataProvider
{
    private readonly IOptions<ShipsOfficeOptions> _options;
    private readonly TimeProvider _time;

    public ShipsOfficeDataProvider(
        IOptions<ShipsOfficeOptions> options,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public Task<ShipsOfficeSnapshot> GetSnapshotAsync(
        TenantId tenant,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(BuildEmptySnapshot());
    }

    /// <inheritdoc />
#pragma warning disable CS1998 // async lacks await — IAsyncEnumerable shape requires it
    public async IAsyncEnumerable<ShipsOfficeDocumentView> SearchAsync(
        TenantId tenant,
        ShipsOfficeSearchQuery query,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ct.ThrowIfCancellationRequested();
        // Phase 2a empty stub: nothing to yield. Phase 2b adds the
        // in-memory linear scan over the snapshot projection.
        yield break;
    }
#pragma warning restore CS1998

    /// <inheritdoc />
    public async IAsyncEnumerable<ShipsOfficeDocumentView> SubscribeChangesAsync(
        TenantId tenant,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Phase 2a stub: no document-change events ever fire (the
        // empty-snapshot projection has nothing to mutate). The
        // method still honors the FallbackPollingInterval contract by
        // suspending until cancellation rather than completing the
        // stream — Phase 2b adds push-driven invalidation via
        // IMissionEnvelopeObserver and starts yielding views as
        // documents change.
        var interval = _options.Value.FallbackPollingInterval;
        if (interval <= TimeSpan.Zero)
        {
            yield break;
        }
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, _time, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
            // Intentionally yield nothing — Phase 2a stub.
        }
    }

    private ShipsOfficeSnapshot BuildEmptySnapshot() =>
        new ShipsOfficeSnapshot(
            Documents: Array.Empty<ShipsOfficeDocumentView>(),
            TotalCount: 0,
            AsOf: _time.GetUtcNow());
}
