namespace Sunfish.Blocks.PropertyEquipment.Models;

/// <summary>
/// Discriminator for the kind of event captured in an
/// <see cref="EquipmentLifecycleEvent"/>. The set is closed in first-slice;
/// future hand-offs may extend (e.g., <c>MileageRecorded</c> when the
/// Vehicle subtype + Trip events ship).
/// </summary>
public enum EquipmentLifecycleEventType
{
    /// <summary>Equipment was installed or first commissioned at the property.</summary>
    Installed,

    /// <summary>Equipment was serviced (maintenance, tune-up, repair).</summary>
    Serviced,

    /// <summary>Equipment was inspected (annual review, move-in/out walkthrough).</summary>
    Inspected,

    /// <summary>A warranty claim was filed against the equipment.</summary>
    WarrantyClaimed,

    /// <summary>Equipment was replaced (new equipment record supersedes this one).</summary>
    Replaced,

    /// <summary>Equipment was disposed of (sold, scrapped, demolished).</summary>
    Disposed,

    /// <summary>A photo was added to the equipment record.</summary>
    PhotoAdded,

    /// <summary>Free-text notes were updated.</summary>
    NotesUpdated,
}
