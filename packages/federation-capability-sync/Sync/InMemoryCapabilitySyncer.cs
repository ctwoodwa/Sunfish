using System.Security.Cryptography;
using Sunfish.Federation.CapabilitySync.Riblt;
using Sunfish.Federation.Common;
using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Crypto;

namespace Sunfish.Federation.CapabilitySync.Sync;

/// <summary>
/// In-process <see cref="ICapabilitySyncer"/>. Resolves the peer's store through a
/// test-harness shim (<c>peerStoreResolver</c>) rather than a wire transport — the shim is
/// replaced by the HTTP transport in a follow-up task.
/// </summary>
/// <remarks>
/// <para>Reconciliation flow:</para>
/// <list type="number">
///   <item><description>Build local and remote <see cref="RibltItem"/> sets (hash = folded GUID, checksum = SHA-256 over canonical-JSON payload).</description></item>
///   <item><description>Attempt RIBLT fast path with up to <see cref="MaxRibltBatches"/> symbol batches.</description></item>
///   <item><description>On decode success, pull the <c>RemoteOnly</c> nonces.</description></item>
///   <item><description>If RIBLT stalls or goes inconsistent, fall back to full-set set-difference.</description></item>
///   <item><description>For every wanted nonce, fetch, verify, and persist; reject on signature failure.</description></item>
/// </list>
/// </remarks>
public sealed class InMemoryCapabilitySyncer(
    ICapabilityOpStore localStore,
    Func<PeerId, ICapabilityOpStore?> peerStoreResolver,
    IOperationVerifier verifier) : ICapabilitySyncer
{
    private const int RibltBatchSize = 16;
    private const int MaxRibltBatches = 3;

    private readonly ICapabilityOpStore _localStore = localStore ?? throw new ArgumentNullException(nameof(localStore));
    private readonly Func<PeerId, ICapabilityOpStore?> _peerStoreResolver = peerStoreResolver ?? throw new ArgumentNullException(nameof(peerStoreResolver));
    private readonly IOperationVerifier _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));

    /// <inheritdoc />
    public ValueTask<CapabilityReconcileResult> ReconcileAsync(PeerDescriptor peer, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(peer);
        ct.ThrowIfCancellationRequested();

        var peerStore = _peerStoreResolver(peer.Id);
        if (peerStore is null)
        {
            return ValueTask.FromResult(new CapabilityReconcileResult(
                0, 0, 0, false, false, Array.Empty<Guid>()));
        }

        var localItems = BuildItemMap(_localStore);
        var remoteItems = BuildItemMap(peerStore);

        bool usedFast = false, usedFallback = false;
        List<Guid> wanted;

        var encoder = new RibltEncoder(remoteItems.Keys);
        RibltDecodeResult? result = null;
        int symbolsSoFar = 0;
        for (int round = 0; round < MaxRibltBatches; round++)
        {
            symbolsSoFar += RibltBatchSize;
            var symbols = encoder.Batch(0, symbolsSoFar);
            result = RibltDecoder.TryDecode(symbols, localItems.Keys.ToArray());
            if (result.Outcome == RibltDecodeOutcome.Success)
            {
                usedFast = true;
                break;
            }
        }

        if (result is not null && result.Outcome == RibltDecodeOutcome.Success)
        {
            wanted = result.RemoteOnly
                .Where(r => remoteItems.ContainsKey(r))
                .Select(r => remoteItems[r])
                .ToList();
        }
        else
        {
            usedFallback = true;
            var localNonces = new HashSet<Guid>(_localStore.AllNonces());
            wanted = peerStore.AllNonces().Where(n => !localNonces.Contains(n)).ToList();
        }

        int transferred = 0, alreadyPresent = 0, rejected = 0;
        var rejectedNonces = new List<Guid>();
        foreach (var nonce in wanted)
        {
            if (_localStore.Contains(nonce))
            {
                alreadyPresent++;
                continue;
            }

            var op = peerStore.TryGet(nonce);
            if (op is null)
            {
                rejected++;
                rejectedNonces.Add(nonce);
                continue;
            }

            if (!_verifier.Verify(op))
            {
                rejected++;
                rejectedNonces.Add(nonce);
                continue;
            }

            _localStore.Put(op);
            transferred++;
        }

        return ValueTask.FromResult(new CapabilityReconcileResult(
            transferred, alreadyPresent, rejected, usedFast, usedFallback, rejectedNonces));
    }

    private static Dictionary<RibltItem, Guid> BuildItemMap(ICapabilityOpStore store)
    {
        var map = new Dictionary<RibltItem, Guid>();
        foreach (var op in store.All())
        {
            var digest = DigestOf(op);
            var item = RibltItem.From(op.Nonce, digest);
            map[item] = op.Nonce;
        }
        return map;
    }

    private static ulong DigestOf(SignedOperation<CapabilityOp> op)
    {
        var bytes = CanonicalJson.Serialize(op.Payload);
        Span<byte> sha = stackalloc byte[32];
        SHA256.HashData(bytes, sha);
        return BitConverter.ToUInt64(sha[..8]);
    }
}
