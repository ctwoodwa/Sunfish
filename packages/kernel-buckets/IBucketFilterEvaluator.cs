namespace Sunfish.Kernel.Buckets;

/// <summary>
/// Evaluates a bucket filter expression against a record's context. Paper §10.2 uses a small
/// expression grammar for per-record filtering (<c>record.team_id = peer.team_id</c>).
/// </summary>
/// <remarks>
/// <para>
/// The contract is deliberately minimal: the bucket engine only needs boolean yes/no per record.
/// No DSL was mandated by the paper, so this wave ships a narrow grammar
/// (see <see cref="SimpleBucketFilterEvaluator"/>) that covers the paper's own examples.
/// Swap in a full expression engine (e.g. Jint, Sprache) in a later wave if needed.
/// </para>
/// </remarks>
public interface IBucketFilterEvaluator
{
    /// <summary>
    /// Evaluate <paramref name="filter"/> against <paramref name="context"/>. A null or empty
    /// filter is treated as "always true" — the whole record-type set passes through.
    /// </summary>
    /// <param name="filter">The filter expression (<see cref="BucketDefinition.Filter"/>).</param>
    /// <param name="context">
    /// Flat key/value bag representing the record and peer fields referenced in the filter.
    /// Keys use dotted notation (<c>record.team_id</c>, <c>peer.team_id</c>, <c>project.archived</c>).
    /// </param>
    /// <returns>Whether the record passes the filter.</returns>
    /// <exception cref="BucketFilterSyntaxException">The filter expression is syntactically invalid.</exception>
    bool Evaluate(string? filter, IReadOnlyDictionary<string, object?> context);
}

/// <summary>Thrown when a filter expression is syntactically invalid.</summary>
public sealed class BucketFilterSyntaxException : Exception
{
    /// <summary>Create a new <see cref="BucketFilterSyntaxException"/>.</summary>
    public BucketFilterSyntaxException(string message) : base(message) { }
}
