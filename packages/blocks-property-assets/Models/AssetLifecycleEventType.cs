namespace Sunfish.Blocks.PropertyAssets.Models;

/// <summary>
/// Discriminator for the kind of event captured in an
/// <see cref="AssetLifecycleEvent"/>. The set is closed in first-slice;
/// future hand-offs may extend (e.g., <c>MileageRecorded</c> when the
/// Vehicle subtype + Trip events ship).
/// </summary>
public enum AssetLifecycleEventType
{
    /// <summary>Asset was installed or first commissioned at the property.</summary>
    Installed,

    /// <summary>Asset was serviced (maintenance, tune-up, repair).</summary>
    Serviced,

    /// <summary>Asset was inspected (annual review, move-in/out walkthrough).</summary>
    Inspected,

    /// <summary>A warranty claim was filed against the asset.</summary>
    WarrantyClaimed,

    /// <summary>Asset was replaced (new asset record supersedes this one).</summary>
    Replaced,

    /// <summary>Asset was disposed of (sold, scrapped, demolished).</summary>
    Disposed,

    /// <summary>A photo was added to the asset record.</summary>
    PhotoAdded,

    /// <summary>Free-text notes were updated.</summary>
    NotesUpdated,
}
