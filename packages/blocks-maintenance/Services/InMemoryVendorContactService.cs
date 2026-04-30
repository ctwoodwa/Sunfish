using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Sunfish.Blocks.Maintenance.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Maintenance.Services;

/// <summary>
/// In-memory <see cref="IVendorContactService"/> for tests + non-production
/// hosts. Enforces the at-most-one-primary-per-vendor invariant on
/// add/update — when a contact is added/updated with
/// <see cref="VendorContact.IsPrimaryForVendor"/> = true, any prior
/// primary at that vendor is automatically demoted.
/// </summary>
public sealed class InMemoryVendorContactService : IVendorContactService
{
    private readonly ConcurrentDictionary<VendorContactId, VendorContact> _byId = new();

    /// <inheritdoc />
    public Task<VendorContact> AddContactAsync(VendorContact contact, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(contact);
        ct.ThrowIfCancellationRequested();
        UpsertWithPrimaryInvariant(contact);
        return Task.FromResult(contact);
    }

    /// <inheritdoc />
    public Task<VendorContact> UpdateContactAsync(VendorContact contact, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(contact);
        ct.ThrowIfCancellationRequested();
        if (!_byId.ContainsKey(contact.Id))
        {
            throw new InvalidOperationException($"Contact '{contact.Id.Value}' does not exist; use AddContactAsync to create.");
        }
        UpsertWithPrimaryInvariant(contact);
        return Task.FromResult(contact);
    }

    /// <inheritdoc />
    public Task RemoveContactAsync(VendorContactId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _byId.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<VendorContact> ListContactsAsync(VendorId vendor, [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var c in _byId.Values.Where(c => c.Vendor == vendor))
        {
            ct.ThrowIfCancellationRequested();
            yield return c;
            await Task.Yield();
        }
    }

    /// <inheritdoc />
    public Task<VendorContact?> GetPrimaryForPropertyAsync(VendorId vendor, EntityId? property, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var vendorContacts = _byId.Values.Where(c => c.Vendor == vendor).ToList();

        // Per-property override (only when property supplied).
        if (property is { } prop)
        {
            var overridden = vendorContacts.FirstOrDefault(c =>
                c.PrimaryForProperty.TryGetValue(prop, out var primary) && primary);
            if (overridden is not null)
            {
                return Task.FromResult<VendorContact?>(overridden);
            }
        }

        // Vendor-wide default.
        var defaultPrimary = vendorContacts.FirstOrDefault(c => c.IsPrimaryForVendor);
        return Task.FromResult<VendorContact?>(defaultPrimary);
    }

    /// <summary>
    /// Upserts <paramref name="contact"/> while preserving the
    /// at-most-one-primary-per-vendor invariant. When the upsert sets
    /// <see cref="VendorContact.IsPrimaryForVendor"/> = true, any prior
    /// primary at the same vendor is demoted to false in the same call.
    /// </summary>
    private void UpsertWithPrimaryInvariant(VendorContact contact)
    {
        if (contact.IsPrimaryForVendor)
        {
            // Demote any existing primary at this vendor (excluding the
            // contact being upserted, since its own state is the new
            // truth).
            foreach (var existing in _byId.Values
                .Where(c => c.Vendor == contact.Vendor && c.IsPrimaryForVendor && c.Id != contact.Id)
                .ToList())
            {
                _byId[existing.Id] = existing with { IsPrimaryForVendor = false };
            }
        }
        _byId[contact.Id] = contact;
    }
}
