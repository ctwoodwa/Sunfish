namespace Sunfish.Kernel.Crdt;

/// <summary>
/// Factory / registry for <see cref="ICrdtDocument"/> instances. Registered as a singleton;
/// one engine per process. Paper §9, ADR 0028.
/// </summary>
public interface ICrdtEngine
{
    /// <summary>
    /// Create a fresh empty document with the given identifier. The returned document has
    /// no containers; callers access or create them via <see cref="ICrdtDocument.GetText"/>,
    /// <see cref="ICrdtDocument.GetMap"/>, or <see cref="ICrdtDocument.GetList"/>.
    /// </summary>
    ICrdtDocument CreateDocument(string documentId);

    /// <summary>
    /// Open a document by hydrating from a previously-serialized snapshot.
    /// The resulting document's <see cref="ICrdtDocument.DocumentId"/> is <paramref name="documentId"/>;
    /// passing a <paramref name="snapshot"/> produced under a different identifier is a caller error.
    /// </summary>
    ICrdtDocument OpenDocument(string documentId, ReadOnlyMemory<byte> snapshot);

    /// <summary>Backend identifier, e.g. "loro", "yrs", "stub".</summary>
    string EngineName { get; }

    /// <summary>Backend version string, useful for diagnostics and compatibility checks.</summary>
    string EngineVersion { get; }
}
