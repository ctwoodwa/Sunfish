namespace Sunfish.Kernel.Crdt;

/// <summary>
/// Rich-text CRDT container. Paper §9 flags rich-text history as a primary growth axis;
/// implementations MUST provide tombstone GC or shallow-snapshot compaction.
/// </summary>
/// <remarks>
/// <para>
/// Indices are UTF-16 code-unit indices, matching .NET <see cref="string"/> semantics.
/// Concurrent inserts at the same index converge via the backend's tie-break rule
/// (typically a deterministic actor-id ordering).
/// </para>
/// </remarks>
public interface ICrdtText
{
    /// <summary>Current materialized string value.</summary>
    string Value { get; }

    /// <summary>Length of <see cref="Value"/> in UTF-16 code units.</summary>
    int Length { get; }

    /// <summary>Insert <paramref name="text"/> at <paramref name="index"/>.</summary>
    void Insert(int index, string text);

    /// <summary>Delete <paramref name="length"/> code units starting at <paramref name="index"/>.</summary>
    void Delete(int index, int length);

    /// <summary>Raised after a local or remote change mutates <see cref="Value"/>.</summary>
    event EventHandler<CrdtTextChangedEventArgs>? Changed;
}

/// <summary>
/// Text-container change notification. Emitted for both local and remote-applied mutations.
/// </summary>
/// <param name="Index">Start index where the change was observed.</param>
/// <param name="InsertedLength">Number of UTF-16 code units inserted (0 if pure delete).</param>
/// <param name="DeletedLength">Number of UTF-16 code units deleted (0 if pure insert).</param>
/// <param name="InsertedText">The inserted text, if any.</param>
public sealed record CrdtTextChangedEventArgs(int Index, int InsertedLength, int DeletedLength, string? InsertedText);
