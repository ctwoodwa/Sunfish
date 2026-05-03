using System.ComponentModel;
using System.Runtime.CompilerServices;
using Sunfish.Kernel.Security.Attestation;
using Sunfish.UIAdapters.Blazor.Components.LocalFirst;

namespace Sunfish.Anchor.Services;

/// <summary>
/// In-memory session state for the Anchor accelerator.
/// Tracks the three paper §13.2 status-bar indicators (node health, link status,
/// data freshness), the onboarded node identity (NodeId + TeamId from the
/// accepted <see cref="RoleAttestation"/>), and the last-synced timestamp
/// surfaced by <see cref="SunfishFreshnessBadge"/>.
/// </summary>
/// <remarks>
/// <para>
/// Wave 3.3 + 3.4 scope: this service is manually driven. Real wiring into
/// kernel-sync's <c>IGossipDaemon</c> and local-node-host IPC is deferred to
/// Wave 4+. <see cref="SetState"/> exists so demo screens and future real
/// subscribers can change state and see the UI react via
/// <see cref="INotifyPropertyChanged"/>.
/// </para>
/// <para>
/// Onboarding persistence is also out of scope; the service loses its state on
/// process exit. The follow-up slice (Wave 3-tail or Wave 4) will persist the
/// accepted attestation bundle via <c>IEncryptedStore</c> so repeat launches
/// skip the onboarding flow.
/// </para>
/// </remarks>
public sealed class AnchorSessionService : INotifyPropertyChanged
{
    private SyncState _nodeHealth = SyncState.Offline;
    private SyncState _linkStatus = SyncState.Offline;
    private SyncState _dataFreshness = SyncState.Offline;
    private DateTimeOffset? _lastSyncedAt;
    private string? _nodeId;
    private string? _teamId;
    private RoleAttestation? _attestation;

    /// <summary>Hex-encoded subject public key (this node's identity). <c>null</c> until onboarded.</summary>
    public string? NodeId
    {
        get => _nodeId;
        private set => SetField(ref _nodeId, value);
    }

    /// <summary>Hex-encoded team identifier. <c>null</c> until onboarded.</summary>
    public string? TeamId
    {
        get => _teamId;
        private set => SetField(ref _teamId, value);
    }

    /// <summary>Local node process / background sync worker health (paper §13.2 indicator 1).</summary>
    public SyncState NodeHealth
    {
        get => _nodeHealth;
        private set => SetField(ref _nodeHealth, value);
    }

    /// <summary>Transport link to upstream / peer nodes (paper §13.2 indicator 2).</summary>
    public SyncState LinkStatus
    {
        get => _linkStatus;
        private set => SetField(ref _linkStatus, value);
    }

    /// <summary>Aggregate data-currency across tracked records (paper §13.2 indicator 3).</summary>
    public SyncState DataFreshness
    {
        get => _dataFreshness;
        private set => SetField(ref _dataFreshness, value);
    }

    /// <summary>Timestamp of the most recent successful sync / onboarding apply.</summary>
    public DateTimeOffset? LastSyncedAt
    {
        get => _lastSyncedAt;
        private set => SetField(ref _lastSyncedAt, value);
    }

    /// <summary>The role attestation accepted at onboarding, if any.</summary>
    public RoleAttestation? Attestation => _attestation;

    /// <summary><c>true</c> once a founder or joiner onboarding has completed.</summary>
    public bool IsOnboarded => NodeId is not null;

    /// <summary>Raised when any observable property changes.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Called after QR-scan (or founder) onboarding completes. Materializes
    /// <see cref="NodeId"/> / <see cref="TeamId"/> from <paramref name="attestation"/>,
    /// stamps <see cref="LastSyncedAt"/>, and transitions node health to
    /// <see cref="SyncState.Healthy"/>.
    /// </summary>
    /// <param name="attestation">The attestation identifying this node in the team.</param>
    /// <param name="initialSnapshot">
    /// Initial CRDT snapshot bytes decoded from the QR payload (or empty for a
    /// founder bundle). Currently retained by the caller; this method does not
    /// apply it to a store. Wave 4 wires this into kernel-crdt.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    public Task OnboardAsync(
        RoleAttestation attestation,
        ReadOnlyMemory<byte> initialSnapshot,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(attestation);
        ct.ThrowIfCancellationRequested();

        _attestation = attestation;
        NodeId = Convert.ToHexString(attestation.SubjectPublicKey);
        TeamId = Convert.ToHexString(attestation.TeamId);
        LastSyncedAt = DateTimeOffset.UtcNow;
        // Onboarding just completed — the local node is healthy but we haven't
        // yet established a peer link. DataFreshness = Healthy since we just
        // applied a fresh snapshot.
        NodeHealth = SyncState.Healthy;
        LinkStatus = SyncState.Offline;
        DataFreshness = SyncState.Healthy;
        OnPropertyChanged(nameof(IsOnboarded));

        // initialSnapshot intentionally unused in this wave (see XML docs above).
        _ = initialSnapshot;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Resets to an un-onboarded state. Useful for tests and for the future
    /// "sign out / wipe device" action.
    /// </summary>
    public void Reset()
    {
        _attestation = null;
        NodeId = null;
        TeamId = null;
        LastSyncedAt = null;
        NodeHealth = SyncState.Offline;
        LinkStatus = SyncState.Offline;
        DataFreshness = SyncState.Offline;
        OnPropertyChanged(nameof(IsOnboarded));
    }

    /// <summary>
    /// Applies a manual state transition. Used by the demo home page and, in
    /// future waves, by the subscriber that observes <c>IGossipDaemon</c>.
    /// </summary>
    public void SetState(SyncState nodeHealth, SyncState linkStatus, SyncState dataFreshness)
    {
        NodeHealth = nodeHealth;
        LinkStatus = linkStatus;
        DataFreshness = dataFreshness;
    }

    /// <summary>Stamps <see cref="LastSyncedAt"/> to <paramref name="at"/>.</summary>
    public void MarkSynced(DateTimeOffset at) => LastSyncedAt = at;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }
        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged(string? propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));
}
