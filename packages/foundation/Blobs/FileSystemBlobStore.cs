using System.Security.Cryptography;

namespace Sunfish.Foundation.Blobs;

/// <summary>
/// Filesystem-backed blob store. Default implementation for single-node Sunfish
/// deployments (dev, test, Bridge accelerator without federation).
/// </summary>
/// <remarks>
/// Layout: <c>{root}/{cid[0..2]}/{cid[2..4]}/{cid}</c> — two-level sharding prevents
/// a single directory from accumulating millions of files. Pinning is represented
/// by the presence of <c>{root}/.pins/{cid}</c> marker files; unpinned blobs remain
/// on disk until an explicit reclamation pass removes them (reclamation is not
/// implemented here — consumers that need it can call <see cref="ExistsLocallyAsync"/>
/// and delete based on their own policy).
/// </remarks>
public sealed class FileSystemBlobStore : IBlobStore
{
    private readonly string _root;
    private readonly string _pinsDir;

    public FileSystemBlobStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        _root = Path.GetFullPath(rootDirectory);
        _pinsDir = Path.Combine(_root, ".pins");
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(_pinsDir);
    }

    public async ValueTask<Cid> PutAsync(ReadOnlyMemory<byte> content, CancellationToken ct = default)
    {
        var cid = Cid.FromBytes(content.Span);
        var path = CidToPath(cid);

        if (File.Exists(path))
        {
            return cid;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Write to a temp file + atomic rename to avoid torn writes if the process crashes.
        var tempPath = path + ".tmp-" + Path.GetRandomFileName();
        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await stream.WriteAsync(content, ct);
            }
            File.Move(tempPath, path, overwrite: false);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* best-effort */ }
            }
            throw;
        }

        return cid;
    }

    /// <summary>
    /// Streams content directly to a temp file on disk while computing the CID incrementally
    /// (single-pass SHA-256 via <see cref="IncrementalHash"/>). The temp file is atomically
    /// renamed to the final CID-addressed path once hashing completes, guaranteeing no
    /// half-written files are ever visible as completed blobs.
    /// </summary>
    public async ValueTask<Cid> PutStreamingAsync(Stream content, CancellationToken ct = default)
    {
        // We don't know the CID until we've consumed the whole stream, so write to a temp
        // location first and rename once hashing is done.
        var tempPath = Path.Combine(_root, ".tmp-" + Path.GetRandomFileName());
        Directory.CreateDirectory(_root);

        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var copyBuffer = new byte[81920]; // 80 KiB — matches FileStream default copy buffer

        try
        {
            await using (var dest = new FileStream(
                tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: 81920, useAsync: true))
            {
                int read;
                while ((read = await content.ReadAsync(copyBuffer, ct)) > 0)
                {
                    hasher.AppendData(copyBuffer, 0, read);
                    await dest.WriteAsync(copyBuffer.AsMemory(0, read), ct);
                }
            }

            Span<byte> digest = stackalloc byte[Cid.Sha256DigestLength];
            hasher.GetHashAndReset(digest);
            var cid = Cid.FromDigest(digest);
            var finalPath = CidToPath(cid);

            if (File.Exists(finalPath))
            {
                // Duplicate: discard the temp copy — the content is already stored.
                File.Delete(tempPath);
                return cid;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);
            File.Move(tempPath, finalPath, overwrite: false);
            return cid;
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* best-effort */ }
            }
            throw;
        }
    }

    public async ValueTask<ReadOnlyMemory<byte>?> GetAsync(Cid cid, CancellationToken ct = default)
    {
        var path = CidToPath(cid);
        if (!File.Exists(path))
        {
            return null;
        }

        return await File.ReadAllBytesAsync(path, ct);
    }

    public ValueTask<bool> ExistsLocallyAsync(Cid cid, CancellationToken ct = default)
        => ValueTask.FromResult(File.Exists(CidToPath(cid)));

    public ValueTask PinAsync(Cid cid, CancellationToken ct = default)
    {
        var marker = Path.Combine(_pinsDir, cid.Value);
        if (!File.Exists(marker))
        {
            File.WriteAllText(marker, string.Empty);
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask UnpinAsync(Cid cid, CancellationToken ct = default)
    {
        var marker = Path.Combine(_pinsDir, cid.Value);
        if (File.Exists(marker))
        {
            File.Delete(marker);
        }
        return ValueTask.CompletedTask;
    }

    private string CidToPath(Cid cid)
    {
        var value = cid.Value;
        if (value.Length < 5)
        {
            throw new ArgumentException($"Invalid CID '{value}' — too short to shard.", nameof(cid));
        }
        // value[0] is the multibase prefix; shard on value[1..3] and value[3..5].
        return Path.Combine(_root, value.Substring(1, 2), value.Substring(3, 2), value);
    }
}
