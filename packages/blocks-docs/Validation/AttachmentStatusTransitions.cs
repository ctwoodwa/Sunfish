using Sunfish.Blocks.Docs.Models;

namespace Sunfish.Blocks.Docs.Validation;

/// <summary>
/// Allowed transitions in the attachment lifecycle:
///
/// <code>
///   Active     → Superseded | Tombstoned
///   Superseded → Tombstoned             (a superseded version can still be tombstoned for GDPR / retention)
///   Tombstoned                          (terminal)
/// </code>
///
/// <para>
/// Notably forbidden: <c>Superseded → Active</c> (no "un-supersede" — the
/// replacement row stands), <c>Tombstoned → Active</c> (no resurrection).
/// </para>
/// </summary>
public static class AttachmentStatusTransitions
{
    /// <summary>True if <paramref name="from"/> may legally transition to <paramref name="to"/>.</summary>
    public static bool IsAllowed(AttachmentStatus from, AttachmentStatus to) =>
        (from, to) switch
        {
            (AttachmentStatus.Active,     AttachmentStatus.Superseded)  => true,
            (AttachmentStatus.Active,     AttachmentStatus.Tombstoned)  => true,
            (AttachmentStatus.Superseded, AttachmentStatus.Tombstoned)  => true,
            _ => false,
        };
}
