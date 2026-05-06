using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.SickBay;

namespace Sunfish.Blocks.SickBay;

/// <summary>
/// Reference <see cref="ISickBayDataProvider"/> per ADR 0082 §1+§2 +
/// W#54 Phase 2. Materializes a Sick Bay snapshot by aggregating the
/// host-registered <see cref="SickBayOptions.RegisteredFieldPurposes"/>
/// into <see cref="PharmacyInventoryEntry"/> rows, and returns a
/// placeholder <see cref="AtmosphereReadout"/> + empty Lab list until
/// the Phase 2b Mission Envelope integration lands.
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
/// <b>Phase 2b deferral (Mission Envelope integration):</b> the
/// hand-off §2.1 cited an <c>IMissionEnvelopeProvider.GetCurrentEnvelope(tenant)</c>
/// API that does not exist on origin/main (the actual contract is
/// <c>GetCurrentAsync(ct)</c>, no tenant param, since MissionEnvelope is
/// process-level not tenant-level). The dimension-to-probe-status
/// mapping needed to derive <see cref="AtmosphereReadout.WarningProbeCount"/>
/// + <see cref="AtmosphereReadout.CriticalProbeCount"/> is also non-
/// trivial (MissionEnvelope exposes typed capability records, not a
/// flat probe-result list). Phase 2b will resolve both via an XO
/// ruling; Phase 2 ships pharmacy + a Green stub Atmosphere so the
/// dashboard can render and downstream wiring can land in parallel.
/// Halt-condition H2.A is acknowledged in the cob-question beacon
/// filed alongside this PR.
/// </para>
/// <para>
/// <b>SubscribeSnapshotAsync posture:</b> emits one snapshot on subscribe
/// and re-polls on
/// <see cref="SickBayOptions.FallbackPollingInterval"/> (default 60s).
/// Push-driven invalidation lands in Phase 2b.
/// </para>
/// </remarks>
internal sealed class SickBayDataProvider : ISickBayDataProvider
{
    private readonly IOptions<SickBayOptions> _options;
    private readonly TimeProvider _time;

    public SickBayDataProvider(
        IOptions<SickBayOptions> options,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public Task<SickBaySnapshot> GetSnapshotAsync(
        TenantId tenant,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(BuildSnapshot());
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<SickBaySnapshot> SubscribeSnapshotAsync(
        TenantId tenant,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // Emit one snapshot immediately, then re-poll on the configured
        // fallback cadence. Phase 2b adds push-driven invalidation via
        // IMissionEnvelopeObserver.
        yield return BuildSnapshot();

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
            yield return BuildSnapshot();
        }
    }

    private SickBaySnapshot BuildSnapshot()
    {
        var capturedAt = _time.GetUtcNow();
        return new SickBaySnapshot(
            Pharmacy: BuildPharmacy(capturedAt),
            Lab: [],
            Atmosphere: BuildAtmosphereStub(capturedAt),
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

    private static AtmosphereReadout BuildAtmosphereStub(DateTimeOffset capturedAt) =>
        new AtmosphereReadout(
            OverallHealth: AtmosphereHealth.Green,
            WarningProbeCount: 0,
            CriticalProbeCount: 0,
            ForceEnableActive: false,
            CapturedAt: capturedAt);
}
