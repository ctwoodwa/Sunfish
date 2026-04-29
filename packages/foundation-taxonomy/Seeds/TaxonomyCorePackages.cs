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
}
