using System.Collections.Generic;
using Sunfish.Federation.Common;
using Sunfish.Kernel.Audit;

namespace Sunfish.Foundation.Transport.Audit;

/// <summary>
/// Builds <see cref="AuditPayload"/> bodies for the W#30 three-tier
/// transport audit events (ADR 0061). Mirrors the
/// <c>VersionVectorAuditPayloads</c> + <c>FieldEncryptionAuditPayloadFactory</c>
/// conventions: keys alphabetized; bodies opaque to the substrate.
/// </summary>
public static class TransportAuditPayloads
{
    /// <summary>Body for <see cref="AuditEventType.TransportTierSelected"/>.</summary>
    public static AuditPayload TierSelected(PeerId peer, TransportTier tier, string? adapterName) =>
        new(new Dictionary<string, object?>
        {
            ["adapter_name"] = adapterName,
            ["peer_id"] = peer.Value,
            ["tier"] = tier.ToString(),
        });

    /// <summary>Body for <see cref="AuditEventType.MeshDeviceRegistered"/>.</summary>
    public static AuditPayload MeshDeviceRegistered(PeerId peer, string adapterName, string deviceName) =>
        new(new Dictionary<string, object?>
        {
            ["adapter_name"] = adapterName,
            ["device_name"] = deviceName,
            ["peer_id"] = peer.Value,
        });

    /// <summary>Body for <see cref="AuditEventType.MeshHandshakeCompleted"/>.</summary>
    public static AuditPayload MeshHandshakeCompleted(PeerId peer, string adapterName) =>
        new(new Dictionary<string, object?>
        {
            ["adapter_name"] = adapterName,
            ["peer_id"] = peer.Value,
        });

    /// <summary>Body for <see cref="AuditEventType.MeshTransportFailed"/>.</summary>
    public static AuditPayload MeshTransportFailed(PeerId peer, string adapterName, string reason) =>
        new(new Dictionary<string, object?>
        {
            ["adapter_name"] = adapterName,
            ["peer_id"] = peer.Value,
            ["reason"] = reason,
        });

    /// <summary>Body for <see cref="AuditEventType.TransportFallbackToRelay"/>.</summary>
    public static AuditPayload TransportFallbackToRelay(PeerId peer, string outcome) =>
        new(new Dictionary<string, object?>
        {
            ["outcome"] = outcome, // "Selected" or "Failed" per ADR 0061 §"Tier selection algorithm"
            ["peer_id"] = peer.Value,
        });
}
