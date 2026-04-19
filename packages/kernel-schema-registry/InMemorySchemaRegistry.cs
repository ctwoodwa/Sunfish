using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;

using Json.Schema;

using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Blobs;
using Sunfish.Foundation.Crypto;

namespace Sunfish.Kernel.Schema;

/// <summary>
/// In-memory default backend for <see cref="ISchemaRegistry"/>.
/// Schemas are kept in a <see cref="ConcurrentDictionary{TKey,TValue}"/> keyed
/// by <see cref="SchemaId"/>; canonical-JSON bytes are additionally written to
/// the injected <see cref="IBlobStore"/> so federation peers can fetch the
/// schema by CID through any blob-store backend.
/// </summary>
/// <remarks>
/// <para>
/// This backend is not persistent — process restarts clear the dictionary.
/// Persistent backends (spec §3.4 follow-ups) can implement the same interface
/// over Postgres, Confluent Schema Registry, or Apicurio without touching
/// consumers.
/// </para>
/// <para>
/// Validation uses JsonSchema.Net (<c>Json.Schema</c> namespace) in
/// draft 2020-12 mode. The parsed <see cref="JsonSchema"/> is cached alongside
/// the <see cref="Schema"/> record to avoid re-parsing on every
/// <see cref="ValidateAsync"/> call.
/// </para>
/// </remarks>
public sealed class InMemorySchemaRegistry : ISchemaRegistry
{
    private readonly IBlobStore _blobs;
    private readonly ConcurrentDictionary<SchemaId, Entry> _schemas = new();

    /// <summary>Creates a new <see cref="InMemorySchemaRegistry"/> that side-stores canonical schema bytes in <paramref name="blobs"/>.</summary>
    public InMemorySchemaRegistry(IBlobStore blobs)
    {
        ArgumentNullException.ThrowIfNull(blobs);
        _blobs = blobs;
    }

    /// <inheritdoc />
    public ValueTask<Schema?> GetAsync(SchemaId id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return _schemas.TryGetValue(id, out var entry)
            ? new ValueTask<Schema?>(entry.Schema)
            : new ValueTask<Schema?>((Schema?)null);
    }

    /// <inheritdoc />
    public async ValueTask<Schema> RegisterAsync(
        string jsonSchemaText,
        IReadOnlyList<SchemaId>? parents = null,
        IReadOnlyList<string>? tags = null,
        int? blobThreshold = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(jsonSchemaText);

        if (blobThreshold.HasValue && blobThreshold.Value <= 0)
        {
            throw new ArgumentException(
                $"blobThreshold must be a positive integer; got {blobThreshold.Value}.",
                nameof(blobThreshold));
        }

        // 1. Canonicalize the schema text before hashing so that two clients
        //    who register logically-equivalent schemas with different
        //    whitespace / key order produce the same CID (federation parity).
        byte[] canonicalBytes;
        try
        {
            var node = JsonNode.Parse(jsonSchemaText)
                ?? throw new InvalidSchemaException("JSON Schema text parsed to null.");
            canonicalBytes = CanonicalJson.Serialize(node);
        }
        catch (JsonException ex)
        {
            throw new InvalidSchemaException(
                $"JSON Schema text is not valid JSON: {ex.Message}", ex);
        }

        // 2. Parse + validate with JsonSchema.Net so we fail at register-time
        //    on malformed schema documents rather than on the first payload.
        JsonSchema parsedSchema;
        try
        {
            parsedSchema = JsonSchema.FromText(jsonSchemaText);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidSchemaException(
                $"JSON Schema text is not a valid JSON Schema: {ex.Message}", ex);
        }

        // 3. Content-address the canonical bytes and side-store in the blob layer.
        var cid = Cid.FromBytes(canonicalBytes);
        await _blobs.PutAsync(canonicalBytes, ct).ConfigureAwait(false);

        // 4. Build the schema record with a self-identifying id of the form
        //    "schema:{cid}". The id embeds the CID so schemas are addressable
        //    by content across federation boundaries without a side lookup.
        var id = new SchemaId($"schema:{cid.Value}");
        var schema = new Schema(
            Id: id,
            JsonSchemaText: jsonSchemaText,
            ParentSchemas: parents ?? Array.Empty<SchemaId>(),
            Migrations: Array.Empty<Migration>(),
            Tags: tags ?? Array.Empty<string>(),
            ContentAddress: cid,
            BlobThreshold: blobThreshold);

        // Register-or-return-existing so the operation is idempotent.
        var entry = _schemas.GetOrAdd(id, _ => new Entry(schema, parsedSchema));
        return entry.Schema;
    }

    /// <inheritdoc />
    public ValueTask<SchemaValidationResult> ValidateAsync(
        SchemaId id,
        ReadOnlyMemory<byte> documentBytes,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!_schemas.TryGetValue(id, out var entry))
        {
            throw new SchemaNotFoundException(
                $"Schema '{id.Value}' is not registered with this ISchemaRegistry instance.");
        }

        using var document = JsonDocument.Parse(documentBytes);

        // OutputFormat.List gives us a flat list of sub-results, each with its
        // own InstanceLocation (JSON Pointer) — exactly the shape we map to
        // SchemaValidationError. Hierarchical would nest; Flag would collapse
        // to a single pass/fail bit.
        var options = new EvaluationOptions
        {
            OutputFormat = OutputFormat.List,
        };

        var results = entry.ParsedSchema.Evaluate(document.RootElement, options);

        if (results.IsValid)
        {
            return new ValueTask<SchemaValidationResult>(
                new SchemaValidationResult(true, Array.Empty<SchemaValidationError>()));
        }

        var errors = CollectErrors(results);
        return new ValueTask<SchemaValidationResult>(
            new SchemaValidationResult(false, errors));
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<Schema> ListAsync(
        string? tagFilter = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var entry in _schemas.Values)
        {
            ct.ThrowIfCancellationRequested();
            if (tagFilter is null || entry.Schema.Tags.Contains(tagFilter))
            {
                yield return entry.Schema;
            }
        }

        // Satisfy the async-iterator contract without adding real asynchrony —
        // the in-memory dictionary scan is pure sync but the method signature
        // must expose IAsyncEnumerable for interface conformance.
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask<MigrationPlan> PlanMigrationAsync(SchemaId from, SchemaId to, CancellationToken ct = default)
    {
        throw new NotSupportedException(
            "Migration half of ISchemaRegistry is deferred — see gap analysis G2 follow-up.");
    }

    /// <inheritdoc />
    public ValueTask<ReadOnlyMemory<byte>> MigrateAsync(SchemaId from, SchemaId to, ReadOnlyMemory<byte> document, CancellationToken ct = default)
    {
        throw new NotSupportedException(
            "Migration half of ISchemaRegistry is deferred — see gap analysis G2 follow-up.");
    }

    /// <summary>
    /// Flattens a JsonSchema.Net <see cref="EvaluationResults"/> tree into the
    /// Sunfish <see cref="SchemaValidationError"/> list. The List-format result
    /// has a top-level <see cref="EvaluationResults.Details"/> collection; we
    /// walk it and emit one Sunfish error per keyword entry in the node's
    /// <see cref="EvaluationResults.Errors"/> dictionary.
    /// </summary>
    private static IReadOnlyList<SchemaValidationError> CollectErrors(EvaluationResults results)
    {
        var collected = new List<SchemaValidationError>();
        Walk(results, collected);
        if (collected.Count == 0)
        {
            // Defensive: IsValid was false but we found no errored leaves.
            // Surface a single aggregate error rather than silently returning
            // an empty list on an invalid result.
            collected.Add(new SchemaValidationError(
                JsonPointer: results.InstanceLocation.ToString(),
                Message: "Validation failed (no keyword-level details reported)."));
        }
        return collected;
    }

    private static void Walk(EvaluationResults node, List<SchemaValidationError> sink)
    {
        // Errors is a Dictionary<string, string>? on EvaluationResults —
        // keyed by the failing keyword (e.g. "type", "required", "minimum")
        // and valued with a human-readable message produced by the validator.
        if (node.Errors is { Count: > 0 } keywordErrors)
        {
            var pointer = node.InstanceLocation.ToString();
            foreach (var kvp in keywordErrors)
            {
                // Prefix with the keyword so the surface of the Sunfish error
                // mirrors the validator's reasoning rather than stripping context.
                sink.Add(new SchemaValidationError(
                    JsonPointer: pointer,
                    Message: $"{kvp.Key}: {kvp.Value}"));
            }
        }

        // Details is nullable on EvaluationResults — walk only when present.
        if (node.Details is { Count: > 0 } children)
        {
            foreach (var child in children)
            {
                Walk(child, sink);
            }
        }
    }

    /// <summary>Pairs a <see cref="Schema"/> with its parsed <see cref="JsonSchema"/> for validation reuse.</summary>
    private sealed record Entry(Schema Schema, JsonSchema ParsedSchema);
}
