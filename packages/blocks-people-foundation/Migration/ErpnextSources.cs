namespace Sunfish.Blocks.People.Foundation.Migration;

/// <summary>
/// Source record from an ERPNext <c>Customer</c> doctype. Field shape
/// matches the Frappe REST API verbatim so the importer can be fed
/// directly from a Frappe HTTP client or <c>erpnext.ts</c>.
/// </summary>
/// <param name="Name">ERPNext <c>name</c> — the stable id; the FK we dedupe on (e.g. <c>"CUST-0001"</c>).</param>
/// <param name="Modified">ERPNext <c>modified</c> — version key; string-ordinal comparison decides Skipped vs Updated.</param>
/// <param name="CustomerName">Human-readable display name (e.g. <c>"Acme Holdings LLC"</c>).</param>
/// <param name="CustomerType">ERPNext <c>customer_type</c> — typically <c>"Individual"</c> or <c>"Company"</c>; maps to <see cref="Sunfish.Blocks.People.Foundation.Models.PartyKind"/>.</param>
/// <param name="EmailId">Primary email address; optional. Imported as an <c>EmailAddress</c> row if shape-valid.</param>
/// <param name="MobileNo">Primary mobile phone (E.164 expected); optional. Imported as a <c>PhoneNumber</c> row if shape-valid.</param>
/// <param name="TaxId">ERPNext <c>tax_id</c>; optional. Stored unencrypted in v1 — see Party.TaxId TODO.</param>
/// <param name="Disabled">When true, the imported Party gets <c>DoNotContact = true</c> as a soft-deactivation signal.</param>
public sealed record ErpnextCustomerSource(
    string Name,
    string Modified,
    string CustomerName,
    string? CustomerType = null,
    string? EmailId = null,
    string? MobileNo = null,
    string? TaxId = null,
    bool Disabled = false);

/// <summary>
/// Source record from an ERPNext <c>Supplier</c> doctype. Mirrors the
/// <see cref="ErpnextCustomerSource"/> shape with field renames; the
/// importer maps both to the same <c>Party</c> + a different role-edge.
/// </summary>
/// <param name="Name">ERPNext <c>name</c> — stable id (e.g. <c>"SUP-0001"</c>).</param>
/// <param name="Modified">ERPNext <c>modified</c> — version key.</param>
/// <param name="SupplierName">Human-readable display name.</param>
/// <param name="SupplierType">ERPNext <c>supplier_type</c>; typically <c>"Individual"</c> or <c>"Company"</c>.</param>
/// <param name="EmailId">Primary email; optional.</param>
/// <param name="MobileNo">Primary mobile (E.164 expected); optional.</param>
/// <param name="TaxId">ERPNext <c>tax_id</c>; optional.</param>
/// <param name="Disabled">Soft-deactivation flag.</param>
public sealed record ErpnextSupplierSource(
    string Name,
    string Modified,
    string SupplierName,
    string? SupplierType = null,
    string? EmailId = null,
    string? MobileNo = null,
    string? TaxId = null,
    bool Disabled = false);
