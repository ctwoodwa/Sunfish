using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Assets.Versions;

namespace Sunfish.Foundation.Assets.Entities;

/// <summary>
/// Zero-dependency in-memory <see cref="IEntityStore"/>.
/// </summary>
/// <remarks>
/// Backed by <see cref="InMemoryAssetStorage"/>; paired with <see cref="InMemoryVersionStore"/>
/// over the same storage container so the materialized current-body cache and the append-only
/// version log stay in sync by construction (plan D-VERSION-STORE-SHAPE).
/// </remarks>
public sealed class InMemoryEntityStore : IEntityStore
{
    private readonly InMemoryAssetStorage _storage;
    private readonly IEntityValidator _validator;
    private readonly IVersionObserver _observer;

    /// <summary>Creates an in-memory entity store backed by the given shared storage.</summary>
    public InMemoryEntityStore(
        InMemoryAssetStorage storage,
        IEntityValidator? validator = null,
        IVersionObserver? observer = null)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _validator = validator ?? NullEntityValidator.Instance;
        _observer = observer ?? NullVersionObserver.Instance;
    }

    /// <inheritdoc />
    public Task<Entity?> GetAsync(EntityId id, VersionSelector version = default, CancellationToken ct = default)
    {
        if (!_storage.Entities.TryGetValue(id, out var record))
            return Task.FromResult<Entity?>(null);

        if (!_storage.Versions.TryGetValue(id, out var history))
            return Task.FromResult<Entity?>(null);

        Versions.Version? selected;
        if (version.ExplicitSequence is { } seq)
        {
            selected = history.FirstOrDefault(v => v.Id.Sequence == seq);
            if (selected is null) return Task.FromResult<Entity?>(null);
        }
        else if (version.AsOf is { } at)
        {
            selected = history
                .Where(v => v.ValidFrom <= at && (v.ValidTo is null || at < v.ValidTo))
                .OrderByDescending(v => v.Id.Sequence)
                .FirstOrDefault();
            if (selected is null) return Task.FromResult<Entity?>(null);
            if (IsTombstone(selected)) return Task.FromResult<Entity?>(null);
        }
        else
        {
            if (record.DeletedAt is not null) return Task.FromResult<Entity?>(null);
            selected = history[^1];
        }

        var entity = new Entity(
            record.Id,
            record.Schema,
            record.Tenant,
            selected.Id,
            CloneBody(selected.Body),
            record.CreatedAt,
            record.UpdatedAt,
            record.DeletedAt);

        return Task.FromResult<Entity?>(entity);
    }

    /// <inheritdoc />
    public async Task<EntityId> CreateAsync(SchemaId schema, JsonDocument body, CreateOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(options);
        await _validator.ValidateAsync(schema, body, ct).ConfigureAwait(false);

        var id = DeriveEntityId(schema, options);
        var validFrom = options.ValidFrom ?? DateTimeOffset.UtcNow;
        var canonicalBody = JsonCanonicalizer.ToCanonicalString(body);

        var lockObj = _storage.LockFor(id);
        lock (lockObj)
        {
            if (_storage.Entities.TryGetValue(id, out var existing))
            {
                // Idempotent: same body → return same id.
                if (existing.BodyJson == canonicalBody)
                    return id;
                throw new IdempotencyConflictException(
                    $"Entity '{id}' already exists with a different body; refusing to overwrite via CreateAsync.");
            }

            var initialBody = CloneBody(body);
            var hash = HashVersion(parentHash: null, canonicalBody: canonicalBody, validFrom: validFrom);
            var versionId = new VersionId(id, 1, hash);
            var version = new Versions.Version(
                Id: versionId,
                ParentId: null,
                Body: initialBody,
                ValidFrom: validFrom,
                ValidTo: null,
                Author: options.Issuer,
                Signature: null,
                Diff: null);

            var history = new List<Versions.Version> { version };
            _storage.Versions[id] = history;

            var record = new EntityRecord
            {
                Id = id,
                Schema = schema,
                Tenant = options.Tenant,
                CurrentVersion = versionId,
                BodyJson = canonicalBody,
                CreatedAt = validFrom,
                UpdatedAt = validFrom,
                DeletedAt = null,
                CreationNonce = options.Nonce,
                CreationIssuer = options.Issuer,
            };
            _storage.Entities[id] = record;

            _ = _observer.OnVersionAppendedAsync(id, version, ct);
            return id;
        }
    }

    /// <inheritdoc />
    public async Task<VersionId> UpdateAsync(EntityId id, JsonDocument newBody, UpdateOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(newBody);
        ArgumentNullException.ThrowIfNull(options);

        if (!_storage.Entities.TryGetValue(id, out var record))
            throw new InvalidOperationException($"Entity '{id}' not found.");

        await _validator.ValidateAsync(record.Schema, newBody, ct).ConfigureAwait(false);

        var canonicalBody = JsonCanonicalizer.ToCanonicalString(newBody);
        var validFrom = options.ValidFrom ?? DateTimeOffset.UtcNow;

        Versions.Version appended;
        var lockObj = _storage.LockFor(id);
        lock (lockObj)
        {
            var history = _storage.Versions[id];
            var tail = history[^1];

            if (options.ExpectedVersion is { } expected && tail.Id != expected)
                throw new ConcurrencyException(
                    $"Entity '{id}' has advanced beyond expected version {expected}; current tip is {tail.Id}.");

            // Close out the previous version's validity so ranges are contiguous.
            var closed = tail with { ValidTo = validFrom };
            history[^1] = closed;

            var parentHash = closed.Id.Hash;
            var hash = HashVersion(parentHash: parentHash, canonicalBody: canonicalBody, validFrom: validFrom);
            var versionId = new VersionId(id, closed.Id.Sequence + 1, hash);
            appended = new Versions.Version(
                Id: versionId,
                ParentId: closed.Id,
                Body: CloneBody(newBody),
                ValidFrom: validFrom,
                ValidTo: null,
                Author: options.Actor,
                Signature: null,
                Diff: null);
            history.Add(appended);

            record.CurrentVersion = versionId;
            record.BodyJson = canonicalBody;
            record.UpdatedAt = validFrom;
            record.DeletedAt = null;
        }

        await _observer.OnVersionAppendedAsync(id, appended, ct).ConfigureAwait(false);
        return appended.Id;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(EntityId id, DeleteOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!_storage.Entities.TryGetValue(id, out var record))
            throw new InvalidOperationException($"Entity '{id}' not found.");

        var validFrom = options.ValidFrom ?? DateTimeOffset.UtcNow;
        using var tombstoneBody = JsonDocument.Parse("{}");

        Versions.Version appended;
        var lockObj = _storage.LockFor(id);
        lock (lockObj)
        {
            var history = _storage.Versions[id];
            var tail = history[^1];
            var closed = tail with { ValidTo = validFrom };
            history[^1] = closed;

            var canonicalBody = JsonCanonicalizer.ToCanonicalString(tombstoneBody);
            var parentHash = closed.Id.Hash;
            var hash = HashVersion(parentHash: parentHash, canonicalBody: canonicalBody, validFrom: validFrom);
            var versionId = new VersionId(id, closed.Id.Sequence + 1, hash);
            appended = new Versions.Version(
                Id: versionId,
                ParentId: closed.Id,
                Body: JsonDocument.Parse("{}"),
                ValidFrom: validFrom,
                ValidTo: null,
                Author: options.Actor,
                Signature: null,
                Diff: null);
            history.Add(appended);

            record.CurrentVersion = versionId;
            record.BodyJson = canonicalBody;
            record.UpdatedAt = validFrom;
            record.DeletedAt = validFrom;
        }

        await _observer.OnVersionAppendedAsync(id, appended, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<Entity> QueryAsync(EntityQuery query, [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var snapshot = _storage.Entities.Values.ToList();
        int emitted = 0;
        foreach (var record in snapshot)
        {
            ct.ThrowIfCancellationRequested();

            if (query.Schema is { } schema && !string.Equals(record.Schema.Value, schema.Value, StringComparison.Ordinal))
                continue;
            if (query.Tenant is { } tenant && !string.Equals(record.Tenant.Value, tenant.Value, StringComparison.Ordinal))
                continue;

            if (!query.IncludeDeleted)
            {
                if (query.AsOf is { } asOf)
                {
                    if (record.DeletedAt is { } deletedAt && deletedAt <= asOf)
                        continue;
                }
                else if (record.DeletedAt is not null)
                {
                    continue;
                }
            }

            if (!_storage.Versions.TryGetValue(record.Id, out var history))
                continue;

            Versions.Version? picked;
            if (query.AsOf is { } at)
            {
                picked = history
                    .Where(v => v.ValidFrom <= at && (v.ValidTo is null || at < v.ValidTo))
                    .OrderByDescending(v => v.Id.Sequence)
                    .FirstOrDefault();
                if (picked is null) continue;
            }
            else
            {
                picked = history[^1];
            }

            yield return new Entity(
                record.Id,
                record.Schema,
                record.Tenant,
                picked.Id,
                CloneBody(picked.Body),
                record.CreatedAt,
                record.UpdatedAt,
                record.DeletedAt);

            emitted++;
            if (query.Limit is { } limit && emitted >= limit) yield break;
            await Task.Yield();
        }
    }

    private static bool IsTombstone(Versions.Version version)
    {
        using var doc = JsonDocument.Parse(JsonCanonicalizer.ToCanonicalBytes(version.Body));
        return doc.RootElement.ValueKind == JsonValueKind.Object && !doc.RootElement.EnumerateObject().Any();
    }

    private static JsonDocument CloneBody(JsonDocument body)
        => JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(body.RootElement));

    internal static EntityId DeriveEntityId(SchemaId schema, CreateOptions options)
    {
        if (options.ExplicitLocalPart is { Length: > 0 } explicitLocal)
            return new EntityId(options.Scheme, options.Authority, explicitLocal);

        var input = Encoding.UTF8.GetBytes($"{schema.Value}|{options.Authority}|{options.Nonce}|{options.Issuer.Value}");
        Span<byte> digest = stackalloc byte[32];
        SHA256.HashData(input, digest);
        // Take 16 bytes (128-bit) and base32-encode via the existing Blobs Base32 shape.
        var local = Base32Lower.Encode(digest[..16]);
        return new EntityId(options.Scheme, options.Authority, local);
    }

    private static string HashVersion(string? parentHash, string canonicalBody, DateTimeOffset validFrom)
    {
        var prefix = parentHash ?? string.Empty;
        var input = Encoding.UTF8.GetBytes($"{prefix}|{canonicalBody}|{validFrom:O}");
        Span<byte> digest = stackalloc byte[32];
        SHA256.HashData(input, digest);
        return Convert.ToHexStringLower(digest);
    }
}

/// <summary>RFC 4648 base32 lowercase, no padding. Internal duplicate to avoid Blobs coupling.</summary>
internal static class Base32Lower
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz234567";

    public static string Encode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty) return string.Empty;

        var outputLength = (bytes.Length * 8 + 4) / 5;
        var output = new char[outputLength];
        int buffer = 0, bitsLeft = 0, outputIndex = 0;

        foreach (var b in bytes)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                output[outputIndex++] = Alphabet[(buffer >> (bitsLeft - 5)) & 0x1F];
                bitsLeft -= 5;
            }
        }

        if (bitsLeft > 0)
            output[outputIndex] = Alphabet[(buffer << (5 - bitsLeft)) & 0x1F];

        return new string(output);
    }
}
