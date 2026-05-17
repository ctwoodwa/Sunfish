using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Sunfish.Blocks.Leases.Models;
using Sunfish.Blocks.Leases.Services;
using Sunfish.Blocks.Maintenance.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Catalog.Bundles;
using Sunfish.Foundation.MissionSpace;
using Sunfish.Foundation.ShipsOffice;
using Sunfish.Foundation.ShipsOffice.Services;

namespace Sunfish.Blocks.ShipsOffice;

/// <summary>
/// Reference <see cref="IShipsOfficeDataProvider"/> per ADR 0083 §1+§2 + W#55 Phase 2b.
/// Projects a tenant's Ship's Office document browse from four sources:
/// <list type="bullet">
///   <item><description><see cref="IBundleCatalog"/> — registered bundle manifests</description></item>
///   <item><description><see cref="ILeaseDocumentVersionLog"/> — latest revision per lease</description></item>
///   <item><description><see cref="IW9DocumentService"/> (NEVER <c>GetWithDecryptedTinAsync</c>) — vendor W9s; TIN excluded</description></item>
///   <item><description>SignatureEnvelope — empty-list stub (H6 pending ADR 0004 Stage 06)</description></item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// <b>H4 invariant (load-bearing, ADR 0046-A2 §4 + ADR 0083 §Trust):</b> this
/// implementation MUST NOT depend on <c>Sunfish.Foundation.Recovery.IFieldDecryptor</c>.
/// W9 integration uses <see cref="IW9DocumentService.GetAsync"/> only — the TIN is
/// intentionally excluded from <see cref="ShipsOfficeDocumentView"/> per the §Trust
/// redaction policy. The H4 reflection test in
/// <c>ShipsOfficeProviderTests.Provider_DoesNotReference_FoundationRecovery</c>
/// pins this at the AssemblyName level + type-graph walk.
/// </para>
/// <para>
/// <b>H5 revisit-trigger:</b> <see cref="SubscribeChangesAsync"/> subscribes to
/// <see cref="IMissionEnvelopeProvider"/> for push-driven invalidation. A fallback
/// poll fires on <see cref="ShipsOfficeOptions.FallbackPollingInterval"/> (default 60s)
/// even when no envelope event arrives. Revisit when ADR 0065-A1
/// <c>IStandingOrderEventStream</c> matures further.
/// </para>
/// <para>
/// <b>H6 revisit-trigger:</b> <see cref="ShipsOfficeDocumentKind.SignatureEnvelope"/>
/// returns an empty list. Revisit when ADR 0004 Stage 06 ships
/// <c>ISignatureEnvelopeStore</c> with a queryable surface.
/// </para>
/// </remarks>
internal sealed class ShipsOfficeDataProvider : IShipsOfficeDataProvider
{
    private readonly IBundleCatalog _bundleCatalog;
    private readonly ILeaseService _leaseService;
    private readonly ILeaseDocumentVersionLog _leaseDocLog;
    private readonly IMaintenanceService _maintenanceService;
    private readonly IW9DocumentService _w9Service;
    private readonly IFormSchemaStore _formSchemas;
    private readonly IMissionEnvelopeProvider _missionEnvelopeProvider;
    private readonly IOptions<ShipsOfficeOptions> _options;
    private readonly TimeProvider _time;

    public ShipsOfficeDataProvider(
        IBundleCatalog bundleCatalog,
        ILeaseService leaseService,
        ILeaseDocumentVersionLog leaseDocLog,
        IMaintenanceService maintenanceService,
        IW9DocumentService w9Service,
        IFormSchemaStore formSchemas,
        IMissionEnvelopeProvider missionEnvelopeProvider,
        IOptions<ShipsOfficeOptions> options,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(bundleCatalog);
        ArgumentNullException.ThrowIfNull(leaseService);
        ArgumentNullException.ThrowIfNull(leaseDocLog);
        ArgumentNullException.ThrowIfNull(maintenanceService);
        ArgumentNullException.ThrowIfNull(w9Service);
        ArgumentNullException.ThrowIfNull(formSchemas);
        ArgumentNullException.ThrowIfNull(missionEnvelopeProvider);
        ArgumentNullException.ThrowIfNull(options);
        _bundleCatalog = bundleCatalog;
        _leaseService = leaseService;
        _leaseDocLog = leaseDocLog;
        _maintenanceService = maintenanceService;
        _w9Service = w9Service;
        _formSchemas = formSchemas;
        _missionEnvelopeProvider = missionEnvelopeProvider;
        _options = options;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<ShipsOfficeSnapshot> GetSnapshotAsync(
        TenantId tenant,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var views = await BuildViewsAsync(tenant, ct).ConfigureAwait(false);
        var list = views.ToArray();
        return new ShipsOfficeSnapshot(
            Documents: list,
            TotalCount: list.Length,
            AsOf: _time.GetUtcNow());
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ShipsOfficeDocumentView> SearchAsync(
        TenantId tenant,
        ShipsOfficeSearchQuery query,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ct.ThrowIfCancellationRequested();

        var all = await BuildViewsAsync(tenant, ct).ConfigureAwait(false);
        IEnumerable<ShipsOfficeDocumentView> filtered = all;

        if (query.KindFilter is { Count: > 0 })
        {
            var kindSet = query.KindFilter.ToHashSet();
            filtered = filtered.Where(v => kindSet.Contains(v.Kind));
        }

        if (query.StatusFilter.HasValue)
        {
            filtered = filtered.Where(v => v.Status == query.StatusFilter.Value);
        }

        if (!string.IsNullOrEmpty(query.TextQuery))
        {
            var text = query.TextQuery;
            filtered = filtered.Where(v => v.Title.Contains(text, StringComparison.OrdinalIgnoreCase));
        }

        // Opaque cursor: base64-encoded decimal offset. Treat invalid/stale as first page (per P1 council SI-4).
        int offset = 0;
        if (!string.IsNullOrEmpty(query.PageToken))
        {
            try
            {
                var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(query.PageToken));
                if (!int.TryParse(decoded, out var parsed) || parsed < 0)
                    offset = 0;
                else
                    offset = parsed;
            }
            catch (FormatException)
            {
                offset = 0;
            }
        }

        var pageSize = query.PageSize > 0 ? query.PageSize : 50;
        foreach (var view in filtered.Skip(offset).Take(pageSize))
        {
            ct.ThrowIfCancellationRequested();
            yield return view;
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ShipsOfficeDocumentView> SubscribeChangesAsync(
        TenantId tenant,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Bounded channel (capacity 1, drop-oldest) so envelope bursts don't queue up —
        // we only need to know "something changed."
        var channel = Channel.CreateBounded<bool>(
            new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });

        var observer = new EnvelopeObserver(channel.Writer);
        _missionEnvelopeProvider.Subscribe(observer);
        try
        {
            // Initial projection — yield all current documents.
            var views = await BuildViewsAsync(tenant, ct).ConfigureAwait(false);
            foreach (var v in views)
            {
                ct.ThrowIfCancellationRequested();
                yield return v;
            }

            var interval = _options.Value.FallbackPollingInterval;
            if (interval <= TimeSpan.Zero)
                interval = TimeSpan.FromSeconds(60);

            while (!ct.IsCancellationRequested)
            {
                // Wait for a push signal or fallback timeout — whichever fires first.
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(interval);
                try
                {
                    await channel.Reader.ReadAsync(cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    // Fallback poll timeout — not user cancellation.
                }

                if (ct.IsCancellationRequested) yield break;

                views = await BuildViewsAsync(tenant, ct).ConfigureAwait(false);
                foreach (var v in views)
                {
                    ct.ThrowIfCancellationRequested();
                    yield return v;
                }
            }
        }
        finally
        {
            _missionEnvelopeProvider.Unsubscribe(observer);
        }
    }

    private async Task<List<ShipsOfficeDocumentView>> BuildViewsAsync(
        TenantId tenant, CancellationToken ct)
    {
        var views = new List<ShipsOfficeDocumentView>();
        var now = _time.GetUtcNow();

        // 1. BundleManifest — static catalog registration; no per-tenant scoping.
        foreach (var manifest in _bundleCatalog.GetBundles())
        {
            views.Add(new ShipsOfficeDocumentView(
                Id: new ShipsOfficeDocumentId($"bundle:{manifest.Key}"),
                Kind: ShipsOfficeDocumentKind.BundleManifest,
                Title: manifest.Name,
                Status: MapBundleStatus(manifest.Status),
                UpdatedAt: now,
                LastModifiedBy: ActorId.Sunfish,
                VersionLabel: manifest.Version));
        }

        // 2. LeaseDocument — latest document version per lease.
        await foreach (var lease in _leaseService.ListAsync(ListLeasesQuery.Empty, ct).ConfigureAwait(false))
        {
            if (lease.DocumentVersions.Count == 0) continue;
            var latest = await _leaseDocLog.GetLatestAsync(lease.Id, ct).ConfigureAwait(false);
            if (latest is null) continue;

            views.Add(new ShipsOfficeDocumentView(
                Id: new ShipsOfficeDocumentId($"lease-doc:{latest.Id.Value}"),
                Kind: ShipsOfficeDocumentKind.LeaseDocument,
                Title: $"Lease {lease.Id.Value}",
                Status: MapLeasePhase(lease.Phase),
                UpdatedAt: latest.AuthoredAt,
                LastModifiedBy: latest.AuthoredBy,
                VersionLabel: $"v{latest.VersionNumber}"));
        }

        // 3. VendorW9 — vendor display name only; TIN excluded per ADR 0083 §Trust.
        await foreach (var vendor in _maintenanceService.ListVendorsAsync(ListVendorsQuery.Empty, ct).ConfigureAwait(false))
        {
            if (!vendor.W9.HasValue) continue;
            var doc = await _w9Service.GetAsync(vendor.W9.Value, tenant, ct).ConfigureAwait(false);
            if (doc is null) continue;

            views.Add(new ShipsOfficeDocumentView(
                Id: new ShipsOfficeDocumentId($"w9:{doc.Id.Value}"),
                Kind: ShipsOfficeDocumentKind.VendorW9,
                Title: vendor.DisplayName,
                Status: doc.VerifiedAt.HasValue ? DocumentStatus.Published : DocumentStatus.Draft,
                UpdatedAt: doc.VerifiedAt ?? doc.ReceivedAt,
                LastModifiedBy: doc.VerifiedBy ?? ActorId.System,
                VersionLabel: null));
        }

        // 4. SignatureEnvelope — H6 pending (ADR 0004 Stage 06 not yet shipped).
        // Revisit when ISignatureEnvelopeStore ships a queryable tenant surface.

        // 5. DynamicTemplate — ADR 0055 (W#55 Phase 5). Sourced from
        // local IFormSchemaStore stub per xo-ruling-T02-43Z; canonical
        // foundation-forms substrate will replace this when a forcing
        // function surfaces. §Trust redaction policy applies — the
        // FormSchema record carries name + status only; no payload data.
        foreach (var schema in await _formSchemas.ListByTenantAsync(tenant, ct).ConfigureAwait(false))
        {
            views.Add(new ShipsOfficeDocumentView(
                Id: new ShipsOfficeDocumentId($"form-schema:{schema.Id.Value}"),
                Kind: ShipsOfficeDocumentKind.DynamicTemplate,
                Title: schema.Name,
                Status: MapFormSchemaStatus(schema.Status),
                UpdatedAt: schema.UpdatedAt,
                LastModifiedBy: schema.LastModifiedBy,
                VersionLabel: null));
        }

        return views;
    }

    private static DocumentStatus MapFormSchemaStatus(FormSchemaStatus status) => status switch
    {
        FormSchemaStatus.Draft     => DocumentStatus.Draft,
        FormSchemaStatus.Published => DocumentStatus.Published,
        FormSchemaStatus.Archived  => DocumentStatus.Archived,
        // Fail loud on unknown enum value — the local-stub FormSchemaStatus
        // is sealed today but the canonical-substrate sweep will reshape
        // this enum; silent fall-through to Published would silently
        // promote unknown future values to visible (§Trust posture says
        // fail-loud, not fail-safe-to-published).
        _ => throw new InvalidOperationException(
            $"Unknown FormSchemaStatus value '{status}' — update MapFormSchemaStatus when the substrate enum gains values."),
    };

    private static DocumentStatus MapBundleStatus(BundleStatus status) => status switch
    {
        BundleStatus.Draft => DocumentStatus.Draft,
        BundleStatus.Deprecated => DocumentStatus.Archived,
        _ => DocumentStatus.Published,
    };

    private static DocumentStatus MapLeasePhase(LeasePhase phase) => phase switch
    {
        LeasePhase.Draft => DocumentStatus.Draft,
        LeasePhase.AwaitingSignature => DocumentStatus.PendingSignature,
        LeasePhase.Cancelled or LeasePhase.Terminated => DocumentStatus.Archived,
        _ => DocumentStatus.Published,
    };

    private sealed class EnvelopeObserver : IMissionEnvelopeObserver
    {
        private readonly ChannelWriter<bool> _writer;

        public EnvelopeObserver(ChannelWriter<bool> writer) => _writer = writer;

        public ValueTask OnChangedAsync(EnvelopeChange change, CancellationToken ct = default)
        {
            _writer.TryWrite(true);
            return ValueTask.CompletedTask;
        }
    }
}
