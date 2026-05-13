using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MissionSpace;
using Sunfish.Foundation.SickBay;

namespace Sunfish.Blocks.SickBay;

/// <summary>
/// Reference <see cref="ISickBayDataProvider"/> per ADR 0082 §1+§2 +
/// W#54 Phase 2 + Phase 2b. Materializes a Sick Bay snapshot by aggregating
/// the host-registered <see cref="SickBayOptions.RegisteredFieldPurposes"/>
/// into <see cref="PharmacyInventoryEntry"/> rows, and projects the
/// 10-dimension <see cref="MissionEnvelope"/> from
/// <see cref="IMissionEnvelopeProvider"/> into the Atmosphere tab per
/// ADR 0082-A1.2.
/// </summary>
/// <remarks>
/// <para>
/// <b>H4 invariant (load-bearing, ADR 0046-A2 §4 + ADR 0082 §Trust):</b>
/// this implementation MUST NOT depend on
/// <c>Sunfish.Foundation.Recovery.IFieldDecryptor</c>. The k=3 anonymity
/// floor in <see cref="PharmacyRecordCount"/> is the only authority
/// the pharmacy browse pane needs; decrypting record values lives on a
/// separate per-document detail surface (different authority cell).
/// The H4 reflection test in
/// <c>SickBayDataProviderTests.DoesNotReference_IFieldDecryptor</c>
/// pins this invariant.
/// </para>
/// <para>
/// <b>Atmosphere projection:</b> when <see cref="IMissionEnvelopeProvider"/>
/// is null (not registered by host), returns
/// <see cref="AtmosphereHealth.Unknown"/> — the safe sentinel meaning "real
/// data not yet available." When the provider is registered, each of the 10
/// typed dimension probes contributes to
/// <see cref="AtmosphereReadout.WarningProbeCount"/> (Stale/PartiallyDegraded)
/// and <see cref="AtmosphereReadout.CriticalProbeCount"/> (Failed/Unreachable)
/// counts. Overall health is Green/Yellow/Orange/Red per ADR 0082-A1.2.2.
/// </para>
/// <para>
/// <b>SubscribeSnapshotAsync posture:</b> when a provider is registered,
/// emits one snapshot on subscribe, then push-drives subsequent snapshots via
/// <see cref="IMissionEnvelopeObserver"/> with a
/// <see cref="SickBayOptions.FallbackPollingInterval"/> backstop. When no
/// provider is registered, falls back to polling-only.
/// </para>
/// </remarks>
internal sealed class SickBayDataProvider : ISickBayDataProvider
{
    private readonly IOptions<SickBayOptions> _options;
    private readonly IMissionEnvelopeProvider? _envelopeProvider;
    private readonly TimeProvider _time;

    public SickBayDataProvider(
        IOptions<SickBayOptions> options,
        IMissionEnvelopeProvider? envelopeProvider = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _envelopeProvider = envelopeProvider;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<SickBaySnapshot> GetSnapshotAsync(
        TenantId tenant,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return await BuildSnapshotAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<SickBaySnapshot> SubscribeSnapshotAsync(
        TenantId tenant,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (_envelopeProvider is null)
        {
            yield return await BuildSnapshotAsync(ct).ConfigureAwait(false);
            await foreach (var snap in PollFallbackAsync(ct).ConfigureAwait(false))
            {
                yield return snap;
            }
            yield break;
        }

        // Observer-driven: bounded channel (capacity 1, DropOldest) coalesces
        // concurrent envelope-change events during multi-dimension probe runs.
        // SingleWriter=true: one EnvelopeChangeObserver per subscription.
        var channel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = true,
            SingleReader = true,
        });
        var observer = new EnvelopeChangeObserver(channel.Writer);

        // Subscribe BEFORE building the initial snapshot so no envelope change
        // fires in the window between snapshot completion and observer registration
        // (W#54 P2b council Blocking B2).
        _envelopeProvider.Subscribe(observer);
        try
        {
            // Drain any signal that arrived during subscription setup so the
            // consumer receives a clean baseline before entering the change loop.
            while (channel.Reader.TryRead(out _)) { }

            yield return await BuildSnapshotAsync(ct).ConfigureAwait(false);

            while (!ct.IsCancellationRequested)
            {
                // A new CTS per iteration so each FallbackPollingInterval timer
                // fires independently and doesn't compound across loop iterations.
                using var timedOut = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timedOut.CancelAfter(_options.Value.FallbackPollingInterval);
                try
                {
                    // Wait for an observer signal OR the fallback polling interval.
                    await channel.Reader.ReadAsync(timedOut.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    // Fallback timer fired — not the consumer's own cancellation.
                }

                if (ct.IsCancellationRequested) yield break;
                yield return await BuildSnapshotAsync(ct).ConfigureAwait(false);
            }
        }
        finally
        {
            _envelopeProvider.Unsubscribe(observer);
        }
    }

    private async IAsyncEnumerable<SickBaySnapshot> PollFallbackAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        var interval = _options.Value.FallbackPollingInterval;
        if (interval <= TimeSpan.Zero) yield break;

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
            yield return await BuildSnapshotAsync(ct).ConfigureAwait(false);
        }
    }

    private async ValueTask<SickBaySnapshot> BuildSnapshotAsync(CancellationToken ct)
    {
        var capturedAt = _time.GetUtcNow();
        var atmosphere = await BuildAtmosphereAsync(capturedAt, ct).ConfigureAwait(false);
        return new SickBaySnapshot(
            Pharmacy: BuildPharmacy(capturedAt),
            Lab: [],
            Atmosphere: atmosphere,
            MedevacState: MedevacState.Idle,
            CapturedAt: capturedAt);
    }

    private IReadOnlyList<PharmacyInventoryEntry> BuildPharmacy(DateTimeOffset capturedAt)
    {
        var purposes = _options.Value.RegisteredFieldPurposes;
        if (purposes.Count == 0) return [];

        return purposes
            .Select(kvp => new PharmacyInventoryEntry(
                FieldPurpose: kvp.Key,
                FriendlyName: kvp.Value,
                RecordCount: PharmacyRecordCount.Suppressed,
                LastRotatedAt: capturedAt,
                RotationStatus: RotationHealth.Current,
                HasCompromiseFlag: false))
            .ToList();
    }

    private async ValueTask<AtmosphereReadout> BuildAtmosphereAsync(
        DateTimeOffset capturedAt,
        CancellationToken ct)
    {
        if (_envelopeProvider is null)
        {
            return BuildAtmosphereUnknown(capturedAt);
        }

        MissionEnvelope envelope;
        try
        {
            envelope = await _envelopeProvider.GetCurrentAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return BuildAtmosphereUnknown(capturedAt);
        }

        return BuildAtmosphereFromEnvelope(envelope, capturedAt);
    }

    private static AtmosphereReadout BuildAtmosphereUnknown(DateTimeOffset capturedAt) =>
        new AtmosphereReadout(
            OverallHealth: AtmosphereHealth.Unknown,
            WarningProbeCount: 0,
            CriticalProbeCount: 0,
            ForceEnableActive: false,
            CapturedAt: capturedAt);

    internal static AtmosphereReadout BuildAtmosphereFromEnvelope(
        MissionEnvelope envelope, DateTimeOffset capturedAt)
    {
        var (warnings, criticals) = CountProbes(envelope);
        return new AtmosphereReadout(
            OverallHealth: Classify(warnings, criticals),
            WarningProbeCount: warnings,
            CriticalProbeCount: criticals,
            ForceEnableActive: false,
            CapturedAt: capturedAt);
    }

    /// <summary>
    /// Counts warning + critical probe statuses across all 10 MissionEnvelope
    /// dimensions per ADR 0082-A1.2.1.
    /// Warning = Stale | PartiallyDegraded; Critical = Failed | Unreachable.
    /// </summary>
    private static (int warnings, int criticals) CountProbes(MissionEnvelope e)
    {
        var statuses = new[]
        {
            e.Hardware.ProbeStatus,
            e.User.ProbeStatus,
            e.Regulatory.ProbeStatus,
            e.Runtime.ProbeStatus,
            e.FormFactor.ProbeStatus,
            e.Edition.ProbeStatus,
            e.Network.ProbeStatus,
            e.TrustAnchor.ProbeStatus,
            e.SyncState.ProbeStatus,
            e.VersionVector.ProbeStatus,
        };
        int w = statuses.Count(s => s is ProbeStatus.Stale or ProbeStatus.PartiallyDegraded);
        int c = statuses.Count(s => s is ProbeStatus.Failed or ProbeStatus.Unreachable);
        return (w, c);
    }

    /// <summary>
    /// Derives overall <see cref="AtmosphereHealth"/> per ADR 0082-A1.2.2.
    /// 3+ criticals → Red; 1–2 criticals → Orange; 1+ warnings (no criticals) → Yellow.
    /// </summary>
    private static AtmosphereHealth Classify(int w, int c) => (w, c) switch
    {
        (_, >= 3) => AtmosphereHealth.Red,
        (_, >= 1) => AtmosphereHealth.Orange,
        (>= 1, 0) => AtmosphereHealth.Yellow,
        _         => AtmosphereHealth.Green,
    };

    /// <summary>
    /// Per-subscription <see cref="IMissionEnvelopeObserver"/> that forwards
    /// change signals into a bounded channel. Created per
    /// <see cref="SubscribeSnapshotAsync"/> invocation; subscribed/unsubscribed
    /// within that call's try/finally.
    /// </summary>
    private sealed class EnvelopeChangeObserver : IMissionEnvelopeObserver
    {
        private readonly ChannelWriter<bool> _writer;

        public EnvelopeChangeObserver(ChannelWriter<bool> writer) => _writer = writer;

        public ValueTask OnChangedAsync(EnvelopeChange change, CancellationToken ct = default)
        {
            // TryWrite drops the signal when the channel is full (DropOldest semantics
            // coalesce rapid concurrent changes into one re-evaluation per consumer tick).
            _writer.TryWrite(true);
            return ValueTask.CompletedTask;
        }
    }
}
