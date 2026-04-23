namespace Sunfish.Kernel.SchemaRegistry.Upcasters;

/// <summary>
/// Upcaster for additive, non-breaking schema changes. Narrower than
/// <see cref="Lenses.ISchemaLens"/> — no backward transform.
/// Paper §7.2: <i>"Additive changes (new optional fields, new event variants) are
/// handled in-place."</i>
/// </summary>
/// <remarks>
/// <para>
/// Use an upcaster when the newer-version shape is a strict superset of the older
/// shape (the canonical "add optional field" case). Use a lens
/// (<see cref="Lenses.ISchemaLens"/>) when the change is structural and needs a
/// backward transform — renames, type changes, reorganizations.
/// </para>
/// <para>
/// Paper §7.2 warns that <i>"upcaster chains accumulate over time and become
/// brittle"</i> and mandates periodic stream compaction. The
/// <see cref="Migration.CopyTransformMigrator"/> is the compaction vehicle.
/// </para>
/// </remarks>
public interface IUpcaster
{
    /// <summary>Event type this upcaster applies to.</summary>
    string EventType { get; }

    /// <summary>Source (older) schema version identifier.</summary>
    string FromVersion { get; }

    /// <summary>Target (newer) schema version identifier.</summary>
    string ToVersion { get; }

    /// <summary>Upcast an older-version event to the newer-version shape.</summary>
    object Upcast(object olderEvent);
}
