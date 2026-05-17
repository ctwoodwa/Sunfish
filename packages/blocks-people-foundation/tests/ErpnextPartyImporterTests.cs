using Sunfish.Blocks.People.Foundation.Migration;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Blocks.People.Foundation.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.People.Foundation.Tests;

public class ErpnextPartyImporterTests
{
    private static TenantId Tenant() => new("acme");
    private static PartyId Actor() => PartyId.NewId();

    private static (ErpnextPartyImporter Importer, InMemoryPartyRepository Repo) NewSut()
    {
        var repo = new InMemoryPartyRepository();
        return (new ErpnextPartyImporter(repo, repo), repo);
    }

    // ── Customer flow ─────────────────────────────────────────────────

    [Fact]
    public async Task UpsertCustomer_FreshSource_InsertsPartyAndAttachesCustomerRole()
    {
        var (importer, repo) = NewSut();
        var source = new ErpnextCustomerSource(
            Name: "CUST-0001",
            Modified: "2026-05-16 22:00:00",
            CustomerName: "Jane Doe",
            CustomerType: "Individual",
            EmailId: "jane@example.com",
            MobileNo: "+14155551234");

        var outcome = await importer.UpsertCustomerAsync(source, Tenant(), Actor());

        Assert.Equal(ImportOutcomeKind.Inserted, outcome.Kind);
        Assert.NotNull(outcome.Entity);
        Assert.Equal(PartyKind.Person, outcome.Entity!.Kind);
        Assert.Equal("Jane Doe", outcome.Entity.DisplayName);

        var roles = await repo.GetActiveRolesAsync(outcome.Entity.Id);
        Assert.Single(roles);
        Assert.Equal(PartyRoleName.Customer, roles[0].RoleName);
        Assert.Equal("CUST-0001", roles[0].RoleRecordId);

        var emails = await repo.GetActiveEmailsAsync(outcome.Entity.Id);
        Assert.Single(emails);
        Assert.Equal("jane@example.com", emails[0].Address);

        var phones = await repo.GetActivePhonesAsync(outcome.Entity.Id);
        Assert.Single(phones);
        Assert.Equal("+14155551234", phones[0].E164);
    }

    [Fact]
    public async Task UpsertCustomer_CompanyType_MapsToOrganizationKind()
    {
        var (importer, _) = NewSut();
        var source = new ErpnextCustomerSource(
            Name: "CUST-0002",
            Modified: "2026-05-16",
            CustomerName: "Acme Holdings LLC",
            CustomerType: "Company");

        var outcome = await importer.UpsertCustomerAsync(source, Tenant(), Actor());

        Assert.Equal(ImportOutcomeKind.Inserted, outcome.Kind);
        Assert.Equal(PartyKind.Organization, outcome.Entity!.Kind);
    }

    [Fact]
    public async Task UpsertCustomer_SameModifiedKey_ReturnsSkipped()
    {
        var (importer, _) = NewSut();
        var source = new ErpnextCustomerSource("CUST-1", "2026-05-16", "Jane");
        var first = await importer.UpsertCustomerAsync(source, Tenant(), Actor());
        var second = await importer.UpsertCustomerAsync(source, Tenant(), Actor());

        Assert.Equal(ImportOutcomeKind.Inserted, first.Kind);
        Assert.Equal(ImportOutcomeKind.Skipped, second.Kind);
        Assert.Equal(first.Entity!.Id, second.Entity!.Id);
    }

    [Fact]
    public async Task UpsertCustomer_AdvancedModifiedKey_ReturnsUpdated()
    {
        var (importer, repo) = NewSut();
        var v1 = new ErpnextCustomerSource("CUST-1", "2026-05-16", "Jane Old Name");
        var v2 = new ErpnextCustomerSource("CUST-1", "2026-05-17", "Jane New Name", TaxId: "123-45-6789");

        await importer.UpsertCustomerAsync(v1, Tenant(), Actor());
        var second = await importer.UpsertCustomerAsync(v2, Tenant(), Actor());

        Assert.Equal(ImportOutcomeKind.Updated, second.Kind);
        Assert.Equal("Jane New Name", second.Entity!.DisplayName);
        Assert.Equal("123-45-6789", second.Entity.TaxId);

        // Tags should contain the new modified-key, not the old one.
        Assert.Contains("erpnextModified:2026-05-17", second.Entity.Tags);
        Assert.DoesNotContain("erpnextModified:2026-05-16", second.Entity.Tags);

        // Role-edge stays single + active (idempotent re-attach).
        var roles = await repo.GetActiveRolesAsync(second.Entity.Id);
        Assert.Single(roles);
    }

    [Fact]
    public async Task UpsertCustomer_DisabledFlag_SetsDoNotContact()
    {
        var (importer, _) = NewSut();
        var source = new ErpnextCustomerSource("CUST-DIS", "2026-05-16", "Inactive Co", Disabled: true);

        var outcome = await importer.UpsertCustomerAsync(source, Tenant(), Actor());

        Assert.True(outcome.Entity!.DoNotContact);
    }

    [Fact]
    public async Task UpsertCustomer_EmptyName_ReturnsFailed()
    {
        var (importer, _) = NewSut();
        var source = new ErpnextCustomerSource("", "2026-05-16", "Jane");
        var outcome = await importer.UpsertCustomerAsync(source, Tenant(), Actor());
        Assert.Equal(ImportOutcomeKind.Failed, outcome.Kind);
        Assert.Null(outcome.Entity);
    }

    [Fact]
    public async Task UpsertCustomer_EmptyCustomerName_ReturnsFailed()
    {
        var (importer, _) = NewSut();
        var source = new ErpnextCustomerSource("CUST-1", "2026-05-16", "");
        var outcome = await importer.UpsertCustomerAsync(source, Tenant(), Actor());
        Assert.Equal(ImportOutcomeKind.Failed, outcome.Kind);
    }

    [Fact]
    public async Task UpsertCustomer_MalformedEmail_StillImportsParty_SilentlyDropsBadEmail()
    {
        var (importer, repo) = NewSut();
        var source = new ErpnextCustomerSource(
            Name: "CUST-EMAIL-BAD",
            Modified: "2026-05-16",
            CustomerName: "Jane",
            EmailId: "not-an-email");

        var outcome = await importer.UpsertCustomerAsync(source, Tenant(), Actor());
        Assert.Equal(ImportOutcomeKind.Inserted, outcome.Kind);
        var emails = await repo.GetActiveEmailsAsync(outcome.Entity!.Id);
        Assert.Empty(emails);
    }

    [Fact]
    public async Task UpsertCustomer_MalformedPhone_StillImportsParty_SilentlyDropsBadPhone()
    {
        var (importer, repo) = NewSut();
        var source = new ErpnextCustomerSource(
            Name: "CUST-PHONE-BAD",
            Modified: "2026-05-16",
            CustomerName: "Jane",
            MobileNo: "415-555-1234"); // not E.164

        var outcome = await importer.UpsertCustomerAsync(source, Tenant(), Actor());
        Assert.Equal(ImportOutcomeKind.Inserted, outcome.Kind);
        var phones = await repo.GetActivePhonesAsync(outcome.Entity!.Id);
        Assert.Empty(phones);
    }

    // ── Supplier flow ─────────────────────────────────────────────────

    [Fact]
    public async Task UpsertSupplier_FreshSource_AttachesVendorRole()
    {
        var (importer, repo) = NewSut();
        var source = new ErpnextSupplierSource(
            Name: "SUP-0001",
            Modified: "2026-05-16",
            SupplierName: "Acme Supply Co",
            SupplierType: "Company");

        var outcome = await importer.UpsertSupplierAsync(source, Tenant(), Actor());

        Assert.Equal(ImportOutcomeKind.Inserted, outcome.Kind);
        Assert.Equal(PartyKind.Organization, outcome.Entity!.Kind);

        var roles = await repo.GetActiveRolesAsync(outcome.Entity.Id);
        Assert.Single(roles);
        Assert.Equal(PartyRoleName.Vendor, roles[0].RoleName);
        Assert.Equal("SUP-0001", roles[0].RoleRecordId);
    }

    [Fact]
    public async Task UpsertSupplier_SameModifiedKey_ReturnsSkipped()
    {
        var (importer, _) = NewSut();
        var source = new ErpnextSupplierSource("SUP-1", "2026-05-16", "Acme Supply");
        await importer.UpsertSupplierAsync(source, Tenant(), Actor());
        var second = await importer.UpsertSupplierAsync(source, Tenant(), Actor());
        Assert.Equal(ImportOutcomeKind.Skipped, second.Kind);
    }

    [Fact]
    public async Task CustomerAndSupplier_SameErpnextName_AreSeparateParties()
    {
        // ERPNext Name namespaces are doctype-scoped — "X-001" can mean a
        // Customer in one table and a Supplier in another. Importer must
        // dedupe per-doctype, not globally.
        var (importer, _) = NewSut();
        var customer = await importer.UpsertCustomerAsync(
            new ErpnextCustomerSource("ENT-001", "2026-05-16", "Acme as Customer"),
            Tenant(), Actor());
        var supplier = await importer.UpsertSupplierAsync(
            new ErpnextSupplierSource("ENT-001", "2026-05-16", "Acme as Supplier"),
            Tenant(), Actor());

        Assert.Equal(ImportOutcomeKind.Inserted, customer.Kind);
        Assert.Equal(ImportOutcomeKind.Inserted, supplier.Kind);
        Assert.NotEqual(customer.Entity!.Id, supplier.Entity!.Id);
    }
}
