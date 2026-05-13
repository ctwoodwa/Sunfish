using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
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
/// <see cref="IMissionEnvelopeProvider"/> into the Atmosphere tab per the
/// XO ruling 2026-05-06T20-00Z.
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
/// counts. Overall health is Green/Yellow/Orange/Red per ADR 0082 §2.
/// </para>
/// <para>
/// <b>SubscribeSnapshotAsync posture:</b> emits one snapshot on subscribe
/// and re-polls on <see cref="SickBayOptions.FallbackPollingInterval"/>
/// (default 60s). Push-driven invalidation via IMissionEnvelopeObserver
/// is deferred to a future phase.
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
        yield return await BuildSnapshotAsync(ct).ConfigureAwait(false);

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
        if (purposes.Count == 0)
        {
            return [];
        }

        // Phase 2 deterministic projection: each registered purpose
        // becomes a row with a Suppressed record count (no real
        // pharmacy backend wired yet) and a Current rotation status.
        // Phase 3b wires in the W#32 / ADR 0046-A2 rotation pipeline,
        // which will source real LastRotatedAt + RecordCount values.
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
        // ADR 0082-A1: null provider → Unknown sentinel (not Green). UI must
        // render a neutral pending state until the provider is registered.
        if (_envelopeProvider is null)
        {
            return new AtmosphereReadout(
                OverallHealth: AtmosphereHealth.Unknown,
                WarningProbeCount: 0,
                CriticalProbeCount: 0,
                ForceEnableActive: false, // Phase 3: wire IInstallForceEnableSurface.HasActiveInstallOverrideAsync
                CapturedAt: capturedAt);
        }

        MissionEnvelope envelope;
        try
        {
            envelope = await _envelopeProvider.GetCurrentAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Provider fault: return Unknown rather than surfacing a misleading health value.
            return new AtmosphereReadout(
                OverallHealth: AtmosphereHealth.Unknown,
                WarningProbeCount: 0,
                CriticalProbeCount: 0,
                ForceEnableActive: false, // Phase 3: wire IInstallForceEnableSurface.HasActiveInstallOverrideAsync
                CapturedAt: capturedAt);
        }

        var (warnings, criticals) = CountProbes(envelope);
        return new AtmosphereReadout(
            OverallHealth: Classify(warnings, criticals),
            WarningProbeCount: warnings,
            CriticalProbeCount: criticals,
            ForceEnableActive: false, // Phase 3: wire IInstallForceEnableSurface.HasActiveInstallOverrideAsync
            CapturedAt: capturedAt);
    }

    /// <summary>
    /// Counts warning + critical probe statuses across all 10 MissionEnvelope
    /// dimensions per XO ruling 2026-05-06T20-00Z-w54-phase2b.
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

    private static AtmosphereHealth Classify(int w, int c) => (w, c) switch
    {
        (0, 0) => AtmosphereHealth.Green,
        (_, 0) => AtmosphereHealth.Yellow,
        (_, 1) => AtmosphereHealth.Orange,
        _      => AtmosphereHealth.Red,
    };
}
