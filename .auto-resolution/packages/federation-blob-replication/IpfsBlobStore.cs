using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sunfish.Federation.BlobReplication.Kubo;
using Sunfish.Foundation.Blobs;

namespace Sunfish.Federation.BlobReplication;

/// <summary>
/// <see cref="IBlobStore"/> implementation backed by a Kubo (IPFS) daemon's HTTP RPC.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="PutAsync"/> adds content via <c>/api/v0/add?cid-version=1&amp;raw-leaves=true</c>, which
/// makes Kubo return a CID in the same encoding Sunfish's <see cref="Cid"/> uses (v1, raw codec,
/// SHA-256, base32-lowercase). The returned CID is parsed with <see cref="Cid.Parse"/> and, as a
/// belt-and-suspenders check, compared against <see cref="Cid.FromBytes"/> computed from the
/// original payload — a mismatch would indicate divergence between Sunfish and Kubo's CID
/// implementations and is treated as a fatal assertion.
/// </para>
/// <para>
/// <see cref="GetAsync"/> returns <see langword="null"/> when the daemon reports the block is not
/// locally available. This matches the <see cref="IBlobStore"/> contract: local-only semantics, no
/// implicit remote fetch beyond whatever Kubo chooses to do via libp2p.
/// </para>
/// </remarks>
public sealed class IpfsBlobStore : IBlobStore
{
    private readonly IKuboHttpClient _kubo;
    private readonly KuboBlobStoreOptions _options;
    private readonly ILogger<IpfsBlobStore> _logger;

    /// <summary>Constructs a Kubo-backed blob store.</summary>
    public IpfsBlobStore(
        IKuboHttpClient kubo,
        IOptions<KuboBlobStoreOptions> options,
        ILogger<IpfsBlobStore>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(kubo);
        ArgumentNullException.ThrowIfNull(options);
        _kubo = kubo;
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? NullLogger<IpfsBlobStore>.Instance;
    }

    /// <inheritdoc />
    public async ValueTask<Cid> PutAsync(ReadOnlyMemory<byte> content, CancellationToken ct = default)
    {
        var response = await _kubo.AddAsync(content, pin: _options.PinOnPut, ct).ConfigureAwait(false);

        Cid fromKubo;
        try
        {
            fromKubo = Cid.Parse(response.Hash);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                $"Kubo returned a CID Sunfish cannot parse: '{response.Hash}'. Expected a CID v1 " +
                "base32-lowercase string. Confirm the Kubo daemon version and the cid-version=1 / " +
                "raw-leaves=true query parameters.",
                ex);
        }

        // Raw-codec CIDs start with "bafkrei..." (CID v1 / raw / SHA-256). Dag-pb CIDs start with
        // "bafybei..." and indicate Kubo chunked the payload into a UnixFS DAG (content was over
        // the chunker's block-size threshold — 256 KiB by default). Only the raw-codec case is
        // byte-equivalent to Sunfish's Cid.FromBytes; the dag-pb case roundtrips content but
        // addressing interop with FileSystemBlobStore is documented as single-block only.
        if (fromKubo.Value.StartsWith("bafkrei", StringComparison.Ordinal))
        {
            var fromSunfish = Cid.FromBytes(content.Span);
            if (!string.Equals(fromKubo.Value, fromSunfish.Value, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"CID mismatch between Kubo ('{fromKubo.Value}') and Sunfish ('{fromSunfish.Value}'). " +
                    "Sunfish.Foundation.Blobs.Cid and the Kubo daemon must agree on the CID v1 / raw / SHA-256 " +
                    "encoding for single-block content; a divergence here will break federation addressing.");
            }

            _logger.LogDebug("IpfsBlobStore put {Cid} ({Bytes} bytes, pinned={Pinned}).",
                fromSunfish.Value, content.Length, _options.PinOnPut);
            return fromSunfish;
        }

        _logger.LogDebug(
            "IpfsBlobStore put {Cid} ({Bytes} bytes, pinned={Pinned}) — non-raw codec; Kubo " +
            "chunked the payload into a UnixFS DAG. Sunfish.Foundation.Blobs.Cid.FromBytes would " +
            "compute a different value for this payload, so cross-store parity with " +
            "FileSystemBlobStore is only guaranteed for payloads under Kubo's 256 KiB chunk size.",
            fromKubo.Value, content.Length, _options.PinOnPut);
        return fromKubo;
    }

    /// <inheritdoc />
    public async ValueTask<ReadOnlyMemory<byte>?> GetAsync(Cid cid, CancellationToken ct = default)
    {
        var bytes = await _kubo.CatAsync(cid.Value, ct).ConfigureAwait(false);
        return bytes is null ? null : (ReadOnlyMemory<byte>)bytes;
    }

    /// <inheritdoc />
    public async ValueTask<bool> ExistsLocallyAsync(Cid cid, CancellationToken ct = default)
    {
        var list = await _kubo.PinListAsync(cid.Value, ct).ConfigureAwait(false);
        return list.Keys is not null && list.Keys.ContainsKey(cid.Value);
    }

    /// <inheritdoc />
    public async ValueTask PinAsync(Cid cid, CancellationToken ct = default)
    {
        await _kubo.PinAddAsync(cid.Value, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask UnpinAsync(Cid cid, CancellationToken ct = default)
    {
        await _kubo.PinRmAsync(cid.Value, ct).ConfigureAwait(false);
    }
}
