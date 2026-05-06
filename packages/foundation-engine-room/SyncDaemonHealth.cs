using System;

namespace Sunfish.Foundation.EngineRoom;

/// <summary>
/// Snapshot of the sync daemon's health per ADR 0079 §1. Per cohort
/// precedent (W#46 / W#49 / W#55), <see cref="DateTimeOffset"/> stands in
/// for the hand-off's <c>NodaTime.Instant</c> — NodaTime is not on
/// <c>Directory.Packages.props</c>; future ADR amendment will migrate
/// every cohort time-bearing record at once.
/// </summary>
/// <param name="Status">Discriminator for daemon state.</param>
/// <param name="PeerCount">Number of peers currently connected.</param>
/// <param name="EventsThroughput">Events-per-second moving average over the last sample window.</param>
/// <param name="GossipCycles">Total gossip cycles executed since daemon start.</param>
/// <param name="AsOf">Wall-clock time the snapshot was materialized.</param>
public sealed record SyncDaemonHealth(
    SyncDaemonStatus Status,
    int PeerCount,
    double EventsThroughput,
    long GossipCycles,
    DateTimeOffset AsOf);
