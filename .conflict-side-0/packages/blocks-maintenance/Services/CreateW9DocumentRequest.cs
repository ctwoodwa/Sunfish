using System;
using Sunfish.Blocks.Maintenance.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Integrations.Signatures;

namespace Sunfish.Blocks.Maintenance.Services;

/// <summary>
/// Input parameters for <see cref="IW9DocumentService.CreateAsync"/>
/// (W#18 Phase 4 / ADR 0058). Plaintext TIN bytes are consumed by
/// the service and never persisted in plaintext.
/// </summary>
/// <param name="Vendor">The vendor this W-9 belongs to.</param>
/// <param name="LegalName">Legal name on the W-9 form.</param>
/// <param name="DbaName">"Doing business as" trade name (optional).</param>
/// <param name="TaxClassification">IRS federal-tax classification.</param>
/// <param name="PlaintextTin">SSN or EIN bytes; consumed and encrypted by the service.</param>
/// <param name="Address">Address printed on the W-9 form.</param>
/// <param name="Tenant">Tenant scope (used for DEK derivation + audit).</param>
/// <param name="ReceivedAt">When the W-9 was received from the vendor.</param>
/// <param name="SignatureRef">Optional signature acknowledgment reference (W#18 Phase 5 wiring).</param>
public sealed record CreateW9DocumentRequest(
    VendorId Vendor,
    string LegalName,
    string? DbaName,
    W9TaxClassification TaxClassification,
    ReadOnlyMemory<byte> PlaintextTin,
    W9MailingAddress Address,
    TenantId Tenant,
    DateTimeOffset ReceivedAt,
    SignatureEventRef? SignatureRef = null);
