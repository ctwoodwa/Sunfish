using System.Diagnostics.CodeAnalysis;

namespace Sunfish.Foundation.Ship.Common;

/// <summary>
/// Permission action identifier per ADR 0077 §2. The 9 v1 actions are the
/// closed set the resolver evaluates against; downstream W#35 cohort ADRs
/// (Quarterdeck, Engine Room, Tactical, Sick Bay, Ship's Office) extend the
/// surface area by composing on these — they do NOT introduce new
/// <see cref="ShipAction"/> values without an ADR amendment to ADR 0077.
/// </summary>
/// <remarks>
/// Each static-readonly field is the canonical wire-format string used in
/// audit-payload <c>action</c> keys + <see cref="DefaultPermissionResolver"/>'s
/// <c>ActionMinimumDeck</c> dictionary keys. The <see cref="Name"/> field is
/// the JSON-serialized form (lowercase-kebab) for cross-process replay.
/// </remarks>
/// <param name="Name">
/// Canonical lowercase-kebab name (e.g., <c>"issue-standing-order"</c>). Used
/// directly in audit payloads + cohort wire formats.
/// </param>
[SuppressMessage("Performance", "CA1815",
    Justification = "record struct provides value equality; CA1815 is a class-only diagnostic.")]
public readonly record struct ShipAction(string Name)
{
    /// <summary>Read-only access (location-scoped).</summary>
    public static readonly ShipAction Read = new("read");

    /// <summary>Write access at the location's MainDeck (location-scoped).</summary>
    public static readonly ShipAction Write = new("write");

    /// <summary>Issue a Standing Order (location-scoped; resolves through ADR 0065).</summary>
    public static readonly ShipAction IssueStandingOrder = new("issue-standing-order");

    /// <summary>Approve a pending Standing Order or escalation (resource-scoped).</summary>
    public static readonly ShipAction Approve = new("approve");

    /// <summary>Promote a subject's <see cref="ShipRole"/> (location-scoped; subject hierarchy-guarded).</summary>
    public static readonly ShipAction PromoteRole = new("promote-role");

    /// <summary>Begin an OOD/EOOW watch (location-scoped; requires watch designation).</summary>
    public static readonly ShipAction StandWatch = new("stand-watch");

    /// <summary>Hand off the OOD/EOOW watch (location-scoped; requires watch designation).</summary>
    public static readonly ShipAction TransferWatch = new("transfer-watch");

    /// <summary>Quarantine a record (resource-scoped; <see cref="DeckDepth.BelowTheWaterline"/>).</summary>
    public static readonly ShipAction Quarantine = new("quarantine");

    /// <summary>Override an active quarantine (resource-scoped; <see cref="DeckDepth.BelowTheWaterline"/>).</summary>
    public static readonly ShipAction OverrideQuarantine = new("override-quarantine");

    // ===== ADR 0083 §4 — W#55 Ship's Office =====

    /// <summary>Browse the Ship's Office (location-scoped; minimum role <see cref="ShipRole.Scribe"/>; <see cref="DeckDepth.TopDeck"/>).</summary>
    public static readonly ShipAction ViewShipsOffice = new("view-ships-office");

    /// <summary>Edit a Ship's Office document via <c>IContentEditorSurface</c> (location-scoped; minimum role <see cref="ShipRole.Scribe"/>; <see cref="DeckDepth.TopDeck"/>).</summary>
    public static readonly ShipAction EditShipsOfficeDocument = new("edit-ships-office-doc");

    /// <summary>Publish a draft Ship's Office document via <c>IShipsOfficeCommandService.PublishAsync</c> (location-scoped; minimum role <see cref="ShipRole.XO"/>; <see cref="DeckDepth.MainDeck"/>).</summary>
    public static readonly ShipAction PublishShipsOfficeDocument = new("publish-ships-office-doc");

    /// <summary>Archive a published Ship's Office document via <c>IShipsOfficeCommandService.ArchiveAsync</c> (location-scoped; minimum role <see cref="ShipRole.XO"/>; <see cref="DeckDepth.MainDeck"/>).</summary>
    public static readonly ShipAction ArchiveShipsOfficeDocument = new("archive-ships-office-doc");

    // ===== ADR 0079 §4 — W#50 Engine Room Observability =====

    /// <summary>Browse the Engine Room observability surface (location-scoped; <see cref="DeckDepth.TopDeck"/>; minimum role department-head per §Trust — Phase 2 enforcement).</summary>
    public static readonly ShipAction ViewEngineRoom = new("view-engine-room");

    /// <summary>Browse the Damage Control panel (location-scoped; <see cref="DeckDepth.MainDeck"/>; minimum role <see cref="ShipRole.EngineerOfficer"/> per §Trust).</summary>
    public static readonly ShipAction ViewDamageControl = new("view-damage-control");

    /// <summary>
    /// Apply a quarantine to a CRDT document (resource-scoped;
    /// <see cref="DeckDepth.MainDeck"/>; minimum role
    /// <see cref="ShipRole.EngineerOfficer"/> + audit-loud per §Trust).
    /// Sits at <see cref="DeckDepth.MainDeck"/> (NOT
    /// <see cref="DeckDepth.BelowTheWaterline"/>) because routine
    /// quarantine is reversible via <see cref="ReleaseQuarantine"/>;
    /// <see cref="OverrideQuarantine"/> is the irreversible destructive
    /// sibling and stays at <see cref="DeckDepth.BelowTheWaterline"/>.
    /// Per W#50 P1 council Minor m3.
    /// </summary>
    public static readonly ShipAction QuarantineDocument = new("quarantine-document");

    /// <summary>Release a CRDT document from quarantine (resource-scoped; <see cref="DeckDepth.MainDeck"/>; minimum role <see cref="ShipRole.EngineerOfficer"/> + audit-loud per §Trust).</summary>
    public static readonly ShipAction ReleaseQuarantine = new("release-quarantine");

    /// <summary>Compact a CRDT document's representation (resource-scoped; <see cref="DeckDepth.MainDeck"/>; minimum role <see cref="ShipRole.EngineerOfficer"/> per §Trust).</summary>
    public static readonly ShipAction CompactDocument = new("compact-document");
}
