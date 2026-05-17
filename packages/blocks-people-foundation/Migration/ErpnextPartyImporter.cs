using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Blocks.People.Foundation.Services;
using Sunfish.Blocks.People.Foundation.Validation;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.People.Foundation.Migration;

/// <summary>
/// Default <see cref="IErpnextPartyImporter"/>. Uses
/// <see cref="IPartyReadModel"/> + <see cref="IPartyWriteService"/> for
/// persistence and role attachment — the importer holds no state of its own.
/// </summary>
public sealed class ErpnextPartyImporter : IErpnextPartyImporter
{
    private const string CustomerRefPrefix = "externalRef:erpnext:customer:";
    private const string SupplierRefPrefix = "externalRef:erpnext:supplier:";
    private const string ModifiedKeyPrefix = "erpnextModified:";

    private readonly IPartyReadModel _read;
    private readonly IPartyWriteService _write;

    public ErpnextPartyImporter(IPartyReadModel read, IPartyWriteService write)
    {
        _read = read ?? throw new ArgumentNullException(nameof(read));
        _write = write ?? throw new ArgumentNullException(nameof(write));
    }

    /// <inheritdoc />
    public Task<ImportOutcome<Party>> UpsertCustomerAsync(
        ErpnextCustomerSource source,
        TenantId tenantId,
        PartyId actor,
        CancellationToken cancellationToken = default)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (string.IsNullOrWhiteSpace(source.Name))
            return Task.FromResult(new ImportOutcome<Party>(ImportOutcomeKind.Failed, null, "ERPNext Customer.name is empty."));
        if (string.IsNullOrWhiteSpace(source.CustomerName))
            return Task.FromResult(new ImportOutcome<Party>(ImportOutcomeKind.Failed, null, "ERPNext Customer.customer_name is empty."));

        return UpsertAsync(
            tenantId: tenantId,
            actor: actor,
            externalRefTag: CustomerRefPrefix + source.Name,
            modifiedKeyTag: ModifiedKeyPrefix + source.Modified,
            displayName: source.CustomerName,
            kindHint: source.CustomerType,
            email: source.EmailId,
            phoneE164: source.MobileNo,
            taxId: source.TaxId,
            disabled: source.Disabled,
            roleName: PartyRoleName.Customer,
            roleRecordId: source.Name,
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task<ImportOutcome<Party>> UpsertSupplierAsync(
        ErpnextSupplierSource source,
        TenantId tenantId,
        PartyId actor,
        CancellationToken cancellationToken = default)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (string.IsNullOrWhiteSpace(source.Name))
            return Task.FromResult(new ImportOutcome<Party>(ImportOutcomeKind.Failed, null, "ERPNext Supplier.name is empty."));
        if (string.IsNullOrWhiteSpace(source.SupplierName))
            return Task.FromResult(new ImportOutcome<Party>(ImportOutcomeKind.Failed, null, "ERPNext Supplier.supplier_name is empty."));

        return UpsertAsync(
            tenantId: tenantId,
            actor: actor,
            externalRefTag: SupplierRefPrefix + source.Name,
            modifiedKeyTag: ModifiedKeyPrefix + source.Modified,
            displayName: source.SupplierName,
            kindHint: source.SupplierType,
            email: source.EmailId,
            phoneE164: source.MobileNo,
            taxId: source.TaxId,
            disabled: source.Disabled,
            roleName: PartyRoleName.Vendor,
            roleRecordId: source.Name,
            cancellationToken: cancellationToken);
    }

    private async Task<ImportOutcome<Party>> UpsertAsync(
        TenantId tenantId,
        PartyId actor,
        string externalRefTag,
        string modifiedKeyTag,
        string displayName,
        string? kindHint,
        string? email,
        string? phoneE164,
        string? taxId,
        bool disabled,
        string roleName,
        string roleRecordId,
        CancellationToken cancellationToken)
    {
        var existing = await FindExistingByExternalRefAsync(tenantId, externalRefTag, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null && existing.Tags.Contains(modifiedKeyTag, StringComparer.Ordinal))
        {
            return new ImportOutcome<Party>(ImportOutcomeKind.Skipped, existing, "ERPNext modified key unchanged.");
        }

        var kind = MapKind(kindHint);
        Party party;

        if (existing is null)
        {
            party = await _write.CreateAsync(tenantId, kind, displayName, actor, cancellationToken)
                .ConfigureAwait(false);
            party = await _write.UpdateAsync(
                party with
                {
                    TaxId = taxId,
                    DoNotContact = disabled,
                    Tags = new List<string> { externalRefTag, modifiedKeyTag },
                },
                actor,
                cancellationToken).ConfigureAwait(false);

            // Side-channel rows. Each is best-effort — a malformed phone
            // shouldn't fail the whole party import.
            await TryAttachContactsAsync(party.Id, email, phoneE164, actor, cancellationToken).ConfigureAwait(false);
            await _write.AttachRoleAsync(party.Id, roleName, roleRecordId, actor, cancellationToken)
                .ConfigureAwait(false);
            return new ImportOutcome<Party>(
                ImportOutcomeKind.Inserted,
                party,
                $"Customer/Supplier {externalRefTag.Split(':').Last()} imported.");
        }

        // Update path — refresh display + tags + tax + disabled flag; leave
        // contact info alone (append-only rows are owned by user actions, not
        // re-import overwrites).
        var updatedTags = MergeTags(existing.Tags, externalRefTag, modifiedKeyTag);
        party = await _write.UpdateAsync(
            existing with
            {
                DisplayName = displayName,
                TaxId = taxId,
                DoNotContact = disabled,
                Tags = updatedTags,
            },
            actor,
            cancellationToken).ConfigureAwait(false);

        // Re-ensure the role-edge exists (idempotent on RoleRecordId).
        await _write.AttachRoleAsync(party.Id, roleName, roleRecordId, actor, cancellationToken)
            .ConfigureAwait(false);
        return new ImportOutcome<Party>(ImportOutcomeKind.Updated, party, "ERPNext modified key advanced.");
    }

    private async Task TryAttachContactsAsync(
        PartyId partyId,
        string? email,
        string? phoneE164,
        PartyId actor,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(email)
            && EmailAddressValidator.ValidateAddress(email).IsValid)
        {
            try
            {
                await _write.AddEmailAsync(partyId, email!.Trim(), isPrimary: true, actor, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (PartyValidationException) { /* swallowed — best-effort contact import */ }
        }
        if (!string.IsNullOrWhiteSpace(phoneE164)
            && PhoneNumberValidator.ValidateE164(phoneE164).IsValid)
        {
            try
            {
                await _write.AddPhoneAsync(partyId, phoneE164!.Trim(), isPrimary: true, actor, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (PartyValidationException) { /* swallowed — best-effort */ }
        }
    }

    private async Task<Party?> FindExistingByExternalRefAsync(
        TenantId tenantId,
        string externalRefTag,
        CancellationToken cancellationToken)
    {
        var all = await _read.ListByTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return all.FirstOrDefault(p => p.Tags.Contains(externalRefTag, StringComparer.Ordinal));
    }

    private static PartyKind MapKind(string? erpnextType) =>
        string.Equals(erpnextType, "Company", StringComparison.OrdinalIgnoreCase)
            ? PartyKind.Organization
            : PartyKind.Person; // ERPNext default ("Individual") and null both map to Person

    private static IReadOnlyList<string> MergeTags(
        IReadOnlyList<string> existing,
        string externalRefTag,
        string modifiedKeyTag)
    {
        // Drop any prior erpnextModified:* tag (only one current value), add the
        // new one, keep the external-ref tag, keep everything else untouched.
        var keep = existing.Where(t => !t.StartsWith(ModifiedKeyPrefix, StringComparison.Ordinal)).ToList();
        if (!keep.Contains(externalRefTag, StringComparer.Ordinal))
            keep.Add(externalRefTag);
        keep.Add(modifiedKeyTag);
        return keep;
    }
}
