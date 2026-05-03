using Sunfish.Blocks.Maintenance.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Maintenance.Services;

/// <summary>
/// Multi-contact-per-vendor service per ADR 0058. Phase 2 ships the
/// in-memory reference implementation; persistence-backed implementations
/// land alongside the rest of blocks-maintenance v1.x storage.
/// </summary>
public interface IVendorContactService
{
    /// <summary>Persists a new contact for the supplied vendor.</summary>
    Task<VendorContact> AddContactAsync(VendorContact contact, CancellationToken ct);

    /// <summary>Updates an existing contact (replace-by-id).</summary>
    Task<VendorContact> UpdateContactAsync(VendorContact contact, CancellationToken ct);

    /// <summary>Removes the contact with the supplied id; no-op when unknown.</summary>
    Task RemoveContactAsync(VendorContactId id, CancellationToken ct);

    /// <summary>Streams every contact registered for the supplied vendor.</summary>
    IAsyncEnumerable<VendorContact> ListContactsAsync(VendorId vendor, CancellationToken ct);

    /// <summary>
    /// Returns the primary contact for <paramref name="vendor"/> at
    /// <paramref name="property"/>:
    /// <list type="number">
    ///   <item>If a contact has a per-property override marking it primary for
    ///         <paramref name="property"/>, return it.</item>
    ///   <item>Otherwise, return the contact whose <see cref="VendorContact.IsPrimaryForVendor"/> is true.</item>
    ///   <item>If neither applies, return null.</item>
    /// </list>
    /// When <paramref name="property"/> is null, the per-property override
    /// step is skipped — only the vendor-wide default applies.
    /// </summary>
    Task<VendorContact?> GetPrimaryForPropertyAsync(VendorId vendor, EntityId? property, CancellationToken ct);
}
