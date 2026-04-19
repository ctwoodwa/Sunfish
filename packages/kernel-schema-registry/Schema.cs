using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Blobs;

namespace Sunfish.Kernel.Schema;

/// <summary>
/// Content-addressed JSON Schema registered in the kernel schema registry
/// (spec §3.4). The tuple of <see cref="JsonSchemaText"/> + canonicalization
/// rules uniquely determines <see cref="ContentAddress"/>; <see cref="Id"/>
/// embeds the same CID so the schema is self-identifying across federated peers.
/// </summary>
/// <param name="Id">
/// Schema identifier of the form <c>schema:{cid}</c> where <c>{cid}</c> is the
/// base32-lowercase CID v1 / raw / SHA-256 encoding of the canonical JSON bytes
/// of <see cref="JsonSchemaText"/>.
/// </param>
/// <param name="JsonSchemaText">
/// Raw JSON Schema document text as supplied to
/// <see cref="ISchemaRegistry.RegisterAsync"/>. Consumers re-parse this string
/// at validation time; the text is the source of truth, not any cached parse.
/// </param>
/// <param name="ParentSchemas">
/// Optional ordered list of parent schema ids this schema extends or refines.
/// Semantics of "parent" are registry-wide conventions (spec §3.4); the registry
/// itself stores the relation without interpreting it.
/// </param>
/// <param name="Migrations">
/// Declared migration steps from ancestor versions to this schema. Stored for
/// federation and reference in the validation path; NOT executed in this PR.
/// See <see cref="ISchemaRegistry.MigrateAsync"/> for the deferred behaviour.
/// </param>
/// <param name="Tags">
/// Caller-supplied tags (case-sensitive free-form strings). Used by
/// <see cref="ISchemaRegistry.ListAsync"/> for basic membership filtering.
/// </param>
/// <param name="ContentAddress">
/// CID of the canonical-JSON UTF-8 bytes of <see cref="JsonSchemaText"/>. Equal
/// across federation peers for logically-equivalent schemas.
/// </param>
public sealed record Schema(
    SchemaId Id,
    string JsonSchemaText,
    IReadOnlyList<SchemaId> ParentSchemas,
    IReadOnlyList<Migration> Migrations,
    IReadOnlyList<string> Tags,
    Cid ContentAddress);

/// <summary>
/// Declarative migration step from one schema version to another. Stored in the
/// registry but NOT executed in this PR — the migration path is deferred to the
/// G2 follow-up (<c>icm/01_discovery/output/sunfish-gap-analysis-2026-04-18.md</c>).
/// </summary>
/// <param name="FromVersion">Source schema version identifier.</param>
/// <param name="ToVersion">Target schema version identifier.</param>
/// <param name="JsonataExpression">
/// JSONata expression that transforms a document conforming to
/// <paramref name="FromVersion"/> into one conforming to <paramref name="ToVersion"/>.
/// Opaque to the registry in this PR; the jsonata engine integration lands with
/// the migration follow-up.
/// </param>
public sealed record Migration(
    string FromVersion,
    string ToVersion,
    string JsonataExpression);
