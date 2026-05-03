namespace Sunfish.Kernel.Sync.Discovery;

/// <summary>
/// Phase 1 G4 — paper §17.2 tier-3 managed-relay peer discovery. Surfaces a
/// single, statically-configured Bridge relay endpoint as a peer the gossip
/// daemon can dial across the WAN. Coexists with
/// <see cref="MdnsPeerDiscovery"/> (tier-1 LAN) so a single Anchor reaches
/// LAN peers directly and remote peers via the relay.
/// </summary>
/// <remarks>
/// <para>
/// Discovery is config-driven, not network-driven. <see cref="StartAsync"/>
/// reads <see cref="ManagedRelayPeerDiscoveryOptions"/>, raises
/// <see cref="PeerDiscovered"/> exactly once with the configured relay's
/// advertisement, and sits idle. <see cref="StopAsync"/> raises
/// <see cref="PeerLost"/> for that single peer and clears state. There is
/// no periodic re-announce, no TTL sweep, no broker — the relay endpoint is
/// authoritative until Anchor settings change.
/// </para>
/// <para>
/// When <see cref="ManagedRelayPeerDiscoveryOptions.RelayUrl"/> is empty,
/// <see cref="StartAsync"/> is a no-op and <see cref="KnownPeers"/> stays
/// empty. This is the LAN-only deployment shape: Anchor on a corporate
/// LAN where mDNS sees every team peer and the WAN relay is unwanted.
/// </para>
/// <para>
/// <b>What this does not do:</b> verify the relay's identity, dial the
/// transport, or refresh the relay's advertisement. The HELLO handshake
/// (in <c>HandshakeProtocol</c>) verifies the relay signs with
/// <see cref="ManagedRelayPeerDiscoveryOptions.RelayPublicKey"/>; a
/// mismatch fails the round at the wire layer. Discovery's contract is
/// only "surface this peer to the daemon."
/// </para>
/// </remarks>
public sealed class ManagedRelayPeerDiscovery : IPeerDiscovery
{
    private readonly ManagedRelayPeerDiscoveryOptions _options;
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);

    private PeerAdvertisement? _self;
    private PeerAdvertisement? _relayAd;
    private bool _disposed;

    /// <inheritdoc />
    public event EventHandler<PeerDiscoveredEventArgs>? PeerDiscovered;

    /// <inheritdoc />
    public event EventHandler<PeerLostEventArgs>? PeerLost;

    /// <summary>Construct with the supplied options instance.</summary>
    public ManagedRelayPeerDiscovery(ManagedRelayPeerDiscoveryOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public IReadOnlyCollection<PeerAdvertisement> KnownPeers =>
        _relayAd is null ? Array.Empty<PeerAdvertisement>() : new[] { _relayAd };

    /// <inheritdoc />
    public async Task StartAsync(PeerAdvertisement self, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(self);
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _lifecycleLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_self is not null)
            {
                return; // idempotent start
            }
            _self = self;

            // Empty RelayUrl = LAN-only deployment. We installed cleanly but
            // produce no peers; the daemon's combined peer set comes entirely
            // from sibling discovery sources (e.g. mDNS).
            if (string.IsNullOrWhiteSpace(_options.RelayUrl))
            {
                return;
            }

            // The relay's PeerAdvertisement adopts the local node's TeamId so
            // any team-aware filter on a downstream consumer (the gossip
            // daemon's AttachDiscovery glue, future relay multiplexers) treats
            // the relay as a same-team peer. The relay itself is infrastructure
            // — it forwards traffic for whichever team's HELLO handshake
            // succeeds — so labelling it with self.TeamId is the correct fit.
            _relayAd = new PeerAdvertisement(
                NodeId: _options.RelayNodeId,
                Endpoint: _options.RelayUrl,
                PublicKey: _options.RelayPublicKey,
                TeamId: self.TeamId,
                SchemaVersion: _options.RelaySchemaVersion,
                Metadata: new Dictionary<string, string>
                {
                    ["tier"] = "3-managed-relay",
                    ["zone"] = "C",
                });

            PeerDiscovered?.Invoke(this, new PeerDiscoveredEventArgs(_relayAd));
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken ct)
    {
        await _lifecycleLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_self is null)
            {
                return;
            }
            _self = null;

            if (_relayAd is not null)
            {
                var nodeId = _relayAd.NodeId;
                _relayAd = null;
                PeerLost?.Invoke(this, new PeerLostEventArgs(nodeId));
            }
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            /* swallow — DisposeAsync should not throw */
        }
        _lifecycleLock.Dispose();
    }
}
