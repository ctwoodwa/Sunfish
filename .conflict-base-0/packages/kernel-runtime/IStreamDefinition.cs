namespace Sunfish.Kernel.Runtime;

/// <summary>
/// Paper §5.3 extension-point contract. Declares a CRDT stream a plugin
/// contributes: the stream identifier, its schema version, the event types it
/// emits, and the sync-bucket contributions for selective sync eligibility.
/// </summary>
public interface IStreamDefinition
{
    /// <summary>Stable identifier for the stream (e.g., "com.sunfish.accounting.ledger").</summary>
    string StreamId { get; }

    /// <summary>Schema version for events on this stream (semver).</summary>
    string SchemaVersion { get; }

    /// <summary>Event type names emitted on this stream.</summary>
    IReadOnlyCollection<string> EventTypes { get; }

    /// <summary>Sync-bucket IDs this stream contributes to. Empty means private/local-only.</summary>
    IReadOnlyCollection<string> BucketContributions { get; }
}
