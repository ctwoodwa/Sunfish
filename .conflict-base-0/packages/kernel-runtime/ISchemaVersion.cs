namespace Sunfish.Kernel.Runtime;

/// <summary>
/// Paper §5.3 extension-point contract. Declares a schema version for an event
/// type and provides an upcaster that lifts older payloads into the current
/// in-memory shape. Paper §7.2.
/// </summary>
public interface ISchemaVersion
{
    /// <summary>The event type this schema version applies to.</summary>
    string EventType { get; }

    /// <summary>The current (canonical) schema version produced by this upcaster.</summary>
    string Version { get; }

    /// <summary>Older versions this upcaster can lift from. The oldest supported version is the floor.</summary>
    IReadOnlyCollection<string> SupportedVersions { get; }

    /// <summary>
    /// Lift an older-version event payload into the current in-memory shape.
    /// Returns <c>null</c> if the upcaster cannot handle <paramref name="fromVersion"/>.
    /// Upcasters must be pure functions (no side effects).
    /// </summary>
    /// <param name="olderEvent">The older-version event payload.</param>
    /// <param name="fromVersion">The version of the older payload.</param>
    /// <returns>The upcast payload, or <c>null</c> if unsupported.</returns>
    object? Upcast(object olderEvent, string fromVersion);
}
