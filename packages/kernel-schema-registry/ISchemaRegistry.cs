using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Kernel.Schema;

/// <summary>
/// Kernel primitive §3.4 — schema registry. Registers content-addressed JSON
/// Schema documents, validates arbitrary JSON payloads against them, and
/// reserves the contract shape for future schema-to-schema migration.
/// </summary>
/// <remarks>
/// <para>
/// This is the shipping contract for gap <b>G2 (validation half, Option B)</b>
/// in the platform gap analysis
/// (<c>icm/01_discovery/output/sunfish-gap-analysis-2026-04-18.md</c>).
/// The namespace <c>Sunfish.Kernel.Schema</c> is preserved from the G1 reserved
/// stub; the assembly that owns the type moved from <c>Sunfish.Kernel</c> to
/// <c>Sunfish.Kernel.SchemaRegistry</c> when the stub was promoted.
/// </para>
/// <para>
/// Migration path members (<see cref="PlanMigrationAsync"/> /
/// <see cref="MigrateAsync"/>) are defined on the interface now so downstream
/// consumers can bind against the final surface, but their implementations in
/// this PR throw <see cref="NotSupportedException"/>. The G2 follow-up will
/// land jsonata-driven migration against the same signatures.
/// </para>
/// </remarks>
public interface ISchemaRegistry
{
    /// <summary>
    /// Loads a previously-registered schema by id. Returns <see langword="null"/>
    /// when the id is unknown to this registry instance.
    /// </summary>
    /// <param name="id">Schema id, typically produced by <see cref="RegisterAsync"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<Schema?> GetAsync(SchemaId id, CancellationToken ct = default);

    /// <summary>
    /// Parses, canonicalizes, content-addresses, blob-stores, and registers the
    /// supplied JSON Schema text. Registering the same text twice returns a
    /// <see cref="Schema"/> with the same <see cref="Schema.ContentAddress"/>
    /// and <see cref="Schema.Id"/> — the operation is idempotent.
    /// </summary>
    /// <param name="jsonSchemaText">
    /// JSON Schema draft 2020-12 document text. Whitespace and key order do not
    /// affect the resulting CID — the document is canonicalized before hashing.
    /// </param>
    /// <param name="parents">Optional ordered list of parent schema ids.</param>
    /// <param name="tags">Optional caller-supplied tags (case-sensitive).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidSchemaException">
    /// The supplied text is not a syntactically valid JSON Schema document.
    /// </exception>
    ValueTask<Schema> RegisterAsync(
        string jsonSchemaText,
        IReadOnlyList<SchemaId>? parents = null,
        IReadOnlyList<string>? tags = null,
        CancellationToken ct = default);

    /// <summary>
    /// Validates <paramref name="documentBytes"/> against the schema previously
    /// registered under <paramref name="id"/>. Returns a
    /// <see cref="SchemaValidationResult"/> capturing success or the list of
    /// failing keyword evaluations with their JSON-Pointer locations.
    /// </summary>
    /// <param name="id">Schema id from a prior <see cref="RegisterAsync"/> call.</param>
    /// <param name="documentBytes">UTF-8 JSON bytes to validate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="SchemaNotFoundException">
    /// <paramref name="id"/> does not identify a schema in this registry instance.
    /// </exception>
    ValueTask<SchemaValidationResult> ValidateAsync(
        SchemaId id,
        ReadOnlyMemory<byte> documentBytes,
        CancellationToken ct = default);

    /// <summary>
    /// Enumerates registered schemas. When <paramref name="tagFilter"/> is
    /// non-null, only schemas whose <see cref="Schema.Tags"/> contains that
    /// exact (case-sensitive) string are yielded.
    /// </summary>
    /// <param name="tagFilter">Optional tag to filter by. Case-sensitive.</param>
    /// <param name="ct">Cancellation token.</param>
    IAsyncEnumerable<Schema> ListAsync(
        string? tagFilter = null,
        CancellationToken ct = default);

    /// <summary>
    /// Plans the migration steps required to carry a document from the schema
    /// identified by <paramref name="from"/> to the one identified by
    /// <paramref name="to"/>. <b>Not implemented in this PR</b> — throws
    /// <see cref="NotSupportedException"/>. Reserved for the G2 follow-up that
    /// lands jsonata-driven migration.
    /// </summary>
    ValueTask<MigrationPlan> PlanMigrationAsync(SchemaId from, SchemaId to, CancellationToken ct = default);

    /// <summary>
    /// Executes a registered migration chain against <paramref name="document"/>
    /// to produce a payload conforming to the <paramref name="to"/> schema.
    /// <b>Not implemented in this PR</b> — throws
    /// <see cref="NotSupportedException"/>. Reserved for the G2 follow-up.
    /// </summary>
    ValueTask<ReadOnlyMemory<byte>> MigrateAsync(SchemaId from, SchemaId to, ReadOnlyMemory<byte> document, CancellationToken ct = default);
}

/// <summary>
/// Outcome of <see cref="ISchemaRegistry.ValidateAsync"/>. When
/// <see cref="IsValid"/> is <see langword="true"/>, <see cref="Errors"/> is
/// empty; otherwise <see cref="Errors"/> is a flat list of the failing keyword
/// evaluations.
/// </summary>
public sealed record SchemaValidationResult(bool IsValid, IReadOnlyList<SchemaValidationError> Errors);

/// <summary>
/// A single validation failure. <see cref="JsonPointer"/> points into the
/// failing document node (RFC 6901 encoding); <see cref="Message"/> is a
/// human-readable description produced by the underlying validator.
/// </summary>
public sealed record SchemaValidationError(string JsonPointer, string Message);

/// <summary>
/// Ordered list of migration steps produced by
/// <see cref="ISchemaRegistry.PlanMigrationAsync"/>. Reserved for the G2
/// follow-up; no plan is produced in this PR.
/// </summary>
public sealed record MigrationPlan(SchemaId From, SchemaId To, IReadOnlyList<Migration> Steps);
