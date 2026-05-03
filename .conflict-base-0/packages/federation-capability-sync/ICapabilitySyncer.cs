using Sunfish.Federation.Common;

namespace Sunfish.Federation.CapabilitySync;

/// <summary>
/// Reconciles the local capability-op store against a remote peer's store via a RIBLT-first
/// protocol with a full-set fallback. Signature verification is applied to every received op
/// before it is persisted.
/// </summary>
public interface ICapabilitySyncer
{
    /// <summary>
    /// Reconciles the local op store with <paramref name="peer"/>. Transfers, counts, and any
    /// rejections are reported in the returned result.
    /// </summary>
    ValueTask<CapabilityReconcileResult> ReconcileAsync(PeerDescriptor peer, CancellationToken ct = default);
}
