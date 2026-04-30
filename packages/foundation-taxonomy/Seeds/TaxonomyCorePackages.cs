using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Taxonomy.Models;
using Sunfish.Foundation.Taxonomy.Services;

namespace Sunfish.Foundation.Taxonomy.Seeds;

/// <summary>
/// Sunfish-shipped Authoritative-regime starter taxonomies, as authored in
/// the v1.0 charters (PR #242).
/// </summary>
public static class TaxonomyCorePackages
{
    /// <summary>
    /// <c>Sunfish.Signature.Scopes@1.0.0</c> — the legal/operational scopes
    /// a captured signature may attest to (per ADR 0054 Pattern E + charter
    /// in PR #242). 17 root nodes + 7 children covering lease execution,
    /// inspection acknowledgments, notary scopes, consent forms, and
    /// payment authorization.
    /// </summary>
    public static TaxonomyCorePackage SunfishSignatureScopes
    {
        get
        {
            var id = new TaxonomyDefinitionId("Sunfish", "Signature", "Scopes");
            var version = TaxonomyVersion.V1_0_0;
            var publishedAt = new DateTimeOffset(2026, 4, 29, 0, 0, 0, TimeSpan.Zero);

            var def = new TaxonomyDefinition
            {
                Id = id,
                Version = version,
                Governance = TaxonomyGovernanceRegime.Authoritative,
                Description = "Legal/operational scopes a captured signature may attest to (ADR 0054 Pattern E). Used as the SignatureScope reference set on captured signatures.",
                Owner = ActorId.Sunfish,
                PublishedAt = publishedAt,
            };

            var nodes = new List<TaxonomyNode>(24);
            void AddRoot(string code, string display, string description) =>
                nodes.Add(new TaxonomyNode
                {
                    Id = new TaxonomyNodeId(id, code),
                    DefinitionVersion = version,
                    Display = display,
                    Description = description,
                    ParentCode = null,
                    Status = TaxonomyNodeStatus.Active,
                    PublishedAt = publishedAt,
                });

            void AddChild(string code, string parentCode, string display, string description) =>
                nodes.Add(new TaxonomyNode
                {
                    Id = new TaxonomyNodeId(id, code),
                    DefinitionVersion = version,
                    Display = display,
                    Description = description,
                    ParentCode = parentCode,
                    Status = TaxonomyNodeStatus.Active,
                    PublishedAt = publishedAt,
                });

            // 17 root nodes (charter §"Root nodes")
            AddRoot("lease-execution", "Lease Execution", "Tenant or landlord signing a lease document with binding intent.");
            AddRoot("lease-amendment", "Lease Amendment", "Signature attesting to a revision of an existing lease (rider, addendum, modification).");
            AddRoot("inspection-acknowledgment", "Inspection Acknowledgment", "Acknowledgment of an inspection report's findings (not necessarily agreement).");
            AddRoot("inspection-disagreement", "Inspection Disagreement", "Acknowledgment of report receipt with explicit disagreement noted.");
            AddRoot("move-in-checklist", "Move-In Checklist", "Tenant signature on move-in condition checklist.");
            AddRoot("move-out-checklist", "Move-Out Checklist", "Tenant signature on move-out condition checklist.");
            AddRoot("notary-jurat", "Notary Jurat", "Notarized affidavit (signer swore truth of contents under oath).");
            AddRoot("notary-acknowledgment", "Notary Acknowledgment", "Notary attesting to identity of signer (no oath).");
            AddRoot("witness-attestation", "Witness Attestation", "Third-party witness confirming signing event.");
            AddRoot("vendor-acceptance", "Vendor Acceptance", "Vendor signing acceptance of work order or service agreement.");
            AddRoot("payment-authorization", "Payment Authorization", "Authorization of a specific payment (ACH, wire, recurring debit).");
            AddRoot("consent-background-check", "Consent — Background Check", "Applicant consent to FCRA-compliant background screening.");
            AddRoot("consent-credit-check", "Consent — Credit Check", "Applicant consent to FCRA-compliant credit screening.");
            AddRoot("consent-disclosure", "Consent — Disclosure", "Acknowledgment of receipt of mandated disclosure (lead-paint, fair housing, etc.).");
            AddRoot("right-of-entry-notice", "Right-of-Entry Notice", "Tenant acknowledgment of landlord notice to enter.");
            AddRoot("delivery-receipt", "Delivery Receipt", "Signature confirming delivery of physical item or document.");
            AddRoot("general-acknowledgment", "General Acknowledgment", "Catch-all when no more specific scope fits (cluster intake explicit).");

            // 7 children (charter §"Children")
            AddChild("lease-origination", "lease-execution", "Lease Origination", "First execution of a lease for a unit (vs renewal).");
            AddChild("lease-renewal", "lease-execution", "Lease Renewal", "Execution of a lease that follows a prior lease for same parties + unit.");
            AddChild("inspection-acknowledgment-annual", "inspection-acknowledgment", "Annual Inspection Acknowledgment", "Annual property inspection report.");
            AddChild("inspection-acknowledgment-move-in", "inspection-acknowledgment", "Move-In Inspection Acknowledgment", "Pre-occupancy inspection report.");
            AddChild("inspection-acknowledgment-move-out", "inspection-acknowledgment", "Move-Out Inspection Acknowledgment", "Post-occupancy inspection report.");
            AddChild("inspection-acknowledgment-post-repair", "inspection-acknowledgment", "Post-Repair Inspection Acknowledgment", "Post-completion verification of work-order resolution.");
            AddChild("inspection-acknowledgment-jurisdictional", "inspection-acknowledgment", "Jurisdictional Inspection Acknowledgment", "City/county/state-mandated inspection.");

            return new TaxonomyCorePackage
            {
                Definition = def,
                Nodes = nodes.AsReadOnly(),
            };
        }
    }

    /// <summary>
    /// <c>Sunfish.Leasing.JurisdictionRules@1.0.0</c> — fair-housing +
    /// FCRA + tenant-protection rule families consumed by the leasing
    /// pipeline (W#22) + the right-of-entry compliance check (ADR 0060).
    /// 7 root jurisdictions + 21 children covering FHA seven protected
    /// classes, FCRA workflow rules, CA Unruh + FEHC, NY Tenant Protection
    /// Act, and source-of-income rules. Charter authored in W#22 Phase 4
    /// (<c>icm/00_intake/output/starter-taxonomies-v1-leasing-2026-04-30.md</c>).
    /// </summary>
    public static TaxonomyCorePackage SunfishLeasingJurisdictionRules
    {
        get
        {
            var id = new TaxonomyDefinitionId("Sunfish", "Leasing", "JurisdictionRules");
            var version = TaxonomyVersion.V1_0_0;
            var publishedAt = new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero);

            var def = new TaxonomyDefinition
            {
                Id = id,
                Version = version,
                Governance = TaxonomyGovernanceRegime.Authoritative,
                Description = "Fair-housing + FCRA + tenant-protection rule families consumed by the leasing pipeline (W#22) + IJurisdictionPolicyResolver (ADR 0060).",
                Owner = ActorId.Sunfish,
                PublishedAt = publishedAt,
            };

            var nodes = new List<TaxonomyNode>(28);
            void AddRoot(string code, string display, string description) =>
                nodes.Add(new TaxonomyNode
                {
                    Id = new TaxonomyNodeId(id, code),
                    DefinitionVersion = version,
                    Display = display,
                    Description = description,
                    ParentCode = null,
                    Status = TaxonomyNodeStatus.Active,
                    PublishedAt = publishedAt,
                });

            void AddChild(string code, string parentCode, string display, string description) =>
                nodes.Add(new TaxonomyNode
                {
                    Id = new TaxonomyNodeId(id, code),
                    DefinitionVersion = version,
                    Display = display,
                    Description = description,
                    ParentCode = parentCode,
                    Status = TaxonomyNodeStatus.Active,
                    PublishedAt = publishedAt,
                });

            // 7 root jurisdictions (charter §"Root nodes")
            AddRoot("us-fed.fha", "US Fair Housing Act", "Title VIII of the Civil Rights Act; bans discrimination in housing on the basis of seven protected classes.");
            AddRoot("us-fed.fcra", "US Fair Credit Reporting Act", "Federal regulation governing consumer-report use in housing decisions. Drives the adverse-action notice + dispute-window workflow.");
            AddRoot("us-fed.fha-source-of-income", "US Source-of-Income Rules", "HUD interpretive guidance + state-level extensions covering Section 8 voucher / housing-assistance discrimination prohibitions.");
            AddRoot("us-state.ca.unruh", "California Unruh Civil Rights Act", "California's broad civil-rights law extending FHA-style protections to additional protected classes.");
            AddRoot("us-state.ca.fehc", "California Fair Employment & Housing Council Regs", "California regulations operationalizing FHA + Unruh; specifies prohibited questions + tenant-screening criteria.");
            AddRoot("us-state.ny.tpa", "New York Tenant Protection Act", "NY 2019 rent-regulation + tenant-screening reforms (e.g., 30-day notice rules, application-fee caps).");
            AddRoot("us-state.ny.adverse-action-extended-window", "NY Extended Adverse-Action Window", "NY-state extension of the FCRA dispute window (90 days for source-of-income decisions).");

            // FHA protected-class enumeration (7 children)
            AddChild("us-fed.fha.race", "us-fed.fha", "Race", "Protected under FHA §3604(a–f).");
            AddChild("us-fed.fha.color", "us-fed.fha", "Color", "Protected under FHA §3604(a–f).");
            AddChild("us-fed.fha.religion", "us-fed.fha", "Religion", "Protected under FHA §3604(a–f).");
            AddChild("us-fed.fha.sex", "us-fed.fha", "Sex", "Protected under FHA §3604(a–f); HUD interprets to include gender identity + sexual orientation since 2021.");
            AddChild("us-fed.fha.familial-status", "us-fed.fha", "Familial Status", "Children-in-household protection per FHA §3602(k).");
            AddChild("us-fed.fha.national-origin", "us-fed.fha", "National Origin", "Protected under FHA §3604(a–f).");
            AddChild("us-fed.fha.disability", "us-fed.fha", "Disability", "Protected under FHA §3604(f); reasonable-accommodation duty applies.");

            // FCRA workflow rules (4 children)
            AddChild("us-fed.fcra.adverse-action-notice", "us-fed.fcra", "Adverse-Action Notice Requirement", "§615(a) mandatory notice when decision is based on a consumer report. Sunfish ships the MandatoryFcraStatement (W#22 Phase 3).");
            AddChild("us-fed.fcra.dispute-window-60d", "us-fed.fcra", "60-Day Dispute Window", "§612(a) right to free report + dispute within 60 days. Default FcraAdverseActionNoticeGenerator.DefaultDisputeWindow.");
            AddChild("us-fed.fcra.consent-required", "us-fed.fcra", "Background-Check Consent Requirement", "§604(b) requires written consent for consumer-report procurement; satisfied by consent-background-check signature scope.");
            AddChild("us-fed.fcra.permissible-purpose", "us-fed.fcra", "Permissible-Purpose Requirement", "§604(a) limits consumer-report use to enumerated purposes (tenant-screening qualifies).");

            // CA Unruh additional protected classes (5 children)
            AddChild("us-state.ca.unruh.sexual-orientation", "us-state.ca.unruh", "Sexual Orientation", "CA Civil Code §51(b).");
            AddChild("us-state.ca.unruh.gender-identity", "us-state.ca.unruh", "Gender Identity", "CA Civil Code §51(e).");
            AddChild("us-state.ca.unruh.marital-status", "us-state.ca.unruh", "Marital Status", "CA Civil Code §51(b).");
            AddChild("us-state.ca.unruh.ancestry", "us-state.ca.unruh", "Ancestry", "CA Civil Code §51(b).");
            AddChild("us-state.ca.unruh.medical-condition", "us-state.ca.unruh", "Medical Condition", "CA Civil Code §51(b); incl. genetic information.");

            // CA FEHC operational rules (2 children)
            AddChild("us-state.ca.fehc.prohibited-question-list", "us-state.ca.fehc", "Prohibited-Question List", "Cal. Code Regs. §12181 — questions an operator may not ask during application/screening.");
            AddChild("us-state.ca.fehc.application-fee-cap", "us-state.ca.fehc", "Application Fee Cap", "CA Civil Code §1950.6 — fee cap; refund duty.");

            // NY TPA operational rules (3 children)
            AddChild("us-state.ny.tpa.application-fee-cap-20usd", "us-state.ny.tpa", "$20 Application Fee Cap", "RPL §238-a — application fees capped at $20.");
            AddChild("us-state.ny.tpa.security-deposit-cap-1mo", "us-state.ny.tpa", "1-Month Security Deposit Cap", "RPL §7-108 — security deposit + last month's rent capped at one month's rent.");
            AddChild("us-state.ny.tpa.tenant-blacklist-prohibition", "us-state.ny.tpa", "Tenant Blacklist Prohibition", "RPL §227-f — prohibits use of tenant-blacklist databases (Housing Court records).");

            // Source-of-income rules (2 children)
            AddChild("us-fed.fha-source-of-income.section-8-voucher", "us-fed.fha-source-of-income", "Section 8 Voucher Acceptance", "HUD interpretive guidance + state-level mandates that Section 8 vouchers must be accepted on equal terms.");
            AddChild("us-fed.fha-source-of-income.housing-assistance", "us-fed.fha-source-of-income", "Other Housing Assistance", "Disability/veterans' housing programs covered by similar state-level rules.");

            return new TaxonomyCorePackage
            {
                Definition = def,
                Nodes = nodes.AsReadOnly(),
            };
        }
    }

    /// <summary>
    /// <c>Sunfish.Vendor.Specialties@1.0.0</c> — vendor trade + service
    /// categories per W#18 Phase 6 + ADR 0058. Replaces the
    /// <c>VendorSpecialty</c> enum in blocks-maintenance; each enum value
    /// is preserved as a v1.0 root node so consumers migrating see no
    /// semantic regression. 11 root anchors + 19 sub-specialty children
    /// = 30 nodes. Charter authored in W#18 Phase 6
    /// (<c>icm/00_intake/output/sunfish-vendor-specialties-v1-charter-2026-04-30.md</c>).
    /// </summary>
    public static TaxonomyCorePackage SunfishVendorSpecialties
    {
        get
        {
            var id = new TaxonomyDefinitionId("Sunfish", "Vendor", "Specialties");
            var version = TaxonomyVersion.V1_0_0;
            var publishedAt = new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero);

            var def = new TaxonomyDefinition
            {
                Id = id,
                Version = version,
                Governance = TaxonomyGovernanceRegime.Authoritative,
                Description = "Vendor trade + service categories. Replaces blocks-maintenance VendorSpecialty enum per ADR 0058 cross-package wiring.",
                Owner = ActorId.Sunfish,
                PublishedAt = publishedAt,
            };

            var nodes = new List<TaxonomyNode>(30);
            void AddRoot(string code, string display, string description) =>
                nodes.Add(new TaxonomyNode
                {
                    Id = new TaxonomyNodeId(id, code),
                    DefinitionVersion = version,
                    Display = display,
                    Description = description,
                    ParentCode = null,
                    Status = TaxonomyNodeStatus.Active,
                    PublishedAt = publishedAt,
                });

            void AddChild(string code, string parentCode, string display, string description) =>
                nodes.Add(new TaxonomyNode
                {
                    Id = new TaxonomyNodeId(id, code),
                    DefinitionVersion = version,
                    Display = display,
                    Description = description,
                    ParentCode = parentCode,
                    Status = TaxonomyNodeStatus.Active,
                    PublishedAt = publishedAt,
                });

            // 11 root anchors (preserve every existing VendorSpecialty enum value)
            AddRoot("general-contractor", "General Contractor", "Broad-capability contractor handling multi-trade jobs (renovation, multi-system repair).");
            AddRoot("plumbing", "Plumbing", "Plumbing installation, repair, and maintenance.");
            AddRoot("electrical", "Electrical", "Electrical wiring, fixture installation, panel work.");
            AddRoot("hvac", "HVAC", "Heating, ventilation, air conditioning installation + repair.");
            AddRoot("landscaping", "Landscaping", "Grounds maintenance, lawn care, tree work.");
            AddRoot("painting", "Painting", "Interior + exterior painting.");
            AddRoot("roofing", "Roofing", "Roof installation, repair, gutter work.");
            AddRoot("pest-control", "Pest Control", "Extermination + ongoing pest management.");
            AddRoot("appliances", "Appliances", "Appliance installation + repair.");
            AddRoot("cleaning", "Cleaning", "Janitorial + cleaning services.");
            AddRoot("other", "Other", "Catch-all when no more specific specialty fits.");

            // Plumbing children (3)
            AddChild("plumbing.water-heater", "plumbing", "Plumbing — Water Heater", "Tank + tankless water heater install/repair/replace.");
            AddChild("plumbing.drain-cleaning", "plumbing", "Plumbing — Drain Cleaning", "Hydrojetting, snake, drain unclogging.");
            AddChild("plumbing.pipe-repair", "plumbing", "Plumbing — Pipe Repair", "Leak repair, repipe, copper/PEX replacement.");

            // Electrical children (3)
            AddChild("electrical.panel", "electrical", "Electrical — Panel", "Service-panel install, upgrade, replace.");
            AddChild("electrical.lighting", "electrical", "Electrical — Lighting", "Fixture install, recessed lighting, low-voltage.");
            AddChild("electrical.ev-charger", "electrical", "Electrical — EV Charger", "Level-2 EV-charger installation.");

            // HVAC children (3)
            AddChild("hvac.central", "hvac", "HVAC — Central System", "Central AC + furnace + heat pump install/service.");
            AddChild("hvac.minisplit", "hvac", "HVAC — Mini-Split", "Ductless mini-split install + service.");
            AddChild("hvac.duct", "hvac", "HVAC — Duct Work", "Ductwork install, sealing, cleaning.");

            // Landscaping children (3)
            AddChild("landscaping.tree-service", "landscaping", "Landscaping — Tree Service", "Tree pruning, removal, stump grinding.");
            AddChild("landscaping.irrigation", "landscaping", "Landscaping — Irrigation", "Sprinkler/drip system install, repair, winterize.");
            AddChild("landscaping.snow-removal", "landscaping", "Landscaping — Snow Removal", "Plowing + de-icing (cold-climate jurisdictions).");

            // Roofing children (3)
            AddChild("roofing.shingle", "roofing", "Roofing — Shingle", "Asphalt + composite shingle install/repair.");
            AddChild("roofing.flat-roof", "roofing", "Roofing — Flat Roof", "EPDM, TPO, modified-bitumen flat-roof systems.");
            AddChild("roofing.gutter", "roofing", "Roofing — Gutter", "Gutter install, repair, leaf-guard.");

            // Cleaning children (4)
            AddChild("cleaning.move-out", "cleaning", "Cleaning — Move-Out", "Deep clean for vacating tenants.");
            AddChild("cleaning.recurring", "cleaning", "Cleaning — Recurring", "Weekly/biweekly common-area + unit cleaning.");
            AddChild("cleaning.carpet", "cleaning", "Cleaning — Carpet", "Carpet shampoo, steam, stain treatment.");
            AddChild("cleaning.window", "cleaning", "Cleaning — Window", "Interior + exterior window cleaning.");

            return new TaxonomyCorePackage
            {
                Definition = def,
                Nodes = nodes.AsReadOnly(),
            };
        }
    }
}
