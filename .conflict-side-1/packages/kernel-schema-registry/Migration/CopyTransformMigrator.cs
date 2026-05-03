using Sunfish.Kernel.Events;
using Sunfish.Kernel.SchemaRegistry.Lenses;

namespace Sunfish.Kernel.SchemaRegistry.Migration;

/// <summary>
/// Paper §7.4's <i>"background copy-transform job"</i> — replays an existing event log
/// into a new one while applying lenses to reach a target schema version.
/// </summary>
/// <remarks>
/// <para>
/// Paper §7.4: <i>"A background copy-transform job reads the existing log, applies
/// lenses and upcasters, and writes to a new epoch stream. Nodes cut over to the new
/// epoch as they upgrade."</i> This class ships the lens half; an upcaster-only variant
/// is trivially derivable by passing an empty <see cref="LensGraph"/> and running a
/// separate pre-pass with <see cref="Upcasters.UpcasterChain"/>.
/// </para>
/// <para>
/// <b>Source-version discovery:</b> the migrator reads each source event's schema
/// version from the <see cref="KernelEvent.Payload"/> under the key
/// <see cref="SchemaVersionPayloadKey"/>. Events without that key are assumed to be at
/// <see cref="UnknownSchemaVersion"/> — they are copied through unchanged and a warning
/// is appended to the migration result.
/// </para>
/// <para>
/// <b>Events with no lens path:</b> are copied through unchanged with a warning
/// appended, so callers get a complete new stream plus an explicit list of events that
/// need another migration strategy (typically a hand-written upcaster or a drop).
/// </para>
/// </remarks>
public class CopyTransformMigrator
{
    /// <summary>Payload key the migrator reads to discover each event's schema version.</summary>
    public const string SchemaVersionPayloadKey = "_schemaVersion";

    /// <summary>Sentinel value used when an event payload does not declare a schema version.</summary>
    public const string UnknownSchemaVersion = "unknown";

    /// <summary>
    /// Replay <paramref name="sourceLog"/>, apply lenses from
    /// <paramref name="lensGraph"/> to reach <paramref name="targetSchemaVersion"/>, and
    /// append results to <paramref name="targetLog"/>.
    /// </summary>
    /// <param name="sourceLog">Log to read from (not modified).</param>
    /// <param name="targetLog">Log to append transformed events into.</param>
    /// <param name="lensGraph">Graph of lenses used for transformation.</param>
    /// <param name="targetSchemaVersion">The schema version every written event should declare.</param>
    /// <param name="ct">Cancellation token.</param>
    public virtual async Task<MigrationResult> MigrateAsync(
        IEventLog sourceLog,
        IEventLog targetLog,
        LensGraph lensGraph,
        string targetSchemaVersion,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sourceLog);
        ArgumentNullException.ThrowIfNull(targetLog);
        ArgumentNullException.ThrowIfNull(lensGraph);
        ArgumentException.ThrowIfNullOrEmpty(targetSchemaVersion);

        ulong read = 0;
        ulong written = 0;
        ulong dropped = 0;
        var warnings = new List<string>();

        await foreach (var entry in sourceLog.ReadAfterAsync(0UL, ct).ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();
            read++;

            var source = entry.Event;
            var sourceVersion = ReadSchemaVersion(source);

            if (sourceVersion == targetSchemaVersion)
            {
                // Already at target — copy through.
                await targetLog.AppendAsync(source, ct).ConfigureAwait(false);
                written++;
                continue;
            }

            if (sourceVersion == UnknownSchemaVersion)
            {
                warnings.Add(
                    $"seq={entry.Sequence} kind={source.Kind} had no '{SchemaVersionPayloadKey}' payload key; copied through unchanged.");
                await targetLog.AppendAsync(source, ct).ConfigureAwait(false);
                written++;
                continue;
            }

            if (!lensGraph.HasPath(source.Kind, sourceVersion, targetSchemaVersion))
            {
                warnings.Add(
                    $"seq={entry.Sequence} kind={source.Kind} version {sourceVersion} has no lens path to {targetSchemaVersion}; copied through unchanged.");
                await targetLog.AppendAsync(source, ct).ConfigureAwait(false);
                written++;
                continue;
            }

            var transformedPayload = lensGraph.Transform(source.Kind, (object)source.Payload, sourceVersion, targetSchemaVersion);
            if (transformedPayload is not IReadOnlyDictionary<string, object?> transformedMap)
            {
                warnings.Add(
                    $"seq={entry.Sequence} kind={source.Kind} lens returned non-map payload ({transformedPayload?.GetType().Name ?? "null"}); event dropped.");
                dropped++;
                continue;
            }

            // Stamp the target schema version on the transformed payload so downstream
            // rehydrators can short-circuit the migrator on a second pass.
            var stamped = new Dictionary<string, object?>(transformedMap)
            {
                [SchemaVersionPayloadKey] = targetSchemaVersion,
            };

            var rewritten = source with { Payload = stamped };
            await targetLog.AppendAsync(rewritten, ct).ConfigureAwait(false);
            written++;
        }

        return new MigrationResult(read, written, dropped, warnings);
    }

    private static string ReadSchemaVersion(KernelEvent evt)
    {
        if (evt.Payload.TryGetValue(SchemaVersionPayloadKey, out var value) && value is string s && !string.IsNullOrEmpty(s))
        {
            return s;
        }
        return UnknownSchemaVersion;
    }
}

/// <summary>
/// Outcome of <see cref="CopyTransformMigrator.MigrateAsync"/>.
/// </summary>
/// <param name="EventsRead">Number of events read from the source log.</param>
/// <param name="EventsWritten">Number of events appended to the target log.</param>
/// <param name="EventsDropped">Number of events intentionally not written (bad lens output, deliberate drops, etc.).</param>
/// <param name="Warnings">Human-readable warnings — typically one per copied-through-unchanged event.</param>
public sealed record MigrationResult(
    ulong EventsRead,
    ulong EventsWritten,
    ulong EventsDropped,
    IReadOnlyList<string> Warnings);
