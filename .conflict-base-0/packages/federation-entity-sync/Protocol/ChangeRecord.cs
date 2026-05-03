using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Federation.EntitySync.Protocol;

/// <summary>
/// A single opaque change record in the entity delta-sync protocol. The diff payload is carried as
/// UTF-8 / binary bytes — the federation layer does not interpret it; consumers are responsible for
/// decoding and CRDT-merging <see cref="Diff"/> into their own entity state.
/// </summary>
/// <remarks>
/// <para>
/// Each change records a parent link (<see cref="ParentVersionId"/>) forming a DAG of versions per
/// entity. A head is any <see cref="VersionId"/> that is not referenced as a parent by any other
/// known change. A root change has <c>ParentVersionId == null</c>.
/// </para>
/// <para>
/// Uses <c>byte[]</c> for <see cref="Diff"/> (rather than <see cref="ReadOnlyMemory{T}"/>) so that
/// <see cref="System.Text.Json.JsonSerializer"/> will emit it as base64 for wire transport without
/// a custom converter. Record-synthesized equality compares the <c>byte[]</c> reference, not content
/// — callers needing structural equality should compare <see cref="Diff"/> via
/// <c>AsSpan().SequenceEqual(...)</c>.
/// </para>
/// </remarks>
/// <param name="EntityId">The entity whose state this change modifies.</param>
/// <param name="VersionId">The identifier this change produces.</param>
/// <param name="ParentVersionId">The parent version this change is based on, or <c>null</c> for a root.</param>
/// <param name="Timestamp">Wall-clock timestamp at which the change was produced.</param>
/// <param name="Diff">Opaque CRDT/JSON-patch diff bytes; interpretation is the consumer's concern.</param>
public sealed record ChangeRecord(
    EntityId EntityId,
    VersionId VersionId,
    VersionId? ParentVersionId,
    DateTimeOffset Timestamp,
    byte[] Diff);
