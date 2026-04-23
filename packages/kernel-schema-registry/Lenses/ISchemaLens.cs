namespace Sunfish.Kernel.SchemaRegistry.Lenses;

/// <summary>
/// Bidirectional schema lens — transforms events between two adjacent schema versions
/// in both directions. Paper §7.3: <i>"declarative, composable transformation functions
/// between schema versions stored in the CRDT document. Lenses form a version graph;
/// migrations between distant versions traverse the shortest path."</i>
/// </summary>
/// <remarks>
/// <para>
/// A lens is the structural-change counterpart of an upcaster (see
/// <see cref="Upcasters.IUpcaster"/>). Upcasters are forward-only — appropriate for the
/// additive cases in paper §7.2 where no information is lost. Lenses are richer: they
/// support renames, type changes, and structural reorganizations, and they can be
/// composed into a version graph traversed in either direction.
/// </para>
/// <para>
/// <b>Lossy backward transforms:</b> when a lens adds a new required field going
/// forward, the backward transform cannot recover that field from older events. Lenses
/// should return a sensible default (e.g. the field's declared default value or
/// <c>null</c>) rather than throwing. The lens author documents which fields are lossy
/// in the backward direction so callers can decide whether to invoke
/// <see cref="BackwardTransform"/> at all.
/// </para>
/// <para>
/// <b>Purity:</b> lens transforms MUST be pure functions — deterministic, no I/O, no
/// shared state. The <see cref="LensGraph"/> will call them during replay, compaction,
/// and rehydration; non-determinism breaks reproducibility.
/// </para>
/// </remarks>
public interface ISchemaLens
{
    /// <summary>The event type this lens applies to (e.g. <c>record.updated</c>). Lenses are scoped per event type.</summary>
    string EventType { get; }

    /// <summary>Older-schema version identifier (source for <see cref="ForwardTransform"/>).</summary>
    string FromVersion { get; }

    /// <summary>Newer-schema version identifier (target for <see cref="ForwardTransform"/>).</summary>
    string ToVersion { get; }

    /// <summary>
    /// Upcast: transform an older-version event to the newer-version shape.
    /// </summary>
    /// <param name="olderEvent">An event conforming to <see cref="FromVersion"/>. The lens does not verify the claim — callers route events to the correct lens via <see cref="EventType"/> / <see cref="FromVersion"/> matching.</param>
    /// <returns>The same event in <see cref="ToVersion"/> shape. Callers treat the return value as opaque and rely on the lens author's contract.</returns>
    object ForwardTransform(object olderEvent);

    /// <summary>
    /// Downcast: transform a newer-version event back to the older-version shape.
    /// Lossy fields return the lens-author's chosen default (typically the field's
    /// declared default or <see langword="null"/>).
    /// </summary>
    /// <param name="newerEvent">An event conforming to <see cref="ToVersion"/>.</param>
    /// <returns>The same event in <see cref="FromVersion"/> shape, with lossy fields defaulted.</returns>
    object BackwardTransform(object newerEvent);
}
